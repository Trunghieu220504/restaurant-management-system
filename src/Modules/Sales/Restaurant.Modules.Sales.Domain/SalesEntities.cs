using System.Text.RegularExpressions;

namespace Restaurant.Modules.Sales.Domain;

/// <summary>One portion's data as it is recorded at order time (names and prices are snapshots).</summary>
public sealed record NewPortion(
    Guid DishId, Guid VariantId, string DishName, string VariantName, long Price,
    string Station, string? Note, IReadOnlyList<NewOption>? Options = null);

public sealed record NewOption(
    Guid OptionId, Guid GroupId, string GroupName, string GroupKind, string OptionName, long Surcharge);

/// <summary>
/// Order aggregate. Batches, portions and options are only changed through this root, so the state machines and
/// the rules "no batch on a closed order" and "close only when nothing waits for confirmation" cannot be bypassed.
/// </summary>
public sealed partial class DonHang
{
    private readonly List<DotGoiMon> dots = [];

    private DonHang()
    {
    }

    public long Id { get; set; }
    public string MaDon { get; private set; } = "";
    public Guid NgayBanId { get; private set; }
    public string Loai { get; private set; } = "";
    public long? PhienSuDungId { get; private set; }
    public string? NhanBanLucTao { get; private set; }
    public string? TenKhach { get; private set; }
    public string TrangThai { get; private set; } = OrderStatus.Open;
    public DateTime? DongLuc { get; private set; }
    public DateTime TaoLuc { get; private set; }
    public Guid? NguoiTaoId { get; private set; }
    public byte[] Version { get; private set; } = [];

    /// <summary>
    /// Bumped by every change under this order (batches, portions, close, cancel). Changing a child row alone does not
    /// touch the order row, so without this counter the row version could not detect "add a batch" racing "close".
    /// </summary>
    public int SoLanCapNhat { get; private set; }

    public IReadOnlyList<DotGoiMon> Dots => dots;

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$")]
    private static partial Regex CodePattern();

    public static SalesResult<DonHang> Create(
        string? code, string? type, Guid businessDayId, long? sessionId, string? tableLabel,
        string? customerName, Guid? createdBy, DateTime now)
    {
        if (code is null || !CodePattern().IsMatch(code) || businessDayId == Guid.Empty || createdBy == Guid.Empty ||
            !Text.Optional(tableLabel, 150, out var label) || !Text.Optional(customerName, 150, out var customer))
            return SalesResult<DonHang>.Fail(SalesErrors.InputInvalid);
        if (type == OrderTypes.DineIn)
        {
            if (sessionId is null) return SalesResult<DonHang>.Fail(SalesErrors.OrderSessionRequired);
            if (sessionId <= 0) return SalesResult<DonHang>.Fail(SalesErrors.InputInvalid);
        }
        else if (type == OrderTypes.Takeaway)
        {
            if (sessionId is not null || label is not null)
                return SalesResult<DonHang>.Fail(SalesErrors.OrderSessionForbidden);
        }
        else
        {
            return SalesResult<DonHang>.Fail(SalesErrors.InputInvalid);
        }

        return SalesResult<DonHang>.Ok(new DonHang
        {
            MaDon = code,
            NgayBanId = businessDayId,
            Loai = type,
            PhienSuDungId = sessionId,
            NhanBanLucTao = label,
            TenKhach = customer,
            TrangThai = OrderStatus.Open,
            TaoLuc = now,
            NguoiTaoId = createdBy
        });
    }

    /// <summary>Adds the next batch (additional ordering is allowed until the order closes).</summary>
    public SalesResult<DotGoiMon> AddBatch(
        string? source, Guid? guestGrantId, Guid? staffId, Guid requestKey,
        IReadOnlyList<NewPortion>? portions, DateTime now)
    {
        if (TrangThai != OrderStatus.Open) return SalesResult<DotGoiMon>.Fail(SalesErrors.OrderNotOpen);
        if (requestKey == Guid.Empty) return SalesResult<DotGoiMon>.Fail(SalesErrors.InputInvalid);
        if (source == BatchSource.Guest)
        {
            if (Loai != OrderTypes.DineIn) return SalesResult<DotGoiMon>.Fail(SalesErrors.BatchGuestNeedsSession);
            if (guestGrantId is null || guestGrantId == Guid.Empty || staffId is not null)
                return SalesResult<DotGoiMon>.Fail(SalesErrors.InputInvalid);
        }
        else if (source == BatchSource.Staff)
        {
            if (staffId is null || staffId == Guid.Empty || guestGrantId is not null)
                return SalesResult<DotGoiMon>.Fail(SalesErrors.InputInvalid);
        }
        else
        {
            return SalesResult<DotGoiMon>.Fail(SalesErrors.InputInvalid);
        }

        if (portions is null || portions.Count == 0) return SalesResult<DotGoiMon>.Fail(SalesErrors.BatchEmpty);
        var built = new List<ChiTietDon>(portions.Count);
        foreach (var portion in portions)
        {
            var made = ChiTietDon.Create(portion);
            if (!made.Succeeded) return SalesResult<DotGoiMon>.Fail(made.Error!);
            built.Add(made.Value!);
        }

        var batch = new DotGoiMon(dots.Count == 0 ? 1 : dots.Max(x => x.SoDot) + 1, source, guestGrantId, staffId,
            requestKey, now, built);
        dots.Add(batch);
        Touch();
        return SalesResult<DotGoiMon>.Ok(batch);
    }

    /// <summary>Confirms a pending batch together with all of its portions that are still pending.</summary>
    public SalesResult ConfirmBatch(int batchNumber, Guid actor, DateTime now)
    {
        var found = Decide(batchNumber, actor, BatchStatus.Confirmed, out var batch);
        if (!found.Succeeded) return found;
        if (!batch!.Portions.Any(x => x.TrangThai == PortionStatus.Pending))
            return SalesResult.Fail(SalesErrors.BatchEmpty);
        batch.Move(BatchStatus.Confirmed);
        batch.Decide(actor, now, null);
        foreach (var portion in batch.Portions.Where(x => x.TrangThai == PortionStatus.Pending))
            portion.Move(PortionStatus.Confirmed);
        Touch();
        return SalesResult.Ok();
    }

    public SalesResult RejectBatch(int batchNumber, Guid actor, string? reason, DateTime now)
    {
        if (!Text.Required(reason, 1000, out var clean)) return SalesResult.Fail(SalesErrors.InputInvalid);
        var found = Decide(batchNumber, actor, BatchStatus.Rejected, out var batch);
        if (!found.Succeeded) return found;
        batch!.Move(BatchStatus.Rejected);
        batch.Decide(actor, now, clean);
        batch.CancelOpenPortions();
        Touch();
        return SalesResult.Ok();
    }

    /// <summary>Withdraws a pending batch before any decision (for example the guest changed their mind).</summary>
    public SalesResult CancelBatch(int batchNumber)
    {
        if (TrangThai != OrderStatus.Open) return SalesResult.Fail(SalesErrors.OrderNotOpen);
        var batch = dots.SingleOrDefault(x => x.SoDot == batchNumber);
        if (batch is null) return SalesResult.Fail(SalesErrors.BatchNotFound);
        if (!SalesStateMachines.Batch.CanMove(batch.TrangThai, BatchStatus.Cancelled))
            return SalesResult.Fail(SalesErrors.BatchNotPending);
        batch.Move(BatchStatus.Cancelled);
        batch.CancelOpenPortions();
        Touch();
        return SalesResult.Ok();
    }

    /// <summary>
    /// Cancels one portion of a pending or confirmed batch while the order is open. A pending batch whose last portion
    /// is cancelled is withdrawn too, so an empty batch never keeps waiting for a decision (and never blocks closing).
    /// </summary>
    public SalesResult CancelPortion(int batchNumber, long portionId)
    {
        if (TrangThai != OrderStatus.Open) return SalesResult.Fail(SalesErrors.OrderNotOpen);
        var batch = dots.SingleOrDefault(x => x.SoDot == batchNumber);
        if (batch is null) return SalesResult.Fail(SalesErrors.BatchNotFound);
        var portion = batch.Portions.SingleOrDefault(x => x.Id == portionId);
        if (portion is null) return SalesResult.Fail(SalesErrors.PortionNotFound);
        if (!SalesStateMachines.Portion.CanMove(portion.TrangThai, PortionStatus.Cancelled))
            return SalesResult.Fail(SalesErrors.PortionAlreadyCancelled);
        portion.Move(PortionStatus.Cancelled);
        if (batch.TrangThai == BatchStatus.Pending && batch.Portions.All(x => x.TrangThai == PortionStatus.Cancelled))
            batch.Move(BatchStatus.Cancelled);
        Touch();
        return SalesResult.Ok();
    }

    /// <summary>Closes the order. Payment is checked by Billing before this is called.</summary>
    public SalesResult Close(DateTime now)
    {
        if (!SalesStateMachines.Order.CanMove(TrangThai, OrderStatus.Closed))
            return SalesResult.Fail(SalesErrors.OrderNotOpen);
        if (dots.Any(x => x.TrangThai == BatchStatus.Pending))
            return SalesResult.Fail(SalesErrors.OrderHasPendingBatches);
        Finish(OrderStatus.Closed, now);
        return SalesResult.Ok();
    }

    /// <summary>Cancels an order that has nothing left to cook or charge. Pending batches are withdrawn with it.</summary>
    public SalesResult Cancel(DateTime now)
    {
        if (!SalesStateMachines.Order.CanMove(TrangThai, OrderStatus.Cancelled))
            return SalesResult.Fail(SalesErrors.OrderNotOpen);
        if (dots.Any(x => x.TrangThai == BatchStatus.Confirmed &&
                          x.Portions.Any(p => p.TrangThai != PortionStatus.Cancelled)))
            return SalesResult.Fail(SalesErrors.OrderHasActivePortions);
        foreach (var batch in dots.Where(x => x.TrangThai == BatchStatus.Pending))
        {
            batch.Move(BatchStatus.Cancelled);
            batch.CancelOpenPortions();
        }

        Finish(OrderStatus.Cancelled, now);
        return SalesResult.Ok();
    }

    private SalesResult Decide(int batchNumber, Guid actor, string decision, out DotGoiMon? batch)
    {
        batch = null;
        if (TrangThai != OrderStatus.Open) return SalesResult.Fail(SalesErrors.OrderNotOpen);
        if (actor == Guid.Empty) return SalesResult.Fail(SalesErrors.InputInvalid);
        batch = dots.SingleOrDefault(x => x.SoDot == batchNumber);
        if (batch is null) return SalesResult.Fail(SalesErrors.BatchNotFound);
        return SalesStateMachines.Batch.CanMove(batch.TrangThai, decision)
            ? SalesResult.Ok() : SalesResult.Fail(SalesErrors.BatchNotPending);
    }

    private void Touch() => SoLanCapNhat++;

    private void Finish(string status, DateTime now)
    {
        Touch();
        TrangThai = status;
        DongLuc = now > TaoLuc ? now : TaoLuc;
    }
}

public sealed class DotGoiMon
{
    private readonly List<ChiTietDon> portions = [];

    private DotGoiMon()
    {
    }

    internal DotGoiMon(int number, string source, Guid? guestGrantId, Guid? staffId, Guid requestKey,
        DateTime now, IEnumerable<ChiTietDon> items)
    {
        SoDot = number;
        Nguon = source;
        QuyenTruyCapKhachId = guestGrantId;
        NguoiTaoId = staffId;
        KhoaYeuCau = requestKey;
        TaoLuc = now;
        portions.AddRange(items);
    }

    public long Id { get; set; }
    public long DonHangId { get; private set; }
    public int SoDot { get; private set; }
    public string Nguon { get; private set; } = "";
    public Guid? QuyenTruyCapKhachId { get; private set; }
    public Guid? NguoiTaoId { get; private set; }
    public string TrangThai { get; private set; } = BatchStatus.Pending;
    public DateTime TaoLuc { get; private set; }
    public DateTime? XacNhanLuc { get; private set; }
    public Guid? NguoiXacNhanId { get; private set; }
    public string? LyDoTuChoi { get; private set; }
    public Guid KhoaYeuCau { get; private set; }
    public byte[] Version { get; private set; } = [];
    public IReadOnlyList<ChiTietDon> Portions => portions;

    internal void Move(string to)
    {
        if (!SalesStateMachines.Batch.CanMove(TrangThai, to))
            throw new InvalidOperationException($"Batch cannot move from {TrangThai} to {to}.");
        TrangThai = to;
    }

    /// <summary>Records who decided (confirmed or rejected) and when.</summary>
    internal void Decide(Guid actor, DateTime now, string? reason)
    {
        NguoiXacNhanId = actor;
        XacNhanLuc = now;
        LyDoTuChoi = reason;
    }

    internal void CancelOpenPortions()
    {
        foreach (var portion in portions.Where(x => x.TrangThai != PortionStatus.Cancelled))
            portion.Move(PortionStatus.Cancelled);
    }
}

/// <summary>One row is one ordered portion. Names and prices are copies, so later menu edits never change history.</summary>
public sealed class ChiTietDon
{
    private readonly List<TuyChonDaDat> options = [];

    private ChiTietDon()
    {
    }

    public long Id { get; set; }
    public long DotGoiMonId { get; private set; }
    public Guid MonId { get; private set; }
    public Guid BienTheId { get; private set; }
    public string TenMonChot { get; private set; } = "";
    public string TenBienTheChot { get; private set; } = "";
    public long GiaMonChot { get; private set; }
    public string TramChot { get; private set; } = "";
    public string? GhiChu { get; private set; }
    public string TrangThai { get; private set; } = PortionStatus.Pending;
    public byte[] Version { get; private set; } = [];
    public IReadOnlyList<TuyChonDaDat> Options => options;

    internal static SalesResult<ChiTietDon> Create(NewPortion? input)
    {
        if (input is null || input.DishId == Guid.Empty || input.VariantId == Guid.Empty || input.Price < 0 ||
            !Text.Required(input.DishName, 150, out var dish) ||
            !Text.Required(input.VariantName, 150, out var variant) ||
            !Text.Optional(input.Note, 2000, out var note) || !Stations.All.Contains(input.Station))
            return SalesResult<ChiTietDon>.Fail(SalesErrors.InputInvalid);
        var portion = new ChiTietDon
        {
            MonId = input.DishId,
            BienTheId = input.VariantId,
            TenMonChot = dish!,
            TenBienTheChot = variant!,
            GiaMonChot = input.Price,
            TramChot = input.Station,
            GhiChu = note
        };
        foreach (var option in input.Options ?? [])
        {
            if (option is null || option.OptionId == Guid.Empty || option.GroupId == Guid.Empty || option.Surcharge < 0 ||
                !OptionGroupKinds.All.Contains(option.GroupKind) ||
                !Text.Required(option.GroupName, 150, out var group) ||
                !Text.Required(option.OptionName, 150, out var name) ||
                portion.options.Any(x => x.TuyChonId == option.OptionId))
                return SalesResult<ChiTietDon>.Fail(SalesErrors.InputInvalid);
            portion.options.Add(new TuyChonDaDat(
                option.OptionId, option.GroupId, group!, option.GroupKind, name!, option.Surcharge));
        }

        return SalesResult<ChiTietDon>.Ok(portion);
    }

    internal void Move(string to)
    {
        if (!SalesStateMachines.Portion.CanMove(TrangThai, to))
            throw new InvalidOperationException($"Portion cannot move from {TrangThai} to {to}.");
        TrangThai = to;
    }
}

public sealed class TuyChonDaDat
{
    private TuyChonDaDat()
    {
    }

    internal TuyChonDaDat(Guid optionId, Guid groupId, string groupName, string groupKind, string optionName, long surcharge)
    {
        TuyChonId = optionId;
        NhomTuyChonId = groupId;
        TenNhomChot = groupName;
        LoaiNhomChot = groupKind;
        TenTuyChonChot = optionName;
        GiaCongChot = surcharge;
    }

    public long ChiTietDonId { get; private set; }
    public Guid TuyChonId { get; private set; }
    public Guid NhomTuyChonId { get; private set; }
    public string TenNhomChot { get; private set; } = "";
    public string LoaiNhomChot { get; private set; } = "";
    public string TenTuyChonChot { get; private set; } = "";
    public long GiaCongChot { get; private set; }
}

internal static class Text
{
    public static bool Required(string? value, int max, out string? clean)
    {
        clean = value?.Trim();
        return !string.IsNullOrEmpty(clean) && clean.Length <= max;
    }

    /// <summary>Blank becomes null; a value that is too long is invalid.</summary>
    public static bool Optional(string? value, int max, out string? clean)
    {
        clean = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return clean is null || clean.Length <= max;
    }
}
