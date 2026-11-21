namespace Restaurant.Modules.Billing.Domain;

public sealed class HoaDon
{
    public long Id { get; set; }
    public long DonHangId { get; set; }
    public string MaHoaDon { get; set; } = string.Empty;
    public Guid NgayBanLapId { get; set; }
    public DateTime TaoLuc { get; set; }
    public DateTime? DongLuc { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class DongHoaDon
{
    public long Id { get; set; }
    public long HoaDonId { get; set; }
    public long ChiTietDonId { get; set; }
    public string TenMonChot { get; set; } = string.Empty;
    public string? NoiDungTuyChonChot { get; set; }
    public decimal ThanhTienGoc { get; set; }
}

public sealed class DieuChinhNghiaVu
{
    public long Id { get; set; }
    public long HoaDonId { get; set; }
    public decimal SoTien { get; set; }
    public string LyDo { get; set; } = string.Empty;
    public long? SuCoMonId { get; set; }
    public long? GiaoDichGocId { get; set; }
    public Guid NgayBanId { get; set; }
    public Guid NguoiDuyetId { get; set; }
    public DateTime TaoLuc { get; set; }
    public Guid KhoaYeuCau { get; set; }
}

public sealed class MienThu
{
    public long Id { get; set; }
    public long HoaDonId { get; set; }
    public decimal SoTien { get; set; }
    public string LyDo { get; set; } = string.Empty;
    public Guid NgayBanId { get; set; }
    public Guid NguoiDuyetId { get; set; }
    public DateTime TaoLuc { get; set; }
    public Guid KhoaYeuCau { get; set; }
}

public sealed class ThanhToan
{
    public long Id { get; set; }
    public long HoaDonId { get; set; }
    public Guid NgayBanId { get; set; }
    public string PhuongThuc { get; set; } = string.Empty;
    public decimal SoTien { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public string? MaThamChieuNganHang { get; set; }
    public decimal? TienKhachDua { get; set; }
    public decimal? TienThua { get; set; }
    public Guid NguoiThuId { get; set; }
    public DateTime? XacNhanLuc { get; set; }
    public DateTime TaoLuc { get; set; }
    public Guid KhoaYeuCau { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class HoanTien
{
    public long Id { get; set; }
    public long ThanhToanId { get; set; }
    public Guid NgayBanId { get; set; }
    public decimal SoTien { get; set; }
    public string PhuongThuc { get; set; } = string.Empty;
    public string TrangThai { get; set; } = string.Empty;
    public string LyDo { get; set; } = string.Empty;
    public Guid NguoiDeNghiId { get; set; }
    public Guid? NguoiDuyetId { get; set; }
    public Guid? NguoiChiId { get; set; }
    public string? MaChiTra { get; set; }
    public DateTime TaoLuc { get; set; }
    public DateTime? ChiLuc { get; set; }
    public Guid KhoaYeuCau { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class ChungTu
{
    public long Id { get; set; }
    public long HoaDonId { get; set; }
    public string SoChungTu { get; set; } = string.Empty;
    public string Loai { get; set; } = string.Empty;
    public long? ThanhToanId { get; set; }
    public long? HoanTienId { get; set; }
    public string NoiDungChot { get; set; } = string.Empty;
    public DateTime TaoLuc { get; set; }
    public Guid NguoiLapId { get; set; }
}
