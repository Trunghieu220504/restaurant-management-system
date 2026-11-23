using Restaurant.IntegrationTests.SalesFakes;
using Restaurant.Modules.Billing.Contracts;
using Restaurant.Modules.Catalog.Contracts;
using Restaurant.Modules.Sales.Application;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.SalesDay.Contracts;
using Restaurant.Modules.Seating.Contracts;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>
/// HIE-3.1 (first part): create order, add batch, cancel batch, written against the five contracts and run with fakes.
/// No database: the rules, the lock order and the error codes are what is under test.
/// </summary>
public sealed class SalesOrderServiceTests
{
    private static readonly Guid Day = CatalogId.Of(900);
    private static readonly Guid Staff = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime Now = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Guid Pho = CatalogId.Of(1);
    private static readonly Guid PhoRegular = CatalogId.Of(2);
    private static readonly Guid PhoLarge = CatalogId.Of(3);
    private static readonly Guid Tea = CatalogId.Of(4);
    private static readonly Guid TeaVariant = CatalogId.Of(5);
    private static readonly Guid Pearl = CatalogId.Of(6);
    private static readonly Guid Sugar = CatalogId.Of(7);

    private readonly CallLog log = new();
    private readonly InMemoryOrderStore store;
    private readonly FakeSessionLookup sessions;
    private readonly FakeSalesDayGate gate;
    private readonly FakeMenuPricing menu;
    private readonly OrderService service;

    public SalesOrderServiceTests()
    {
        store = new InMemoryOrderStore(log);
        sessions = new FakeSessionLookup(log).AddOpen(7, Day, "Table 7");
        gate = new FakeSalesDayGate(log).AddDay(Day);
        menu = new FakeMenuPricing(log)
            .AddDish(Pho, "Pho", Stations.Kitchen)
            .AddVariant(Pho, PhoRegular, "Regular", 60000)
            .AddVariant(Pho, PhoLarge, "Large", 75000)
            .AddDish(Tea, "Milk tea", Stations.Bar)
            .AddVariant(Tea, TeaVariant, "Large", 45000)
            .AddOption(Tea, Pearl, CatalogId.Of(60), "Toppings", OptionGroupKinds.Topping, "Pearl", 5000)
            .AddOption(Tea, Sugar, CatalogId.Of(61), "Sugar", OptionGroupKinds.Option, "50%", 0);
        service = new OrderService(store, sessions, gate, menu, new FixedClock(Now));
    }

    // ---- create order ----

    [Fact]
    public async Task Dine_in_order_takes_day_and_table_label_from_the_session_and_follows_the_lock_order()
    {
        var result = await service.CreateOrderAsync(DineIn(7), default);

        Assert.True(result.Succeeded);
        var view = result.Value!;
        Assert.Equal((OrderTypes.DineIn, Day, 7L, "Table 7", OrderStatus.Open), (view.Type, view.BusinessDayId, view.SessionId, view.TableLabel, view.Status));
        Assert.Equal("DH-0001", view.Code);
        Assert.True(view.Id > 0);
        var stored = Assert.Single(store.Orders);
        Assert.Equal((Now, Staff), (stored.TaoLuc, stored.NguoiTaoId));
        // Day gate before anything that locks or writes the order.
        Assert.Equal(
            ["tx.begin", "session.check:7", $"day.gate:{Day}", "order.find_open_by_session:7", "tx.commit"], log.Entries);
    }

    [Fact]
    public async Task Dine_in_order_needs_an_existing_open_session()
    {
        var missing = await service.CreateOrderAsync(DineIn(99), default);
        sessions.Close(7);
        var closed = await service.CreateOrderAsync(DineIn(7), default);

        Assert.Equal(SeatingErrorCodes.SessionNotFound, missing.Error);
        Assert.Equal(SeatingErrorCodes.SessionClosed, closed.Error);
        Assert.Empty(store.Orders);
        Assert.Equal((0, 2), (store.Commits, store.Rollbacks));
        Assert.DoesNotContain(log.Entries, x => x.StartsWith("day.gate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Order_is_refused_when_the_business_day_is_not_open_or_unknown()
    {
        gate.SetStatus(Day, SalesDayStatuses.PendingClose);
        var pending = await service.CreateOrderAsync(DineIn(7), default);
        gate.SetStatus(Day, SalesDayStatuses.Closed);
        var closed = await service.CreateOrderAsync(Takeaway(Day), default);
        var unknown = await service.CreateOrderAsync(Takeaway(CatalogId.Of(901)), default);

        Assert.Equal(SalesDayErrorCodes.DayNotOpen, pending.Error);
        Assert.Equal(SalesDayErrorCodes.DayNotOpen, closed.Error);
        Assert.Equal(SalesDayErrorCodes.DayNotFound, unknown.Error);
        Assert.Empty(store.Orders);
    }

    [Fact]
    public async Task A_session_has_one_open_order_and_a_new_one_is_allowed_after_it_closes()
    {
        var first = (await service.CreateOrderAsync(DineIn(7), default)).Value!;
        var second = await service.CreateOrderAsync(DineIn(7), default);
        Assert.Equal(SalesErrors.OrderSessionHasOpenOrder, second.Error);
        Assert.Single(store.Orders);

        store.Orders.Single(x => x.Id == first.Id).Close(Now.AddHours(1));
        var third = await service.CreateOrderAsync(DineIn(7), default);

        Assert.True(third.Succeeded);
        Assert.Equal(2, store.Orders.Count);
    }

    [Fact]
    public async Task Two_requests_racing_for_one_session_leave_one_order_and_the_loser_gets_a_business_error()
    {
        // Both passed the "no open order" check; the unique index lets only one commit.
        store.FailNextSave(StoreConflict.OpenOrderForSession);

        var result = await service.CreateOrderAsync(DineIn(7), default);

        Assert.Equal(SalesErrors.OrderSessionHasOpenOrder, result.Error);
        Assert.Empty(store.Orders);
        Assert.Equal((0, 1), (store.Commits, store.Rollbacks));
    }

    [Fact]
    public async Task Takeaway_order_has_no_session_and_carries_the_day_and_customer_name()
    {
        var result = await service.CreateOrderAsync(Takeaway(Day, "  Lan  "), default);

        Assert.True(result.Succeeded);
        Assert.Equal((OrderTypes.Takeaway, Day, (long?)null, (string?)null, "Lan"),
            (result.Value!.Type, result.Value.BusinessDayId, result.Value.SessionId, result.Value.TableLabel, result.Value.CustomerName));
        Assert.DoesNotContain(log.Entries, x => x.StartsWith("session.check", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_order_rejects_inconsistent_input_before_touching_any_module()
    {
        var bad = new[]
        {
            (Command: new CreateOrderCommand(null, 7, null, null, Staff), Error: SalesErrors.InputInvalid),
            (Command: new CreateOrderCommand("KHAC", null, Day, null, Staff), Error: SalesErrors.InputInvalid),
            (Command: new CreateOrderCommand(OrderTypes.DineIn, null, null, null, Staff), Error: SalesErrors.OrderSessionRequired),
            (Command: new CreateOrderCommand(OrderTypes.DineIn, 7, Day, null, Staff), Error: SalesErrors.InputInvalid),
            (Command: new CreateOrderCommand(OrderTypes.Takeaway, 7, Day, null, Staff), Error: SalesErrors.OrderSessionForbidden),
            (Command: new CreateOrderCommand(OrderTypes.Takeaway, null, null, null, Staff), Error: SalesErrors.InputInvalid),
            (Command: new CreateOrderCommand(OrderTypes.Takeaway, null, Guid.Empty, null, Staff), Error: SalesErrors.InputInvalid)
        };
        foreach (var (command, error) in bad)
            Assert.Equal(error, (await service.CreateOrderAsync(command, default)).Error);

        Assert.Empty(log.Entries);
        Assert.Empty(store.Orders);
    }

    [Fact]
    public async Task Domain_rules_still_apply_and_roll_back_the_unit_of_work()
    {
        var result = await service.CreateOrderAsync(new CreateOrderCommand(OrderTypes.DineIn, 7, null, null, Guid.Empty), default);

        Assert.Equal(SalesErrors.InputInvalid, result.Error);
        Assert.Empty(store.Orders);
        Assert.Equal((0, 1), (store.Commits, store.Rollbacks));
    }

    // ---- add batch ----

    [Fact]
    public async Task Staff_batch_is_priced_by_the_menu_for_the_orders_day_and_stored_with_snapshots()
    {
        var order = await OpenOrderAsync();
        log.Clear();

        var result = await service.AddBatchAsync(Add(order, Line(Pho, PhoLarge, note: " no onion "),
            Line(Tea, TeaVariant, [Pearl, Sugar])), default);

        Assert.True(result.Succeeded);
        Assert.False(result.Replayed);
        var batch = result.Value!;
        Assert.Equal((1, BatchStatus.Pending, order), (batch.Number, batch.Status, batch.OrderId));
        Assert.Equal(75000 + 50000, batch.Total);
        Assert.Equal(["Pho", "Milk tea"], batch.Portions.Select(x => x.MenuItemName));
        Assert.Equal([75000L, 50000L], batch.Portions.Select(x => x.UnitPrice));
        Assert.Equal(["Pearl", "50%"], batch.Portions[1].OptionNames);
        Assert.All(batch.Portions, x => Assert.True(x.Id > 0));

        var request = Assert.Single(menu.Requests);
        Assert.Equal(Day, request.BusinessDayId);
        Assert.Equal([1, 2], request.Lines.Select(x => x.LineNo));

        var stored = Assert.Single(Assert.Single(store.Orders).Dots);
        var tea = stored.Portions.Single(x => x.TenMonChot == "Milk tea");
        Assert.Equal((Tea, TeaVariant, 45000L, Stations.Bar), (tea.MonId, tea.BienTheId, tea.GiaMonChot, tea.TramChot));
        Assert.Equal("no onion", stored.Portions.Single(x => x.TenMonChot == "Pho").GhiChu);
        Assert.Equal(Now, stored.TaoLuc);
        Assert.Equal(Staff, stored.NguoiTaoId);
    }

    [Fact]
    public async Task Add_batch_gates_the_day_before_it_locks_the_order_and_prices_after_both()
    {
        var order = await OpenOrderAsync();
        log.Clear();

        await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)), default);

        Assert.Equal(["tx.begin", $"order.find_day:{order}", $"day.gate:{Day}", $"order.lock:{order}", $"menu.price:{Day}", "tx.commit"],
            log.Entries);
    }

    [Fact]
    public async Task Additional_batches_get_the_next_number()
    {
        var order = await OpenOrderAsync();

        var first = await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)), default);
        var second = await service.AddBatchAsync(Add(order, Line(Pho, PhoLarge)), default);

        Assert.Equal([1, 2], [first.Value!.Number, second.Value!.Number]);
        Assert.NotEqual(first.Value.BatchId, second.Value.BatchId);
    }

    [Fact]
    public async Task Repeating_a_request_key_returns_the_same_batch_and_adds_nothing()
    {
        var order = await OpenOrderAsync();
        var key = Guid.NewGuid();
        var first = await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)) with { RequestKey = key }, default);
        menu.Requests.Clear();
        // The menu price changed in between: a retry must not re-price or duplicate.
        menu.SetPrice(PhoRegular, 99000);

        var again = await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)) with { RequestKey = key }, default);

        Assert.True(again.Succeeded);
        Assert.True(again.Replayed);
        Assert.Equal(first.Value!.BatchId, again.Value!.BatchId);
        Assert.Equal(60000, again.Value.Total);
        Assert.Empty(menu.Requests);
        Assert.Single(Assert.Single(store.Orders).Dots);
    }

    [Fact]
    public async Task A_retry_is_still_answered_after_the_order_closed()
    {
        var order = await OpenOrderAsync();
        var key = Guid.NewGuid();
        await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)) with { RequestKey = key }, default);
        var stored = Assert.Single(store.Orders);
        stored.CancelBatch(1);
        Assert.True(stored.Close(Now.AddHours(1)).Succeeded);

        var again = await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)) with { RequestKey = key }, default);
        var fresh = await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)), default);

        Assert.True(again.Replayed);
        Assert.Equal(SalesErrors.OrderNotOpen, fresh.Error);
    }

    [Fact]
    public async Task Add_batch_to_an_unknown_order_stops_before_the_day_gate()
    {
        var result = await service.AddBatchAsync(Add(404, Line(Pho, PhoRegular)), default);

        Assert.Equal(SalesErrors.OrderNotFound, result.Error);
        Assert.DoesNotContain(log.Entries, x => x.StartsWith("day.gate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Add_batch_is_refused_when_the_day_left_the_open_state()
    {
        var order = await OpenOrderAsync();
        gate.SetStatus(Day, SalesDayStatuses.PendingClose);

        var result = await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)), default);

        Assert.Equal(SalesDayErrorCodes.DayNotOpen, result.Error);
        Assert.Empty(Assert.Single(store.Orders).Dots);
        Assert.Empty(menu.Requests);
    }

    [Fact]
    public async Task Add_batch_to_a_closed_or_cancelled_order_is_refused_without_pricing()
    {
        var closed = await OpenOrderAsync();
        Assert.True(store.Orders.Single(x => x.Id == closed).Close(Now.AddHours(1)).Succeeded);
        var cancelled = Seed(OrderTypes.Takeaway);
        Assert.True(cancelled.Cancel(Now.AddHours(1)).Succeeded);

        Assert.Equal(SalesErrors.OrderNotOpen, (await service.AddBatchAsync(Add(closed, Line(Pho, PhoRegular)), default)).Error);
        Assert.Equal(SalesErrors.OrderNotOpen, (await service.AddBatchAsync(Add(cancelled.Id, Line(Pho, PhoRegular)), default)).Error);
        Assert.Empty(menu.Requests);
    }

    [Fact]
    public async Task Menu_rejections_are_returned_per_line_and_nothing_is_added()
    {
        var order = await OpenOrderAsync();
        menu.SellOutOption(Tea, Pearl);

        var result = await service.AddBatchAsync(Add(order,
            Line(Pho, PhoRegular),
            Line(Tea, TeaVariant, [Pearl]),
            Line(Pho, TeaVariant),
            Line(CatalogId.Of(777), PhoRegular)), default);

        Assert.Equal(SalesErrors.BatchLineInvalid, result.Error);
        Assert.Equal(
            [(2, CatalogErrorCodes.OptionSoldOut), (3, CatalogErrorCodes.VariantNotOwned), (4, CatalogErrorCodes.ItemNotFound)],
            result.LineErrors.Select(x => (x.LineNo, x.Code)));
        Assert.Empty(Assert.Single(store.Orders).Dots);
        Assert.Equal(1, store.Rollbacks);
    }

    [Fact]
    public async Task Add_batch_validates_the_request_shape()
    {
        var order = await OpenOrderAsync();

        Assert.Equal(SalesErrors.BatchEmpty, (await service.AddBatchAsync(Add(order), default)).Error);
        Assert.Equal(SalesErrors.BatchEmpty, (await service.AddBatchAsync(Add(order) with { Lines = null }, default)).Error);
        Assert.Equal(SalesErrors.InputInvalid,
            (await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)) with { RequestKey = Guid.Empty }, default)).Error);
        Assert.Equal(SalesErrors.InputInvalid,
            (await service.AddBatchAsync(Add(0, Line(Pho, PhoRegular)), default)).Error);
        Assert.Equal(SalesErrors.InputInvalid,
            (await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)) with { Source = "KHAC" }, default)).Error);
        Assert.Equal(SalesErrors.InputInvalid,
            (await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)) with { StaffId = null }, default)).Error);
        Assert.Empty(Assert.Single(store.Orders).Dots);
    }

    [Fact]
    public async Task A_guest_can_add_to_a_dine_in_order_but_not_to_a_takeaway_order()
    {
        var dineIn = await OpenOrderAsync();
        var takeaway = Seed(OrderTypes.Takeaway).Id;
        var grant = Guid.NewGuid();

        var ok = await service.AddBatchAsync(
            Add(dineIn, Line(Pho, PhoRegular)) with { Source = BatchSource.Guest, StaffId = null, GuestGrantId = grant }, default);
        var refused = await service.AddBatchAsync(
            Add(takeaway, Line(Pho, PhoRegular)) with { Source = BatchSource.Guest, StaffId = null, GuestGrantId = grant }, default);

        Assert.True(ok.Succeeded);
        Assert.Equal(SalesErrors.BatchGuestNeedsSession, refused.Error);
    }

    [Fact]
    public async Task A_concurrent_change_is_reported_as_a_business_error_and_not_committed()
    {
        var order = await OpenOrderAsync();
        store.FailNextSave(StoreConflict.ConcurrentChange);

        var result = await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)), default);

        Assert.Equal(SalesErrors.OrderChanged, result.Error);
        Assert.Equal(1, store.Commits);
        Assert.Equal(1, store.Rollbacks);
    }

    [Fact]
    public async Task A_menu_answer_that_misses_a_line_is_not_trusted()
    {
        var order = await OpenOrderAsync();
        var short1 = new OrderService(store, sessions, gate, new ShortPricing(), new FixedClock(Now));

        var result = await short1.AddBatchAsync(Add(order, Line(Pho, PhoRegular), Line(Pho, PhoLarge)), default);

        Assert.Equal(SalesErrors.PricingInconsistent, result.Error);
        Assert.Empty(Assert.Single(store.Orders).Dots);
    }

    [Fact]
    public async Task A_menu_answer_for_another_dish_variant_or_option_is_not_trusted()
    {
        var order = await OpenOrderAsync();
        var requests = new[]
        {
            Line(Pho, PhoRegular),
            Line(Tea, TeaVariant, [Pearl])
        };
        // Each answer is well-formed (one line per request line) but describes something else.
        var wrong = new (string Name, Func<PricedMenuLine, PricedMenuLine> Change, BatchLine Asked)[]
        {
            ("dish", p => p with { MenuItemId = Tea }, requests[0]),
            ("variant", p => p with { VariantId = PhoLarge }, requests[0]),
            ("option", p => p with { Options = [] }, requests[1]),
            ("extra option", p => p with { Options = [.. p.Options, new PricedOption(Sugar, Guid.NewGuid(), "Sugar", OptionGroupKinds.Option, "50%", 0)] }, requests[1])
        };

        foreach (var (name, change, asked) in wrong)
        {
            var honest = (await new FakeMenuPricing().AddDish(Pho, "Pho", Stations.Kitchen).AddVariant(Pho, PhoRegular, "Regular", 60000)
                .AddDish(Tea, "Milk tea", Stations.Bar).AddVariant(Tea, TeaVariant, "Large", 45000)
                .AddOption(Tea, Pearl, CatalogId.Of(60), "Toppings", OptionGroupKinds.Topping, "Pearl", 5000)
                .AddOption(Tea, Sugar, CatalogId.Of(61), "Sugar", OptionGroupKinds.Option, "50%", 0)
                .PriceAsync(new MenuPricingRequest(Guid.NewGuid(), [new MenuLineSelection(1, asked.MenuItemId, asked.VariantId, asked.OptionIds ?? [])]), default))
                .Lines.Single();
            var service2 = new OrderService(store, sessions, gate, new FixedPricing(change(honest)), new FixedClock(Now));

            var result = await service2.AddBatchAsync(Add(order, asked), default);

            Assert.True(result.Error == SalesErrors.PricingInconsistent, $"{name}: {result.Error}");
        }
        Assert.Empty(Assert.Single(store.Orders).Dots);
    }

    // ---- cancel batch ----

    [Fact]
    public async Task Cancelling_a_pending_batch_cancels_its_portions_and_leaves_other_batches_alone()
    {
        var order = await OpenOrderAsync();
        await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular), Line(Tea, TeaVariant)), default);
        await service.AddBatchAsync(Add(order, Line(Pho, PhoLarge)), default);
        log.Clear();

        var result = await service.CancelBatchAsync(new CancelBatchCommand(order, 1), default);

        Assert.True(result.Succeeded);
        Assert.Equal((BatchStatus.Cancelled, 0L), (result.Value!.Status, result.Value.Total));
        Assert.All(result.Value.Portions, x => Assert.Equal(PortionStatus.Cancelled, x.Status));
        var dots = Assert.Single(store.Orders).Dots;
        Assert.Equal([BatchStatus.Cancelled, BatchStatus.Pending], dots.Select(x => x.TrangThai));
        Assert.Equal(["tx.begin", $"order.find_day:{order}", $"day.gate:{Day}", $"order.lock:{order}", "tx.commit"], log.Entries);
    }

    [Fact]
    public async Task A_batch_that_was_already_decided_or_withdrawn_cannot_be_withdrawn()
    {
        var order = await OpenOrderAsync();
        await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)), default);
        await service.AddBatchAsync(Add(order, Line(Pho, PhoLarge)), default);
        var stored = Assert.Single(store.Orders);
        Assert.True(stored.ConfirmBatch(1, Staff, Now).Succeeded);

        var confirmed = await service.CancelBatchAsync(new CancelBatchCommand(order, 1), default);
        Assert.True((await service.CancelBatchAsync(new CancelBatchCommand(order, 2), default)).Succeeded);
        var twice = await service.CancelBatchAsync(new CancelBatchCommand(order, 2), default);

        Assert.Equal(SalesErrors.BatchNotPending, confirmed.Error);
        Assert.Equal(SalesErrors.BatchNotPending, twice.Error);
        Assert.Equal(BatchStatus.Confirmed, stored.Dots[0].TrangThai);
    }

    [Fact]
    public async Task Cancel_batch_reports_unknown_order_unknown_batch_closed_day_and_closed_order()
    {
        var order = await OpenOrderAsync();
        await service.AddBatchAsync(Add(order, Line(Pho, PhoRegular)), default);

        Assert.Equal(SalesErrors.OrderNotFound, (await service.CancelBatchAsync(new CancelBatchCommand(404, 1), default)).Error);
        Assert.Equal(SalesErrors.BatchNotFound, (await service.CancelBatchAsync(new CancelBatchCommand(order, 5), default)).Error);
        Assert.Equal(SalesErrors.InputInvalid, (await service.CancelBatchAsync(new CancelBatchCommand(order, 0), default)).Error);
        Assert.Equal(SalesErrors.InputInvalid, (await service.CancelBatchAsync(new CancelBatchCommand(0, 1), default)).Error);

        gate.SetStatus(Day, SalesDayStatuses.Closed);
        Assert.Equal(SalesDayErrorCodes.DayNotOpen, (await service.CancelBatchAsync(new CancelBatchCommand(order, 1), default)).Error);
        Assert.Equal(BatchStatus.Pending, Assert.Single(store.Orders).Dots.Single().TrangThai);

        gate.SetStatus(Day, SalesDayStatuses.Open);
        var stored = Assert.Single(store.Orders);
        stored.CancelBatch(1);
        Assert.True(stored.Close(Now.AddHours(1)).Succeeded);
        Assert.Equal(SalesErrors.OrderNotOpen, (await service.CancelBatchAsync(new CancelBatchCommand(order, 1), default)).Error);
    }

    // ---- the fake for Billing (used by the confirm step that follows) ----

    [Fact]
    public async Task The_billing_fake_keeps_one_invoice_per_order_and_one_line_per_portion()
    {
        var billing = new FakeInvoiceCharging();
        ChargeBatchRequest Request(Guid key, params long[] portions) => new(
            1, 1, Day, key, Staff, [.. portions.Select(p => new ChargeLine(p, "Pho", 60000))]);
        var key = Guid.NewGuid();

        var first = await billing.ChargeConfirmedBatchAsync(Request(key, 10, 11), default);
        var sameKey = await billing.ChargeConfirmedBatchAsync(Request(key, 10, 11), default);
        var samePortions = await billing.ChargeConfirmedBatchAsync(Request(Guid.NewGuid(), 10, 11), default);
        var more = await billing.ChargeConfirmedBatchAsync(Request(Guid.NewGuid(), 12), default);

        Assert.True(first.IsCharged);
        Assert.False(first.Replayed);
        Assert.True(sameKey.Replayed);
        Assert.True(samePortions.Replayed);
        Assert.Equal(first.InvoiceId, more.InvoiceId);
        Assert.False(more.Replayed);
        Assert.Equal(1, billing.InvoiceCount);
        Assert.Equal([10L, 11L, 12L], billing.ChargedPortions.Keys.Order());
    }

    [Fact]
    public async Task The_billing_fake_refuses_bad_lines_and_closed_invoices()
    {
        var billing = new FakeInvoiceCharging();
        var bad = new[]
        {
            new ChargeBatchRequest(1, 1, Day, Guid.NewGuid(), Staff, []),
            new ChargeBatchRequest(1, 1, Day, Guid.NewGuid(), Staff, [new ChargeLine(0, "Pho", 1)]),
            new ChargeBatchRequest(1, 1, Day, Guid.NewGuid(), Staff, [new ChargeLine(1, "Pho", -1)]),
            new ChargeBatchRequest(1, 1, Day, Guid.NewGuid(), Staff, [new ChargeLine(1, "Pho", 1), new ChargeLine(1, "Pho", 1)])
        };
        foreach (var request in bad)
            Assert.Equal(BillingErrorCodes.LineInvalid, (await billing.ChargeConfirmedBatchAsync(request, default)).ErrorCode);

        billing.CloseInvoiceOf(2);
        var closed = await billing.ChargeConfirmedBatchAsync(
            new ChargeBatchRequest(2, 1, Day, Guid.NewGuid(), Staff, [new ChargeLine(1, "Pho", 1)]), default);
        Assert.Equal(BillingErrorCodes.InvoiceClosed, closed.ErrorCode);
        Assert.Equal(0, billing.InvoiceCount);
    }

    // ---- helpers ----

    private async Task<long> OpenOrderAsync()
    {
        var created = await service.CreateOrderAsync(DineIn(7), default);
        Assert.True(created.Succeeded);
        return created.Value!.Id;
    }

    private DonHang Seed(string type) => store.Seed(type == OrderTypes.DineIn
        ? DonHang.Create("DH-S", type, Day, 8, "Table 8", null, Staff, Now).Value!
        : DonHang.Create("DH-T", type, Day, null, null, "Seed", Staff, Now).Value!);

    private static CreateOrderCommand DineIn(long session) => new(OrderTypes.DineIn, session, null, null, Staff);

    private static CreateOrderCommand Takeaway(Guid day, string? name = null) => new(OrderTypes.Takeaway, null, day, name, Staff);

    private static AddBatchCommand Add(long order, params BatchLine[] lines) =>
        new(order, BatchSource.Staff, null, Staff, Guid.NewGuid(), lines);

    private static BatchLine Line(Guid dish, Guid variant, Guid[]? options = null, string? note = null) =>
        new(dish, variant, options ?? [], note);

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    /// <summary>A menu that answers one line fewer than asked, which a correct Catalog never does.</summary>
    private sealed class FixedPricing(PricedMenuLine line) : IMenuPricing
    {
        public Task<MenuPricingResult> PriceAsync(MenuPricingRequest request, CancellationToken ct) =>
            Task.FromResult(new MenuPricingResult([line with { LineNo = 1 }], []));
    }

    private sealed class ShortPricing : IMenuPricing
    {
        public Task<MenuPricingResult> PriceAsync(MenuPricingRequest request, CancellationToken ct) =>
            Task.FromResult(new MenuPricingResult(
                [new PricedMenuLine(1, Pho, "Pho", PhoRegular, "Regular", 60000, Stations.Kitchen, [], 60000)], []));
    }
}
