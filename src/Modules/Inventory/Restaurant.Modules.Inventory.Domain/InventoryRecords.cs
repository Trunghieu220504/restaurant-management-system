namespace Restaurant.Modules.Inventory.Domain;

public sealed class DonVi
{
    public long Id { get; set; }
    public string Ma { get; set; } = "";
    public string Ten { get; set; } = "";
    public string LoaiDaiLuong { get; set; } = "";
}

public sealed class NguyenLieu
{
    public long Id { get; set; }
    public string Ma { get; set; } = "";
    public string Ten { get; set; } = "";
    public long DonViChuanId { get; set; }
    public string CheDo { get; set; } = "";
    public bool LuuTru { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class QuyDoi
{
    public long NguyenLieuId { get; set; }
    public long DonViNhapId { get; set; }
    public long DonViChuanId { get; set; }
    public decimal HeSoVeChuan { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class NhaCungCap
{
    public long Id { get; set; }
    public string Ten { get; set; } = "";
    public string? DienThoai { get; set; }
    public string? DiaChi { get; set; }
    public bool HoatDong { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class PhieuNhap
{
    public long Id { get; set; }
    public Guid NgayBanId { get; set; }
    public long? NhaCungCapId { get; set; }
    public string TrangThai { get; set; } = "";
    public bool TraBangTienKet { get; set; }
    public string? GhiChu { get; set; }
    public DateTimeOffset TaoLuc { get; set; }
    public Guid NguoiLapId { get; set; }
    public Guid KhoaYeuCau { get; set; }
    public string DauVanTay { get; set; } = "";
    public byte[] Version { get; set; } = [];
}

public sealed class ChiTietPhieuNhap
{
    public long Id { get; set; }
    public long PhieuNhapId { get; set; }
    public long NguyenLieuId { get; set; }
    public long DonViId { get; set; }
    public decimal SoLuong { get; set; }
    public long DonViChuanChotId { get; set; }
    public decimal HeSoChot { get; set; }
    public decimal DonGiaNhap { get; set; }
    public string CheDoChot { get; set; } = "";
    public decimal SoLuongChuan { get; set; }
    public decimal ThanhTien { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class KiemKeTon
{
    public long Id { get; set; }
    public Guid NgayBanId { get; set; }
    public long NguyenLieuId { get; set; }
    public string CheDoChot { get; set; } = "";
    public decimal TonDau { get; set; }
    public decimal GiaTriTonDau { get; set; }
    public decimal? SoDemNhap { get; set; }
    public decimal? SoDemDuyet { get; set; }
    public Guid? NguoiDuyetId { get; set; }
    public DateTimeOffset? DuyetLuc { get; set; }
    public DateTimeOffset? ChotLuc { get; set; }
    public decimal? GiaTriTonCuoi { get; set; }
    public byte[] Version { get; set; } = [];
}
