using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Billing.Domain;

namespace Restaurant.Modules.Billing.Infrastructure.Persistence;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options)
    : DbContext(options)
{
    public const string Schema = "billing";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<BillingRequest>(entity =>
        {
            entity.ToTable("YeuCauDaXuLy");
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).ValueGeneratedNever();
            entity.Property(x => x.Fingerprint).HasMaxLength(64).IsUnicode(false);
            entity.HasOne<HoaDon>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<HoaDon>(entity =>
        {
            entity.ToTable("HoaDon", table =>
            {
                table.HasCheckConstraint("CK_HoaDon_DongLuc",
                    "[DongLuc] IS NULL OR [DongLuc] >= [TaoLuc]");
            });
            entity.HasIndex(x => x.DonHangId).IsUnique();
            entity.Property(x => x.MaHoaDon).HasMaxLength(32).IsUnicode(false);
            entity.HasIndex(x => x.MaHoaDon).IsUnique();
            entity.HasIndex(x => x.NgayBanLapId);
            entity.Property(x => x.Version).IsRowVersion();
        });

        modelBuilder.Entity<DongHoaDon>(entity =>
        {
            entity.ToTable("DongHoaDon", table =>
            {
                table.HasCheckConstraint("CK_DongHoaDon_ThanhTienGoc",
                    "[ThanhTienGoc] >= 0");
            });
            entity.HasOne<HoaDon>().WithMany().HasForeignKey(x => x.HoaDonId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => x.ChiTietDonId).IsUnique();
            entity.Property(x => x.TenMonChot).HasMaxLength(150);
            entity.Property(x => x.NoiDungTuyChonChot).HasMaxLength(2000);
            entity.Property(x => x.ThanhTienGoc).HasPrecision(18, 0);
        });

        modelBuilder.Entity<DieuChinhNghiaVu>(entity =>
        {
            entity.ToTable("DieuChinhNghiaVu", table =>
            {
                table.HasCheckConstraint("CK_DieuChinhNghiaVu_SoTien",
                    "[SoTien] <> 0");
                table.HasCheckConstraint("CK_DieuChinhNghiaVu_GiaoDichGoc",
                    "[GiaoDichGocId] IS NULL OR [GiaoDichGocId] <> [Id]");
            });
            entity.HasOne<HoaDon>().WithMany().HasForeignKey(x => x.HoaDonId).OnDelete(DeleteBehavior.NoAction);
            entity.Property(x => x.SoTien).HasPrecision(18, 0);
            entity.Property(x => x.LyDo).HasMaxLength(1000);
            entity.HasIndex(x => x.SuCoMonId);
            entity.HasOne<DieuChinhNghiaVu>().WithMany().HasForeignKey(x => x.GiaoDichGocId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => x.NgayBanId);
            entity.HasIndex(x => x.NguoiDuyetId);
            entity.HasIndex(x => x.KhoaYeuCau).IsUnique();
        });

        modelBuilder.Entity<MienThu>(entity =>
        {
            entity.ToTable("MienThu", table =>
            {
                table.HasCheckConstraint("CK_MienThu_SoTien",
                    "[SoTien] > 0");
            });
            entity.HasOne<HoaDon>().WithMany().HasForeignKey(x => x.HoaDonId).OnDelete(DeleteBehavior.NoAction);
            entity.Property(x => x.SoTien).HasPrecision(18, 0);
            entity.Property(x => x.LyDo).HasMaxLength(1000);
            entity.HasIndex(x => x.NgayBanId);
            entity.HasIndex(x => x.NguoiDuyetId);
            entity.HasIndex(x => x.KhoaYeuCau).IsUnique();
        });

        modelBuilder.Entity<ThanhToan>(entity =>
        {
            entity.ToTable("ThanhToan", table =>
            {
                table.HasCheckConstraint("CK_ThanhToan_SoTien",
                    "[SoTien] > 0");
                table.HasCheckConstraint("CK_ThanhToan_TrangThai",
                    "[TrangThai] IN ('CHO_XAC_NHAN','THANH_CONG','HUY')");
                table.HasCheckConstraint("CK_ThanhToan_PhuongThuc",
                    "([PhuongThuc] = 'TIEN_MAT' AND [TienKhachDua] IS NOT NULL AND [TienThua] IS NOT NULL AND [TienKhachDua] >= [SoTien] AND [TienThua] = [TienKhachDua] - [SoTien]) OR ([PhuongThuc] = 'CHUYEN_KHOAN' AND [TienKhachDua] IS NULL AND [TienThua] IS NULL)");
            });
            entity.HasOne<HoaDon>().WithMany().HasForeignKey(x => x.HoaDonId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => x.NgayBanId);
            entity.Property(x => x.PhuongThuc).HasMaxLength(32).IsUnicode(false);
            entity.Property(x => x.SoTien).HasPrecision(18, 0);
            entity.Property(x => x.TrangThai).HasMaxLength(32).IsUnicode(false);
            entity.Property(x => x.MaThamChieuNganHang).HasMaxLength(150).IsUnicode(false);
            entity.Property(x => x.TienKhachDua).HasPrecision(18, 0);
            entity.Property(x => x.TienThua).HasPrecision(18, 0);
            entity.HasIndex(x => x.NguoiThuId);
            entity.HasIndex(x => x.KhoaYeuCau).IsUnique();
            entity.Property(x => x.Version).IsRowVersion();
        });

        modelBuilder.Entity<HoanTien>(entity =>
        {
            entity.ToTable("HoanTien", table =>
            {
                table.HasCheckConstraint("CK_HoanTien_SoTien",
                    "[SoTien] > 0");
                table.HasCheckConstraint("CK_HoanTien_PhuongThuc",
                    "[PhuongThuc] IN ('TIEN_MAT','CHUYEN_KHOAN')");
                table.HasCheckConstraint("CK_HoanTien_TrangThai",
                    "[TrangThai] IN ('CHO_DUYET','DA_DUYET','DANG_CHI','CHO_DOI_SOAT','THANH_CONG','TU_CHOI','HUY')");
            });
            entity.HasOne<ThanhToan>().WithMany().HasForeignKey(x => x.ThanhToanId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => x.NgayBanId);
            entity.Property(x => x.SoTien).HasPrecision(18, 0);
            entity.Property(x => x.PhuongThuc).HasMaxLength(32).IsUnicode(false);
            entity.Property(x => x.TrangThai).HasMaxLength(32).IsUnicode(false);
            entity.Property(x => x.LyDo).HasMaxLength(1000);
            entity.HasIndex(x => x.NguoiDeNghiId);
            entity.HasIndex(x => x.NguoiDuyetId);
            entity.HasIndex(x => x.NguoiChiId);
            entity.Property(x => x.MaChiTra).HasMaxLength(150).IsUnicode(false);
            entity.HasIndex(x => x.KhoaYeuCau).IsUnique();
            entity.Property(x => x.Version).IsRowVersion();
        });

        modelBuilder.Entity<ChungTu>(entity =>
        {
            entity.ToTable("ChungTu", table =>
            {
                table.HasCheckConstraint("CK_ChungTu_Loai",
                    "([Loai] = 'HOA_DON' AND [ThanhToanId] IS NULL AND [HoanTienId] IS NULL) OR ([Loai] = 'PHIEU_THU' AND [ThanhToanId] IS NOT NULL AND [HoanTienId] IS NULL) OR ([Loai] = 'PHIEU_HOAN' AND [ThanhToanId] IS NULL AND [HoanTienId] IS NOT NULL)");
            });
            entity.HasOne<HoaDon>().WithMany().HasForeignKey(x => x.HoaDonId).OnDelete(DeleteBehavior.NoAction);
            entity.Property(x => x.SoChungTu).HasMaxLength(32).IsUnicode(false);
            entity.HasIndex(x => x.SoChungTu).IsUnique();
            entity.Property(x => x.Loai).HasMaxLength(32).IsUnicode(false);
            entity.HasOne<ThanhToan>().WithMany().HasForeignKey(x => x.ThanhToanId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<HoanTien>().WithMany().HasForeignKey(x => x.HoanTienId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(x => x.NguoiLapId);
        });
    }
}
