namespace Restaurant.Modules.Catalog.Domain;

public sealed class NhomMon
{
    public Guid Id { get; set; }
    public string Ten { get; set; } = ""; public int ThuTu { get; set; }
    public bool HoatDong { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
public sealed class Mon
{
    public Guid Id { get; set; }
    public Guid NhomMonId { get; set; } public string Ma { get; set; } = ""; public string Ten { get; set; } = ""; public string Tram { get; set; } = "BEP"; public string? AnhObjectKey { get; set; }
    public bool HoatDong { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
public sealed class BienThe
{
    public Guid Id { get; set; }
    public Guid MonId { get; set; } public string Ma { get; set; } = ""; public string Ten { get; set; } = ""; public long Gia { get; set; }
    public bool HoatDong { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
public sealed class NhomTuyChon
{
    public Guid Id { get; set; }
    public string Ten { get; set; } = ""; public string Loai { get; set; } = "TUY_CHON";
    public bool HoatDong { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
public sealed class TuyChon
{
    public Guid Id { get; set; }
    public Guid NhomTuyChonId { get; set; } public string Ten { get; set; } = ""; public long GiaCong { get; set; }
    public bool HoatDong { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
public sealed class MonNhomTuyChon
{
    public Guid MonId { get; set; }
    public Guid NhomTuyChonId { get; set; }
    public int ChonToiThieu { get; set; }
    public int ChonToiDa { get; set; }
}
public sealed class HetMon
{
    public Guid NgayBanId { get; set; }
    public Guid MonId { get; set; }
    public bool Het { get; set; }
    public DateTimeOffset CapNhatLuc { get; set; }
    public Guid NguoiCapNhatId { get; set; }
    public byte[] Version { get; set; } = [];
}
public sealed class HetBienThe
{
    public Guid NgayBanId { get; set; }
    public Guid BienTheId { get; set; }
    public bool Het { get; set; }
    public DateTimeOffset CapNhatLuc { get; set; }
    public Guid NguoiCapNhatId { get; set; }
    public byte[] Version { get; set; } = [];
}
public sealed class HetTuyChon
{
    public Guid NgayBanId { get; set; }
    public Guid TuyChonId { get; set; }
    public bool Het { get; set; }
    public DateTimeOffset CapNhatLuc { get; set; }
    public Guid NguoiCapNhatId { get; set; }
    public byte[] Version { get; set; } = [];
}
