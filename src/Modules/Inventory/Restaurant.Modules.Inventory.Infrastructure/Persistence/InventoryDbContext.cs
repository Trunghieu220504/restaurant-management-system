using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Inventory.Domain;

namespace Restaurant.Modules.Inventory.Infrastructure.Persistence;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options)
    : DbContext(options)
{
    public const string Schema = "inventory";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<DonVi>(e =>
        {
            e.ToTable("DonVi", t =>
            {
                t.HasCheckConstraint("CK_DonVi_Loai", "[LoaiDaiLuong] IN ('KHOI_LUONG','THE_TICH','DEM')");
            });
            e.Property(x => x.Ma).HasMaxLength(32);
            e.HasIndex(x => x.Ma).IsUnique();
            e.Property(x => x.Ten).HasMaxLength(150);
            e.Property(x => x.LoaiDaiLuong).HasMaxLength(32);
        });

        modelBuilder.Entity<NguyenLieu>(e =>
        {
            e.ToTable("NguyenLieu", t =>
            {
                t.HasCheckConstraint("CK_NguyenLieu_CheDo", "[CheDo] IN ('THEO_DOI_TON','TINH_KHI_MUA')");
            });
            e.Property(x => x.Ma).HasMaxLength(32);
            e.HasIndex(x => x.Ma).IsUnique();
            e.Property(x => x.Ten).HasMaxLength(150);
            e.HasOne<DonVi>().WithMany().HasForeignKey(x => x.DonViChuanId).OnDelete(DeleteBehavior.NoAction);
            e.Property(x => x.CheDo).HasMaxLength(32);
            e.Property(x => x.Version).IsRowVersion();
        });

        modelBuilder.Entity<QuyDoi>(e =>
        {
            e.ToTable("QuyDoi", t =>
            {
                t.HasCheckConstraint("CK_QuyDoi_HeSo", "[HeSoVeChuan] > 0");
            });
            e.HasOne<NguyenLieu>().WithMany().HasForeignKey(x => x.NguyenLieuId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<DonVi>().WithMany().HasForeignKey(x => x.DonViNhapId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<DonVi>().WithMany().HasForeignKey(x => x.DonViChuanId).OnDelete(DeleteBehavior.NoAction);
            e.Property(x => x.HeSoVeChuan).HasPrecision(18, 6);
            e.Property(x => x.Version).IsRowVersion();
            e.HasKey(x => new { x.NguyenLieuId, x.DonViNhapId });
        });

        modelBuilder.Entity<NhaCungCap>(e =>
        {
            e.ToTable("NhaCungCap");
            e.Property(x => x.Ten).HasMaxLength(150);
            e.Property(x => x.DienThoai).HasMaxLength(32);
            e.Property(x => x.DiaChi).HasMaxLength(300);
            e.Property(x => x.Version).IsRowVersion();
        });

        modelBuilder.Entity<PhieuNhap>(e =>
        {
            e.ToTable("PhieuNhap", t =>
            {
                t.HasCheckConstraint("CK_PhieuNhap_TrangThai", "[TrangThai] IN ('NHAP','HIEU_LUC','HUY')");
            });
            e.HasOne<NhaCungCap>().WithMany().HasForeignKey(x => x.NhaCungCapId).OnDelete(DeleteBehavior.NoAction);
            e.Property(x => x.TrangThai).HasMaxLength(32);
            e.Property(x => x.GhiChu).HasMaxLength(2000);
            e.HasIndex(x => x.KhoaYeuCau).IsUnique();
            e.Property(x => x.DauVanTay).HasMaxLength(64);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.NgayBanId, x.TrangThai });
        });

        modelBuilder.Entity<ChiTietPhieuNhap>(e =>
        {
            e.ToTable("ChiTietPhieuNhap", t =>
            {
                t.HasCheckConstraint("CK_ChiTietPhieuNhap_SoLuong", "[SoLuong] > 0 AND [HeSoChot] > 0 AND [SoLuongChuan] > 0");
                t.HasCheckConstraint("CK_ChiTietPhieuNhap_Gia", "[DonGiaNhap] >= 0 AND [ThanhTien] >= 0");
                t.HasCheckConstraint("CK_ChiTietPhieuNhap_CheDo", "[CheDoChot] IN ('THEO_DOI_TON','TINH_KHI_MUA')");
            });
            e.HasOne<PhieuNhap>().WithMany().HasForeignKey(x => x.PhieuNhapId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<NguyenLieu>().WithMany().HasForeignKey(x => x.NguyenLieuId).OnDelete(DeleteBehavior.NoAction);
            e.Property(x => x.SoLuong).HasPrecision(18, 4);
            e.HasOne<DonVi>().WithMany().HasForeignKey(x => x.DonViChuanChotId).OnDelete(DeleteBehavior.NoAction);
            e.Property(x => x.HeSoChot).HasPrecision(18, 6);
            e.Property(x => x.DonGiaNhap).HasPrecision(18, 0);
            e.Property(x => x.CheDoChot).HasMaxLength(32);
            e.Property(x => x.SoLuongChuan).HasPrecision(28, 8);
            e.Property(x => x.ThanhTien).HasPrecision(18, 0);
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<QuyDoi>().WithMany().HasForeignKey(x => new { x.NguyenLieuId, x.DonViId }).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<KiemKeTon>(e =>
        {
            e.ToTable("KiemKeTon", t =>
            {
                t.HasCheckConstraint("CK_KiemKeTon_Ton", "[TonDau] >= 0 AND [GiaTriTonDau] >= 0 AND ([SoDemDuyet] IS NULL OR [SoDemDuyet] >= 0)");
            });
            e.HasOne<NguyenLieu>().WithMany().HasForeignKey(x => x.NguyenLieuId).OnDelete(DeleteBehavior.NoAction);
            e.Property(x => x.CheDoChot).HasMaxLength(32);
            e.Property(x => x.TonDau).HasPrecision(18, 4);
            e.Property(x => x.GiaTriTonDau).HasPrecision(18, 0);
            e.Property(x => x.SoDemNhap).HasPrecision(18, 4);
            e.Property(x => x.SoDemDuyet).HasPrecision(18, 4);
            e.Property(x => x.GiaTriTonCuoi).HasPrecision(18, 0);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.NgayBanId, x.NguyenLieuId }).IsUnique();
        });
    }
}
