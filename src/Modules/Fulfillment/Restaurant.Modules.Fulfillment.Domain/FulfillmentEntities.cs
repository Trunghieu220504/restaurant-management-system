namespace Restaurant.Modules.Fulfillment.Domain;

/// <summary>
/// One preparation attempt of one ordered portion. <c>ChiTietDonId</c> is a logical reference to Sales (no cross-module
/// key). A remake is a new ticket with the next <c>Lan</c> that points back at the attempt it replaces and at the approved
/// incident that allowed it; the very first ticket has neither. The circular keys use NO ACTION, so the order of inserts is
/// first ticket, then incident, then remake.
/// </summary>
public sealed class PhieuCheBien
{
    private PhieuCheBien()
    {
    }

    public long Id { get; set; }
    public long ChiTietDonId { get; private set; }
    public int Lan { get; private set; }
    public string TramChot { get; private set; } = "";
    public long? PhieuGocId { get; private set; }
    public long? SuCoNguonId { get; private set; }
    public string TrangThai { get; private set; } = TicketStatus.New;

    /// <summary>Creation time. Not in the revised database design; added so a station can order its queue by arrival.</summary>
    public DateTime TaoLuc { get; private set; }
    public Guid? NguoiBatDauId { get; private set; }
    public DateTime? BatDauLuc { get; private set; }
    public DateTime? HoanThanhLuc { get; private set; }
    public Guid? NguoiHoanThanhId { get; private set; }
    public DateTime? GiaoLuc { get; private set; }
    public Guid? NguoiGiaoId { get; private set; }
    public byte[] Version { get; private set; } = [];

    /// <summary>The first ticket of a confirmed portion: attempt 1, no original ticket and no incident.</summary>
    public static FulfillmentResult<PhieuCheBien> CreateInitial(long portionId, string? station, DateTime now)
    {
        if (portionId <= 0 || station is null || !Stations.All.Contains(station))
            return FulfillmentResult<PhieuCheBien>.Fail(FulfillmentErrors.InputInvalid);
        return FulfillmentResult<PhieuCheBien>.Ok(new PhieuCheBien
        {
            ChiTietDonId = portionId,
            Lan = 1,
            TramChot = station,
            TrangThai = TicketStatus.New,
            TaoLuc = now
        });
    }

    public FulfillmentResult<PhieuCheBien> Start(Guid actor, DateTime now) =>
        Transition(TicketStatus.InProgress, actor, now);

    public FulfillmentResult<PhieuCheBien> MarkReady(Guid actor, DateTime now) =>
        Transition(TicketStatus.Ready, actor, now);

    public FulfillmentResult<PhieuCheBien> Deliver(Guid actor, DateTime now) =>
        Transition(TicketStatus.Served, actor, now);

    public FulfillmentResult<PhieuCheBien> Cancel(Guid actor, DateTime now) =>
        Transition(TicketStatus.Cancelled, actor, now);

    private FulfillmentResult<PhieuCheBien> Transition(string target, Guid actor, DateTime now)
    {
        if (actor == Guid.Empty || now.Kind != DateTimeKind.Utc)
            return FulfillmentResult<PhieuCheBien>.Fail(FulfillmentErrors.InputInvalid);

        var allowed = (TrangThai, target) switch
        {
            (TicketStatus.New, TicketStatus.InProgress) => true,
            (TicketStatus.InProgress, TicketStatus.Ready) => true,
            (TicketStatus.Ready, TicketStatus.Served) => true,
            (TicketStatus.New, TicketStatus.Cancelled) => true,
            (TicketStatus.InProgress, TicketStatus.Cancelled) => true,
            (TicketStatus.Ready, TicketStatus.Cancelled) => true,
            _ => false
        };
        if (!allowed)
            return FulfillmentResult<PhieuCheBien>.Fail(
                TrangThai is TicketStatus.Served or TicketStatus.Cancelled
                    ? FulfillmentErrors.AlreadyProcessed
                    : FulfillmentErrors.InvalidTransition);

        TrangThai = target;
        switch (target)
        {
            case TicketStatus.InProgress:
                NguoiBatDauId = actor;
                BatDauLuc = now;
                break;
            case TicketStatus.Ready:
                NguoiHoanThanhId = actor;
                HoanThanhLuc = now;
                break;
            case TicketStatus.Served:
                NguoiGiaoId = actor;
                GiaoLuc = now;
                break;
        }
        return FulfillmentResult<PhieuCheBien>.Ok(this);
    }
}

/// <summary>A reported problem with one ticket (spoiled, wrong dish, ...). Approval and its effects arrive with HIE-4.x.</summary>
public sealed class SuCoMon
{
    private SuCoMon()
    {
    }

    public long Id { get; set; }
    public long PhieuCheBienId { get; private set; }
    public string LyDo { get; private set; } = "";
    public DateTime BaoLuc { get; private set; }
    public Guid NguoiBaoId { get; private set; }
    public string TrangThai { get; private set; } = IncidentStatus.Pending;
    public string? QuyetDinh { get; private set; }
    public Guid? NguoiDuyetId { get; private set; }
    public DateTime? DuyetLuc { get; private set; }
    public string? LyDoDuyet { get; private set; }

    /// <summary>Idempotency key of the report request: a retried request cannot file the same incident twice.</summary>
    public Guid KhoaYeuCau { get; private set; }
    public byte[] Version { get; private set; } = [];

    public static FulfillmentResult<SuCoMon> Report(long ticketId, string? reason, Guid reporter, Guid requestKey, DateTime now)
    {
        var clean = reason?.Trim();
        if (ticketId <= 0 || string.IsNullOrEmpty(clean) || clean.Length > 1000 ||
            reporter == Guid.Empty || requestKey == Guid.Empty)
            return FulfillmentResult<SuCoMon>.Fail(FulfillmentErrors.InputInvalid);
        return FulfillmentResult<SuCoMon>.Ok(new SuCoMon
        {
            PhieuCheBienId = ticketId,
            LyDo = clean,
            BaoLuc = now,
            NguoiBaoId = reporter,
            TrangThai = IncidentStatus.Pending,
            KhoaYeuCau = requestKey
        });
    }
}

/// <summary>Durable record of a completed ticket command for safe retries.</summary>
public sealed class YeuCauFulfillmentDaXuLy
{
    public string Operation { get; set; } = "";
    public Guid ActorScope { get; set; }
    public Guid Key { get; set; }
    public byte[] RequestHash { get; set; } = [];
    public long TicketId { get; set; }
    public DateTime CreatedAt { get; set; }
}
