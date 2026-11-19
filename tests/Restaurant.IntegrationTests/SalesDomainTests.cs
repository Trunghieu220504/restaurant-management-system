using Restaurant.Modules.Sales.Domain;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>HIE-2.2: the order, batch and portion state machines and the order aggregate rules. No database needed.</summary>
public sealed class SalesDomainTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Day = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Staff = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Grant = Guid.Parse("55555555-5555-5555-5555-555555555555");

    // ---- state machines: every (from, to) pair is checked, not only the expected ones ----

    public static TheoryData<string, string, string, bool> OrderPairs => Pairs(SalesStateMachines.Order,
        (OrderStatus.Open, OrderStatus.Closed), (OrderStatus.Open, OrderStatus.Cancelled));

    public static TheoryData<string, string, string, bool> BatchPairs => Pairs(SalesStateMachines.Batch,
        (BatchStatus.Pending, BatchStatus.Confirmed), (BatchStatus.Pending, BatchStatus.Rejected),
        (BatchStatus.Pending, BatchStatus.Cancelled));

    public static TheoryData<string, string, string, bool> PortionPairs => Pairs(SalesStateMachines.Portion,
        (PortionStatus.Pending, PortionStatus.Confirmed), (PortionStatus.Pending, PortionStatus.Cancelled),
        (PortionStatus.Confirmed, PortionStatus.Cancelled));

    private static TheoryData<string, string, string, bool> Pairs(StateMachine machine, params (string, string)[] valid)
    {
        var data = new TheoryData<string, string, string, bool>();
        var name = machine == SalesStateMachines.Order ? "order" : machine == SalesStateMachines.Batch ? "batch" : "portion";
        foreach (var from in machine.States)
            foreach (var to in machine.States)
                data.Add(name, from, to, valid.Contains((from, to)));
        return data;
    }

    private static StateMachine Machine(string name) => name switch
    {
        "order" => SalesStateMachines.Order,
        "batch" => SalesStateMachines.Batch,
        _ => SalesStateMachines.Portion
    };

    [Theory, MemberData(nameof(OrderPairs)), MemberData(nameof(BatchPairs)), MemberData(nameof(PortionPairs))]
    public void Only_listed_status_moves_are_allowed(string machine, string from, string to, bool expected) =>
        Assert.Equal(expected, Machine(machine).CanMove(from, to));

    [Fact]
    public void Final_states_cannot_be_left_and_unknown_states_are_refused()
    {
        Assert.All(new[] { OrderStatus.Closed, OrderStatus.Cancelled }, s => Assert.True(SalesStateMachines.Order.IsTerminal(s)));
        Assert.All(new[] { BatchStatus.Confirmed, BatchStatus.Rejected, BatchStatus.Cancelled },
            s => Assert.True(SalesStateMachines.Batch.IsTerminal(s)));
        Assert.True(SalesStateMachines.Portion.IsTerminal(PortionStatus.Cancelled));
        Assert.False(SalesStateMachines.Portion.IsTerminal(PortionStatus.Confirmed));
        Assert.False(SalesStateMachines.Order.CanMove("MO", "KHONG_CO"));
        Assert.False(SalesStateMachines.Order.CanMove("", OrderStatus.Closed));
        Assert.Throws<ArgumentException>(() => new StateMachine(["A"], ("A", "B")));
    }

    // ---- order creation ----

    [Fact]
    public void A_dine_in_order_needs_a_session_and_a_takeaway_order_must_not_have_one()
    {
        Assert.Equal(SalesErrors.OrderSessionRequired, Order(OrderTypes.DineIn, session: null).Error);
        Assert.Equal(SalesErrors.InputInvalid, Order(OrderTypes.DineIn, session: 0).Error);
        Assert.Equal(SalesErrors.InputInvalid, Order(OrderTypes.DineIn, session: -3).Error);
        Assert.Equal(SalesErrors.OrderSessionForbidden, Order(OrderTypes.Takeaway, session: 9).Error);
        Assert.Equal(SalesErrors.OrderSessionForbidden, Order(OrderTypes.Takeaway, session: null, label: "B1").Error);
        Assert.Equal(SalesErrors.InputInvalid, Order("TAI_CHO", session: 9).Error);
        Assert.Equal(SalesErrors.InputInvalid, Order(null, session: 9).Error);

        var dineIn = Order(OrderTypes.DineIn, session: 9, label: " Table 1 ").Value!;
        Assert.Equal(OrderStatus.Open, dineIn.TrangThai);
        Assert.Equal(9, dineIn.PhienSuDungId);
        Assert.Equal("Table 1", dineIn.NhanBanLucTao);
        Assert.Null(dineIn.DongLuc);
        var takeaway = Order(OrderTypes.Takeaway, session: null, customer: "An").Value!;
        Assert.Null(takeaway.PhienSuDungId);
        Assert.Equal("An", takeaway.TenKhach);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-A1")]
    [InlineData("A 1")]
    [InlineData("0123456789012345678901234567890123")]
    public void An_order_code_must_be_a_short_code(string? code) =>
        Assert.Equal(SalesErrors.InputInvalid, Order(OrderTypes.DineIn, session: 1, code: code).Error);

    [Fact]
    public void An_order_needs_a_real_day_and_creator_and_bounded_text()
    {
        Assert.Equal(SalesErrors.InputInvalid, DonHang.Create("A1", OrderTypes.DineIn, Guid.Empty, 1, null, null, Staff, T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, DonHang.Create("A1", OrderTypes.DineIn, Day, 1, null, null, Guid.Empty, T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, Order(OrderTypes.Takeaway, session: null, customer: new string('x', 151)).Error);
        Assert.True(Order(OrderTypes.Takeaway, session: null, customer: new string('x', 150)).Succeeded);
        Assert.Null(Order(OrderTypes.Takeaway, session: null, customer: "  ").Value!.TenKhach);
        Assert.Null(DonHang.Create("A1", OrderTypes.DineIn, Day, 1, null, null, null, T0).Value!.NguoiTaoId);
    }

    // ---- adding batches ----

    [Fact]
    public void Batches_are_numbered_in_order_and_start_pending_with_pending_portions()
    {
        var order = OpenOrder();
        var first = Add(order);
        var second = Add(order, portions: [Portion(), Portion()]);
        Assert.Equal(1, first.SoDot);
        Assert.Equal(2, second.SoDot);
        Assert.Equal(BatchStatus.Pending, second.TrangThai);
        Assert.Equal(BatchSource.Staff, second.Nguon);
        Assert.Equal(Staff, second.NguoiTaoId);
        Assert.Null(second.QuyenTruyCapKhachId);
        Assert.Equal(2, second.Portions.Count);
        Assert.All(second.Portions, p => Assert.Equal(PortionStatus.Pending, p.TrangThai));
        Assert.Equal(2, order.Dots.Count);
    }

    [Fact]
    public void A_batch_cannot_be_added_to_a_closed_or_cancelled_order()
    {
        var closed = OpenOrder();
        Assert.True(closed.Close(T0.AddMinutes(30)).Succeeded);
        var cancelled = OpenOrder();
        Assert.True(cancelled.Cancel(T0.AddMinutes(30)).Succeeded);
        foreach (var order in new[] { closed, cancelled })
        {
            var result = order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [Portion()], T0);
            Assert.Equal(SalesErrors.OrderNotOpen, result.Error);
            Assert.Empty(order.Dots);
        }
    }

    [Fact]
    public void The_batch_creator_must_match_its_source()
    {
        var order = OpenOrder();
        Assert.Equal(SalesErrors.InputInvalid, order.AddBatch(BatchSource.Staff, null, null, Guid.NewGuid(), [Portion()], T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.AddBatch(BatchSource.Staff, null, Guid.Empty, Guid.NewGuid(), [Portion()], T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.AddBatch(BatchSource.Staff, Grant, Staff, Guid.NewGuid(), [Portion()], T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.AddBatch(BatchSource.Guest, null, null, Guid.NewGuid(), [Portion()], T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.AddBatch(BatchSource.Guest, Guid.Empty, null, Guid.NewGuid(), [Portion()], T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.AddBatch(BatchSource.Guest, Grant, Staff, Guid.NewGuid(), [Portion()], T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.AddBatch("ROBOT", null, Staff, Guid.NewGuid(), [Portion()], T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.AddBatch(null, null, Staff, Guid.NewGuid(), [Portion()], T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.AddBatch(BatchSource.Staff, null, Staff, Guid.Empty, [Portion()], T0).Error);
        Assert.Empty(order.Dots);

        var guest = order.AddBatch(BatchSource.Guest, Grant, null, Guid.NewGuid(), [Portion()], T0).Value!;
        Assert.Equal(Grant, guest.QuyenTruyCapKhachId);
        Assert.Null(guest.NguoiTaoId);
    }

    [Fact]
    public void A_guest_can_only_order_into_a_dine_in_order()
    {
        var takeaway = Order(OrderTypes.Takeaway, session: null).Value!;
        Assert.Equal(SalesErrors.BatchGuestNeedsSession,
            takeaway.AddBatch(BatchSource.Guest, Grant, null, Guid.NewGuid(), [Portion()], T0).Error);
        Assert.True(takeaway.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [Portion()], T0).Succeeded);
    }

    [Fact]
    public void A_batch_needs_at_least_one_valid_portion()
    {
        var order = OpenOrder();
        Assert.Equal(SalesErrors.BatchEmpty, order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), null, T0).Error);
        Assert.Equal(SalesErrors.BatchEmpty, order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [], T0).Error);
        foreach (var bad in new[]
                 {
                     Portion() with { Price = -1 }, Portion() with { Station = "KHO" }, Portion() with { DishId = 0 },
                     Portion() with { VariantId = 0 }, Portion() with { DishName = " " },
                     Portion() with { VariantName = new string('x', 151) }, Portion() with { Note = new string('x', 2001) }
                 })
            Assert.Equal(SalesErrors.InputInvalid, order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [bad], T0).Error);
        // One bad portion rejects the whole batch: nothing is half added.
        Assert.Equal(SalesErrors.InputInvalid,
            order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [Portion(), Portion() with { Price = -5 }], T0).Error);
        Assert.Empty(order.Dots);
    }

    [Fact]
    public void Options_are_copied_with_their_group_and_validated()
    {
        var order = OpenOrder();
        var good = Portion() with
        {
            Options = [new NewOption(7, 2, " Topping ", OptionGroupKinds.Topping, " Pearl ", 5000), new NewOption(8, 3, "Sugar", OptionGroupKinds.Option, "50%", 0)]
        };
        var portion = order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [good], T0).Value!.Portions.Single();
        Assert.Equal(2, portion.Options.Count);
        Assert.Contains(portion.Options, o => o.TuyChonId == 7 && o.TenNhomChot == "Topping" && o.TenTuyChonChot == "Pearl" && o.GiaCongChot == 5000);

        foreach (var bad in new[]
                 {
                     new NewOption(0, 2, "G", OptionGroupKinds.Topping, "O", 0), new NewOption(7, 0, "G", OptionGroupKinds.Topping, "O", 0),
                     new NewOption(7, 2, "G", "KHAC", "O", 0), new NewOption(7, 2, " ", OptionGroupKinds.Topping, "O", 0),
                     new NewOption(7, 2, "G", OptionGroupKinds.Topping, "", 0), new NewOption(7, 2, "G", OptionGroupKinds.Topping, "O", -1)
                 })
            Assert.Equal(SalesErrors.InputInvalid,
                order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [Portion() with { Options = [bad] }], T0).Error);
        var twice = Portion() with
        {
            Options = [new NewOption(7, 2, "G", OptionGroupKinds.Topping, "O", 0), new NewOption(7, 2, "G", OptionGroupKinds.Topping, "O", 0)]
        };
        Assert.Equal(SalesErrors.InputInvalid, order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [twice], T0).Error);
    }

    // ---- confirming, rejecting and cancelling batches ----

    [Fact]
    public void Confirming_a_pending_batch_confirms_its_portions_and_records_who_and_when()
    {
        var order = OpenOrder();
        var batch = Add(order, portions: [Portion(), Portion()]);
        Assert.True(order.ConfirmBatch(1, Staff, T0.AddMinutes(1)).Succeeded);
        Assert.Equal(BatchStatus.Confirmed, batch.TrangThai);
        Assert.Equal(Staff, batch.NguoiXacNhanId);
        Assert.Equal(T0.AddMinutes(1), batch.XacNhanLuc);
        Assert.Null(batch.LyDoTuChoi);
        Assert.All(batch.Portions, p => Assert.Equal(PortionStatus.Confirmed, p.TrangThai));
    }

    [Fact]
    public void A_cancelled_portion_stays_cancelled_when_its_batch_is_confirmed()
    {
        var order = OpenOrder();
        var batch = Add(order, portions: [Portion(), Portion()]);
        Number(batch);
        Assert.True(order.CancelPortion(1, batch.Portions[0].Id).Succeeded);
        Assert.True(order.ConfirmBatch(1, Staff, T0).Succeeded);
        Assert.Equal(PortionStatus.Cancelled, batch.Portions[0].TrangThai);
        Assert.Equal(PortionStatus.Confirmed, batch.Portions[1].TrangThai);
    }

    [Fact]
    public void Cancelling_the_last_portion_of_a_pending_batch_withdraws_the_batch()
    {
        var order = OpenOrder();
        var batch = Add(order);
        Number(batch);
        Assert.True(order.CancelPortion(1, batch.Portions[0].Id).Succeeded);
        Assert.Equal(BatchStatus.Cancelled, batch.TrangThai);
        Assert.Equal(SalesErrors.BatchNotPending, order.ConfirmBatch(1, Staff, T0).Error);
        Assert.Equal(SalesErrors.BatchNotPending, order.RejectBatch(1, Staff, "No", T0).Error);
        Assert.Equal(SalesErrors.BatchNotPending, order.CancelBatch(1).Error);
        // an emptied batch no longer blocks closing the order
        Assert.True(order.Close(T0.AddHours(1)).Succeeded);
    }

    [Fact]
    public void Cancelling_the_last_portion_of_a_confirmed_batch_keeps_the_batch_confirmed()
    {
        var order = OpenOrder();
        var batch = Add(order);
        Number(batch);
        Assert.True(order.ConfirmBatch(1, Staff, T0).Succeeded);
        Assert.True(order.CancelPortion(1, batch.Portions[0].Id).Succeeded);
        Assert.Equal(BatchStatus.Confirmed, batch.TrangThai);
    }

    [Fact]
    public void Every_change_under_an_order_bumps_its_update_counter()
    {
        var order = OpenOrder();
        Assert.Equal(0, order.SoLanCapNhat);
        var first = Add(order, portions: [Portion(), Portion()]);
        Number(first);
        var afterAdd = order.SoLanCapNhat;
        Assert.Equal(1, afterAdd);
        Assert.True(order.CancelPortion(1, first.Portions[0].Id).Succeeded);
        Assert.Equal(afterAdd + 1, order.SoLanCapNhat);
        Assert.True(order.ConfirmBatch(1, Staff, T0).Succeeded);
        Assert.Equal(afterAdd + 2, order.SoLanCapNhat);
        Add(order);
        Assert.True(order.RejectBatch(2, Staff, "No", T0).Succeeded);
        Add(order);
        Assert.True(order.CancelBatch(3).Succeeded);
        Assert.Equal(afterAdd + 6, order.SoLanCapNhat);
        Assert.True(order.Close(T0.AddHours(1)).Succeeded);
        Assert.Equal(afterAdd + 7, order.SoLanCapNhat);
    }

    [Fact]
    public void A_refused_change_does_not_bump_the_update_counter()
    {
        var order = OpenOrder();
        Add(order);
        var before = order.SoLanCapNhat;
        Assert.False(order.Close(T0.AddHours(1)).Succeeded);
        Assert.False(order.ConfirmBatch(9, Staff, T0).Succeeded);
        Assert.False(order.RejectBatch(1, Staff, " ", T0).Succeeded);
        Assert.Equal(before, order.SoLanCapNhat);
    }

    [Fact]
    public void Only_a_pending_batch_can_be_confirmed_rejected_or_cancelled()
    {
        foreach (var finish in new Func<DonHang, SalesResult>[]
                 {
                     o => o.ConfirmBatch(1, Staff, T0), o => o.RejectBatch(1, Staff, "Out", T0), o => o.CancelBatch(1)
                 })
        {
            foreach (var again in new Func<DonHang, SalesResult>[]
                     {
                         o => o.ConfirmBatch(1, Staff, T0), o => o.RejectBatch(1, Staff, "Out", T0), o => o.CancelBatch(1)
                     })
            {
                var order = OpenOrder();
                var batch = Add(order);
                Assert.True(finish(order).Succeeded);
                var status = batch.TrangThai;
                Assert.Equal(SalesErrors.BatchNotPending, again(order).Error);
                Assert.Equal(status, batch.TrangThai);
            }
        }
    }

    [Fact]
    public void Rejecting_needs_a_reason_and_cancels_the_portions()
    {
        var order = OpenOrder();
        var batch = Add(order, portions: [Portion(), Portion()]);
        Assert.Equal(SalesErrors.InputInvalid, order.RejectBatch(1, Staff, null, T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.RejectBatch(1, Staff, "  ", T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.RejectBatch(1, Staff, new string('x', 1001), T0).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.RejectBatch(1, Guid.Empty, "Sold out", T0).Error);
        Assert.Equal(BatchStatus.Pending, batch.TrangThai);

        Assert.True(order.RejectBatch(1, Staff, " Sold out ", T0.AddMinutes(2)).Succeeded);
        Assert.Equal(BatchStatus.Rejected, batch.TrangThai);
        Assert.Equal("Sold out", batch.LyDoTuChoi);
        Assert.Equal(Staff, batch.NguoiXacNhanId);
        Assert.All(batch.Portions, p => Assert.Equal(PortionStatus.Cancelled, p.TrangThai));
    }

    [Fact]
    public void Cancelling_a_pending_batch_cancels_its_portions_without_a_decision()
    {
        var order = OpenOrder();
        var batch = Add(order);
        Assert.True(order.CancelBatch(1).Succeeded);
        Assert.Equal(BatchStatus.Cancelled, batch.TrangThai);
        Assert.Null(batch.NguoiXacNhanId);
        Assert.Null(batch.XacNhanLuc);
        Assert.All(batch.Portions, p => Assert.Equal(PortionStatus.Cancelled, p.TrangThai));
    }

    [Fact]
    public void Unknown_batches_and_an_unknown_actor_are_refused()
    {
        var order = OpenOrder();
        Add(order);
        Assert.Equal(SalesErrors.BatchNotFound, order.ConfirmBatch(2, Staff, T0).Error);
        Assert.Equal(SalesErrors.BatchNotFound, order.RejectBatch(0, Staff, "x", T0).Error);
        Assert.Equal(SalesErrors.BatchNotFound, order.CancelBatch(-1).Error);
        Assert.Equal(SalesErrors.BatchNotFound, order.CancelPortion(2, 1).Error);
        Assert.Equal(SalesErrors.InputInvalid, order.ConfirmBatch(1, Guid.Empty, T0).Error);
    }

    [Fact]
    public void Nothing_in_a_closed_or_cancelled_order_can_change()
    {
        var closed = OpenOrder();
        var first = Add(closed);
        Number(first);
        Assert.True(closed.ConfirmBatch(1, Staff, T0).Succeeded);
        Assert.True(closed.Close(T0.AddHours(1)).Succeeded);

        var cancelled = OpenOrder();
        var withdrawn = Add(cancelled);
        Number(withdrawn);
        Assert.True(cancelled.Cancel(T0.AddHours(1)).Succeeded);

        foreach (var order in new[] { closed, cancelled })
        {
            Assert.Equal(SalesErrors.OrderNotOpen, order.ConfirmBatch(1, Staff, T0).Error);
            Assert.Equal(SalesErrors.OrderNotOpen, order.RejectBatch(1, Staff, "x", T0).Error);
            Assert.Equal(SalesErrors.OrderNotOpen, order.CancelBatch(1).Error);
            Assert.Equal(SalesErrors.OrderNotOpen, order.CancelPortion(1, 101).Error);
            Assert.Equal(SalesErrors.OrderNotOpen, order.Close(T0).Error);
            Assert.Equal(SalesErrors.OrderNotOpen, order.Cancel(T0).Error);
        }

        Assert.Equal(BatchStatus.Confirmed, first.TrangThai);
        Assert.Equal(PortionStatus.Confirmed, first.Portions[0].TrangThai);
        Assert.Equal(BatchStatus.Cancelled, withdrawn.TrangThai);
    }

    // ---- portions ----

    [Fact]
    public void A_portion_can_be_cancelled_before_or_after_confirmation_but_only_once()
    {
        var order = OpenOrder();
        var batch = Add(order, portions: [Portion(), Portion(), Portion()]);
        Number(batch);
        Assert.True(order.CancelPortion(1, batch.Portions[0].Id).Succeeded);
        Assert.True(order.ConfirmBatch(1, Staff, T0).Succeeded);
        Assert.True(order.CancelPortion(1, batch.Portions[1].Id).Succeeded);
        Assert.Equal(PortionStatus.Cancelled, batch.Portions[1].TrangThai);
        Assert.Equal(PortionStatus.Confirmed, batch.Portions[2].TrangThai);
        Assert.Equal(SalesErrors.PortionAlreadyCancelled, order.CancelPortion(1, batch.Portions[0].Id).Error);
        Assert.Equal(SalesErrors.PortionAlreadyCancelled, order.CancelPortion(1, batch.Portions[1].Id).Error);
        Assert.Equal(SalesErrors.PortionNotFound, order.CancelPortion(1, 999).Error);
        Assert.Equal(BatchStatus.Confirmed, batch.TrangThai);
    }

    [Fact]
    public void A_portion_of_a_rejected_batch_cannot_be_cancelled_again()
    {
        var order = OpenOrder();
        var batch = Add(order);
        Number(batch);
        Assert.True(order.RejectBatch(1, Staff, "No", T0).Succeeded);
        Assert.Equal(SalesErrors.PortionAlreadyCancelled, order.CancelPortion(1, batch.Portions[0].Id).Error);
    }

    // ---- closing and cancelling the order ----

    [Fact]
    public void An_order_with_a_batch_waiting_for_confirmation_cannot_be_closed()
    {
        var order = OpenOrder();
        Add(order);
        Assert.Equal(SalesErrors.OrderHasPendingBatches, order.Close(T0.AddHours(1)).Error);
        Assert.Equal(OrderStatus.Open, order.TrangThai);
        Assert.Null(order.DongLuc);
        Assert.True(order.ConfirmBatch(1, Staff, T0).Succeeded);
        Assert.True(order.Close(T0.AddHours(1)).Succeeded);
        Assert.Equal(OrderStatus.Closed, order.TrangThai);
        Assert.Equal(T0.AddHours(1), order.DongLuc);
    }

    [Fact]
    public void A_decided_batch_does_not_block_closing_and_an_empty_order_can_close()
    {
        var rejected = OpenOrder();
        Add(rejected);
        Assert.True(rejected.RejectBatch(1, Staff, "No", T0).Succeeded);
        Assert.True(rejected.Close(T0).Succeeded);
        Assert.True(OpenOrder().Close(T0).Succeeded);
    }

    [Fact]
    public void The_closing_time_is_never_before_the_creation_time()
    {
        var order = OpenOrder();
        Assert.True(order.Close(T0.AddMinutes(-5)).Succeeded);
        Assert.Equal(T0, order.DongLuc);
    }

    [Fact]
    public void Cancelling_an_order_withdraws_pending_batches_but_refuses_while_food_is_confirmed()
    {
        var withPending = OpenOrder();
        var pending = Add(withPending);
        var rejected = Add(withPending);
        Assert.True(withPending.RejectBatch(2, Staff, "No", T0).Succeeded);
        Assert.True(withPending.Cancel(T0.AddMinutes(1)).Succeeded);
        Assert.Equal(OrderStatus.Cancelled, withPending.TrangThai);
        Assert.Equal(T0.AddMinutes(1), withPending.DongLuc);
        Assert.Equal(BatchStatus.Cancelled, pending.TrangThai);
        Assert.Equal(BatchStatus.Rejected, rejected.TrangThai);
        Assert.All(pending.Portions, p => Assert.Equal(PortionStatus.Cancelled, p.TrangThai));

        var withFood = OpenOrder();
        var confirmed = Add(withFood, portions: [Portion(), Portion()]);
        Number(confirmed);
        Assert.True(withFood.ConfirmBatch(1, Staff, T0).Succeeded);
        Assert.Equal(SalesErrors.OrderHasActivePortions, withFood.Cancel(T0).Error);
        Assert.Equal(OrderStatus.Open, withFood.TrangThai);
        Assert.Equal(BatchStatus.Confirmed, confirmed.TrangThai);

        Assert.True(withFood.CancelPortion(1, confirmed.Portions[0].Id).Succeeded);
        Assert.Equal(SalesErrors.OrderHasActivePortions, withFood.Cancel(T0).Error);
        Assert.True(withFood.CancelPortion(1, confirmed.Portions[1].Id).Succeeded);
        Assert.True(withFood.Cancel(T0).Succeeded);
    }

    [Fact]
    public void A_closed_order_cannot_become_cancelled_and_the_reverse()
    {
        var closed = OpenOrder();
        Assert.True(closed.Close(T0).Succeeded);
        Assert.Equal(SalesErrors.OrderNotOpen, closed.Cancel(T0).Error);
        Assert.Equal(OrderStatus.Closed, closed.TrangThai);
        var cancelled = OpenOrder();
        Assert.True(cancelled.Cancel(T0).Succeeded);
        Assert.Equal(SalesErrors.OrderNotOpen, cancelled.Close(T0).Error);
        Assert.Equal(OrderStatus.Cancelled, cancelled.TrangThai);
    }

    [Fact]
    public void Portions_keep_the_names_and_prices_given_at_order_time()
    {
        var order = OpenOrder();
        var batch = order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(),
            [new NewPortion(11, 22, " Milk tea ", " Large ", 45000, Stations.Bar, " less ice ")], T0).Value!;
        var portion = batch.Portions.Single();
        Assert.Equal((11, 22), (portion.MonId, portion.BienTheId));
        Assert.Equal(("Milk tea", "Large", 45000, Stations.Bar, "less ice"),
            (portion.TenMonChot, portion.TenBienTheChot, portion.GiaMonChot, portion.TramChot, portion.GhiChu));
    }

    // ---- helpers ----

    private static SalesResult<DonHang> Order(
        string? type, long? session, string? label = null, string? customer = null, string? code = "DH-1") =>
        DonHang.Create(code, type, Day, session, label, customer, Staff, T0);

    private static DonHang OpenOrder() => Order(OrderTypes.DineIn, session: 9).Value!;

    private static NewPortion Portion() =>
        new(1, 2, "Pho", "Regular", 60000, Stations.Kitchen, null);

    private static DotGoiMon Add(DonHang order, IReadOnlyList<NewPortion>? portions = null) =>
        order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), portions ?? [Portion()], T0).Value!;

    /// <summary>Gives the portions ids as the database would, so they can be addressed.</summary>
    private static void Number(DotGoiMon batch)
    {
        var next = batch.SoDot * 100L;
        foreach (var portion in batch.Portions) portion.Id = ++next;
    }
}
