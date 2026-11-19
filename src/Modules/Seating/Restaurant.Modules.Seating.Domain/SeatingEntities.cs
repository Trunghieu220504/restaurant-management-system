namespace Restaurant.Modules.Seating.Domain;

public sealed class KhuVuc
{
    public long Id { get; set; }
    public string Ma { get; set; } = "";
    public string Ten { get; set; } = "";
    public bool HoatDong { get; set; } = true;
    public byte[] Version { get; set; } = [];
}

public sealed class Ban
{
    public long Id { get; set; }
    public long KhuVucId { get; set; }
    public string Ma { get; set; } = "";
    public string Ten { get; set; } = "";
    public bool HoatDong { get; set; } = true;
    public string MaQrCongKhai { get; set; } = "";
    public byte[] Version { get; set; } = [];
}

public sealed class PhienSuDung
{
    public long Id { get; set; }
    public long BanHienTaiId { get; set; }
    public Guid NgayBanId { get; set; }
    public DateTime MoLuc { get; set; }
    public Guid NguoiMoId { get; set; }
    public DateTime? DongLuc { get; set; }
    public Guid? NguoiDongId { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class ChuyenBan
{
    public long Id { get; set; }
    public long PhienSuDungId { get; set; }
    public long BanCuId { get; set; }
    public long BanMoiId { get; set; }
    public DateTime ChuyenLuc { get; set; }
    public Guid NguoiChuyenId { get; set; }
    public string LyDo { get; set; } = "";
}

public sealed class QuyenTruyCapKhach
{
    public Guid Id { get; set; }
    public long PhienSuDungId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTime TaoLuc { get; set; }
    public DateTime HetHanLuc { get; set; }
    public DateTime? ThuHoiLuc { get; set; }
    public byte[] Version { get; set; } = [];
}

/// <summary>
/// Record of a command that already ran, keyed by (operation, actor, idempotency key). The same key with the
/// same payload hash replays the stored result; the same key with a different payload is rejected.
/// Raw guest tokens or passwords must never be stored here.
/// </summary>
public sealed class YeuCauDaXuLy
{
    public string Operation { get; set; } = "";
    public Guid ActorScope { get; set; }
    public Guid Key { get; set; }
    public byte[] RequestHash { get; set; } = [];
    public string ResultReference { get; set; } = "";
    public string State { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
