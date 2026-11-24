using Restaurant.Modules.Catalog.Contracts;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.SalesDay.Contracts;
using Restaurant.Modules.Seating.Contracts;

namespace Restaurant.Modules.Sales.Application;

/// <summary>
/// Create an order, add a batch, withdraw a batch. Every write follows the same order (ADR-01): open the transaction,
/// pass the business day gate (shared day lock), then lock and change the order. It talks to the other modules only
/// through their Contracts, so each of them can be replaced by a fake in tests.
/// </summary>
public sealed class OrderService(
    IOrderStore store, ISessionLookup sessions, ISalesDayGate dayGate, IMenuPricing menu, TimeProvider clock) : IOrderCommands
{
    public async Task<ServiceResult<OrderView>> CreateOrderAsync(CreateOrderCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var dineIn = command.Type == OrderTypes.DineIn;
        if (!dineIn && command.Type != OrderTypes.Takeaway) return Fail<OrderView>(SalesErrors.InputInvalid);
        if (dineIn && command.SessionId is null) return Fail<OrderView>(SalesErrors.OrderSessionRequired);
        if (!dineIn && command.SessionId is not null) return Fail<OrderView>(SalesErrors.OrderSessionForbidden);
        // The day of a dine-in order is the day of its session; a caller-supplied day could contradict it.
        if (dineIn ? command.BusinessDayId is not null : command.BusinessDayId is null || command.BusinessDayId == Guid.Empty)
            return Fail<OrderView>(SalesErrors.InputInvalid);

        await using var tx = await store.BeginAsync(ct);

        var dayId = command.BusinessDayId.GetValueOrDefault();
        string? tableLabel = null;
        if (dineIn)
        {
            // Read-only and lock-free: "closed" is final, and the session's day never changes.
            var check = await sessions.CheckOpenAsync(command.SessionId!.Value, ct);
            if (check.Session is null) return Fail<OrderView>(check.ErrorCode ?? SalesErrors.InputInvalid);
            dayId = check.Session.BusinessDayId;
            tableLabel = check.Session.TableLabel;
        }

        var gate = await dayGate.RequireOpenAsync(dayId, ct);
        if (!gate.IsOpen) return Fail<OrderView>(gate.ErrorCode ?? SalesDayErrorCodes.DayNotOpen);

        if (dineIn && await tx.FindOpenOrderIdBySessionAsync(command.SessionId!.Value, ct) is not null)
            return Fail<OrderView>(SalesErrors.OrderSessionHasOpenOrder);

        var created = DonHang.Create(
            await tx.NextOrderCodeAsync(ct), command.Type, dayId, command.SessionId, tableLabel,
            command.CustomerName, command.Actor, clock.GetUtcNow().UtcDateTime);
        if (!created.Succeeded) return Fail<OrderView>(created.Error!);

        var order = created.Value!;
        tx.Add(order);
        return await SaveAndCommitAsync(tx, () => ServiceResult<OrderView>.Ok(ToView(order)), ct);
    }

    public async Task<ServiceResult<BatchView>> AddBatchAsync(AddBatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.OrderId <= 0 || command.RequestKey == Guid.Empty) return Fail<BatchView>(SalesErrors.InputInvalid);
        if (command.Lines is null || command.Lines.Count == 0) return Fail<BatchView>(SalesErrors.BatchEmpty);
        if (command.Lines.Any(x => x is null)) return Fail<BatchView>(SalesErrors.InputInvalid);

        await using var tx = await store.BeginAsync(ct);

        var gated = await GateOrderDayAsync(tx, command.OrderId, ct);
        if (gated is not null) return Fail<BatchView>(gated);
        var order = await tx.LockOrderAsync(command.OrderId, ct);
        if (order is null) return Fail<BatchView>(SalesErrors.OrderNotFound);

        // A retried request must not add a second batch, even if the order has moved on since.
        var earlier = order.Dots.FirstOrDefault(x => x.KhoaYeuCau == command.RequestKey);
        if (earlier is not null) return ServiceResult<BatchView>.Ok(ToView(order, earlier), replayed: true);
        if (order.TrangThai != OrderStatus.Open) return Fail<BatchView>(SalesErrors.OrderNotOpen);

        // Priced against the order's own business day, so the sold-out flags of that day apply.
        var priced = await menu.PriceAsync(new MenuPricingRequest(order.NgayBanId,
            [.. command.Lines.Select((line, index) =>
                new MenuLineSelection(index + 1, line.MenuItemId, line.VariantId, line.OptionIds ?? []))]), ct);
        if (!priced.IsValid)
            return Fail<BatchView>(SalesErrors.BatchLineInvalid, [.. priced.Errors.Select(e => new LineError(e.LineNo, e.Code))]);
        var byLine = priced.Lines.GroupBy(x => x.LineNo).ToDictionary(x => x.Key, x => x.First());
        if (priced.Lines.Count != command.Lines.Count || Enumerable.Range(1, command.Lines.Count).Any(n => !byLine.ContainsKey(n)))
            return Fail<BatchView>(SalesErrors.PricingInconsistent);
        // The snapshot stored on the portion (and later charged) must be of what was asked for, line by line.
        if (command.Lines.Select((line, index) => (line, priced: byLine[index + 1])).Any(x => !Answers(x.line, x.priced)))
            return Fail<BatchView>(SalesErrors.PricingInconsistent);

        var portions = command.Lines.Select((line, index) =>
        {
            var p = byLine[index + 1];
            return new NewPortion(p.MenuItemId, p.VariantId, p.MenuItemName, p.VariantName, p.VariantPrice, p.Station, line.Note,
                [.. p.Options.Select(o => new NewOption(o.OptionId, o.GroupId, o.GroupName, o.GroupKind, o.OptionName, o.Surcharge))]);
        }).ToList();

        var added = order.AddBatch(command.Source, command.GuestGrantId, command.StaffId, command.RequestKey, portions,
            clock.GetUtcNow().UtcDateTime);
        if (!added.Succeeded) return Fail<BatchView>(added.Error!);

        var batch = added.Value!;
        return await SaveAndCommitAsync(tx, () => ServiceResult<BatchView>.Ok(ToView(order, batch)), ct);
    }

    /// <summary>True when the priced line is for the same dish, variant and options as the requested one.</summary>
    private static bool Answers(BatchLine requested, PricedMenuLine priced) =>
        priced.MenuItemId == requested.MenuItemId && priced.VariantId == requested.VariantId &&
        priced.Options.Select(o => o.OptionId).Order().SequenceEqual((requested.OptionIds ?? []).Order());

    private static bool Answers(ChiTietDon portion, PricedMenuLine? priced) =>
        priced is not null && priced.MenuItemId == portion.MonId && priced.VariantId == portion.BienTheId &&
        priced.Options.Select(x => x.OptionId).Order().SequenceEqual(portion.Options.Select(x => x.TuyChonId).Order());

    /// <summary>Withdraws a batch that has not been decided yet (confirmed batches are reduced portion by portion).</summary>
    public async Task<ServiceResult<BatchView>> CancelBatchAsync(CancelBatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.OrderId <= 0 || command.BatchNumber <= 0) return Fail<BatchView>(SalesErrors.InputInvalid);

        await using var tx = await store.BeginAsync(ct);

        var gated = await GateOrderDayAsync(tx, command.OrderId, ct);
        if (gated is not null) return Fail<BatchView>(gated);
        var order = await tx.LockOrderAsync(command.OrderId, ct);
        if (order is null) return Fail<BatchView>(SalesErrors.OrderNotFound);

        var cancelled = order.CancelBatch(command.BatchNumber);
        if (!cancelled.Succeeded) return Fail<BatchView>(cancelled.Error!);

        var batch = order.Dots.Single(x => x.SoDot == command.BatchNumber);
        return await SaveAndCommitAsync(tx, () => ServiceResult<BatchView>.Ok(ToView(order, batch)), ct);
    }

    public async Task<ServiceResult<BatchView>> ConfirmBatchAsync(ConfirmBatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.OrderId <= 0 || command.BatchNumber <= 0 || command.Actor == Guid.Empty)
            return Fail<BatchView>(SalesErrors.InputInvalid);

        await using var tx = await store.BeginAsync(ct);
        var gated = await GateOrderDayAsync(tx, command.OrderId, ct);
        if (gated is not null) return Fail<BatchView>(gated);
        var order = await tx.LockOrderAsync(command.OrderId, ct);
        if (order is null) return Fail<BatchView>(SalesErrors.OrderNotFound);
        var batch = order.Dots.SingleOrDefault(x => x.SoDot == command.BatchNumber);
        if (batch is null) return Fail<BatchView>(SalesErrors.BatchNotFound);
        if (batch.TrangThai != BatchStatus.Pending) return Fail<BatchView>(SalesErrors.BatchNotPending);

        var priced = await menu.PriceAsync(new MenuPricingRequest(order.NgayBanId,
            [.. batch.Portions.Select((portion, index) => new MenuLineSelection(index + 1, portion.MonId, portion.BienTheId,
                [.. portion.Options.Select(option => option.TuyChonId)]))]), ct);
        if (!priced.IsValid)
            return Fail<BatchView>(SalesErrors.BatchLineInvalid, [.. priced.Errors.Select(e => new LineError(e.LineNo, e.Code))]);
        var byLine = priced.Lines.GroupBy(x => x.LineNo).ToDictionary(x => x.Key, x => x.First());
        if (priced.Lines.Count != batch.Portions.Count || batch.Portions.Select((portion, index) =>
                byLine.TryGetValue(index + 1, out var answer) && Answers(portion, answer)).Any(valid => !valid))
            return Fail<BatchView>(SalesErrors.PricingInconsistent);
        if (batch.Portions.Select((portion, index) => portion.GiaMonChot + portion.Options.Sum(x => x.GiaCongChot))
            .Zip(priced.Lines.OrderBy(x => x.LineNo), (stored, current) => stored == current.UnitPrice).Any(equal => !equal))
            return Fail<BatchView>(SalesErrors.PricingChanged);

        var confirmed = order.ConfirmBatch(command.BatchNumber, command.Actor, clock.GetUtcNow().UtcDateTime);
        if (!confirmed.Succeeded) return Fail<BatchView>(confirmed.Error!);
        return await SaveAndCommitAsync(tx, () => ServiceResult<BatchView>.Ok(ToView(order, batch)), ct);
    }

    public async Task<ServiceResult<BatchView>> RejectBatchAsync(RejectBatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.OrderId <= 0 || command.BatchNumber <= 0 || command.Actor == Guid.Empty)
            return Fail<BatchView>(SalesErrors.InputInvalid);

        await using var tx = await store.BeginAsync(ct);
        var gated = await GateOrderDayAsync(tx, command.OrderId, ct);
        if (gated is not null) return Fail<BatchView>(gated);
        var order = await tx.LockOrderAsync(command.OrderId, ct);
        if (order is null) return Fail<BatchView>(SalesErrors.OrderNotFound);
        var rejected = order.RejectBatch(command.BatchNumber, command.Actor, command.Reason, clock.GetUtcNow().UtcDateTime);
        if (!rejected.Succeeded) return Fail<BatchView>(rejected.Error!);
        var batch = order.Dots.Single(x => x.SoDot == command.BatchNumber);
        return await SaveAndCommitAsync(tx, () => ServiceResult<BatchView>.Ok(ToView(order, batch)), ct);
    }

    /// <summary>
    /// Finds the order's day without a lock, then takes the day lock through the gate (day before order).
    /// Returns an error code, or null when the day is open.
    /// </summary>
    private async Task<string?> GateOrderDayAsync(IOrderTransaction tx, long orderId, CancellationToken ct)
    {
        var dayId = await tx.FindBusinessDayAsync(orderId, ct);
        if (dayId is null) return SalesErrors.OrderNotFound;
        var gate = await dayGate.RequireOpenAsync(dayId.Value, ct);
        return gate.IsOpen ? null : gate.ErrorCode ?? SalesDayErrorCodes.DayNotOpen;
    }

    private static async Task<ServiceResult<T>> SaveAndCommitAsync<T>(
        IOrderTransaction tx, Func<ServiceResult<T>> success, CancellationToken ct) where T : class
    {
        try
        {
            await tx.SaveAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (StoreConflictException conflict)
        {
            return Fail<T>(conflict.Reason == StoreConflict.OpenOrderForSession
                ? SalesErrors.OrderSessionHasOpenOrder : SalesErrors.OrderChanged);
        }

        // Built after the save so database-generated ids are filled in.
        return success();
    }

    private static ServiceResult<T> Fail<T>(string error, IReadOnlyList<LineError>? lines = null) where T : class =>
        ServiceResult<T>.Fail(error, lines);

    private static OrderView ToView(DonHang order) => new(
        order.Id, order.MaDon, order.Loai, order.NgayBanId, order.PhienSuDungId, order.NhanBanLucTao, order.TenKhach,
        order.TrangThai);

    private static BatchView ToView(DonHang order, DotGoiMon batch)
    {
        var portions = batch.Portions.Select(p => new PortionView(
            p.Id, p.TenMonChot, p.TenBienTheChot, p.TramChot, p.GhiChu, p.GiaMonChot + p.Options.Sum(o => o.GiaCongChot),
            [.. p.Options.Select(o => o.TenTuyChonChot)], p.TrangThai)).ToList();
        return new BatchView(order.Id, batch.Id, batch.SoDot, batch.TrangThai, portions,
            portions.Where(x => x.Status != PortionStatus.Cancelled).Sum(x => x.UnitPrice));
    }
}
