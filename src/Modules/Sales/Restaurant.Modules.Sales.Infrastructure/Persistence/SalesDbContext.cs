using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Sales.Domain;

namespace Restaurant.Modules.Sales.Infrastructure.Persistence;

public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options)
    : DbContext(options)
{
    public const string Schema = "sales";

    public DbSet<DonHang> Orders => Set<DonHang>();
    public DbSet<DotGoiMon> Batches => Set<DotGoiMon>();
    public DbSet<ChiTietDon> Portions => Set<ChiTietDon>();
    public DbSet<TuyChonDaDat> PortionOptions => Set<TuyChonDaDat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);

        // References to Seating, SalesDay, Catalog and Identity are logical (no cross-module FK). Money is whole VND (bigint),
        // like Catalog prices and the SalesDay opening cash.
        var order = modelBuilder.Entity<DonHang>();
        order.ToTable("DonHang", t =>
        {
            t.HasCheckConstraint("CK_DonHang_Loai", "[Loai] IN ('TAI_BAN','MANG_DI')");
            t.HasCheckConstraint("CK_DonHang_TrangThai", "[TrangThai] IN ('MO','DONG','HUY')");
            t.HasCheckConstraint("CK_DonHang_Phien",
                "([Loai] = 'TAI_BAN' AND [PhienSuDungId] IS NOT NULL) OR ([Loai] = 'MANG_DI' AND [PhienSuDungId] IS NULL AND [NhanBanLucTao] IS NULL)");
            t.HasCheckConstraint("CK_DonHang_Dong",
                "([TrangThai] = 'MO' AND [DongLuc] IS NULL) OR ([TrangThai] <> 'MO' AND [DongLuc] IS NOT NULL AND [DongLuc] >= [TaoLuc])");
        });
        order.HasKey(x => x.Id);
        order.Property(x => x.MaDon).HasMaxLength(32).IsUnicode(false).IsRequired();
        order.Property(x => x.Loai).HasMaxLength(32).IsUnicode(false).IsRequired();
        order.Property(x => x.NhanBanLucTao).HasMaxLength(150);
        order.Property(x => x.TenKhach).HasMaxLength(150);
        order.Property(x => x.TrangThai).HasMaxLength(32).IsUnicode(false).IsRequired();
        order.Property(x => x.Version).IsRowVersion();
        order.Property(x => x.SoLanCapNhat).HasDefaultValue(0);
        order.HasIndex(x => x.MaDon).IsUnique();
        // D-10: at most one open order per session. Takeaway orders have no session, so NULL must not collide.
        order.HasIndex(x => x.PhienSuDungId).IsUnique().HasDatabaseName("UX_DonHang_PhienMo")
            .HasFilter("[TrangThai] = 'MO' AND [PhienSuDungId] IS NOT NULL");
        order.HasIndex(x => new { x.NgayBanId, x.TrangThai });
        order.HasIndex(x => x.PhienSuDungId);
        order.HasIndex(x => x.NguoiTaoId);
        order.HasMany(x => x.Dots).WithOne().HasForeignKey(x => x.DonHangId).OnDelete(DeleteBehavior.NoAction);
        order.Navigation(x => x.Dots).UsePropertyAccessMode(PropertyAccessMode.Field);

        var batch = modelBuilder.Entity<DotGoiMon>();
        batch.ToTable("DotGoiMon", t =>
        {
            t.HasCheckConstraint("CK_DotGoiMon_SoDot", "[SoDot] > 0");
            t.HasCheckConstraint("CK_DotGoiMon_Nguon", "[Nguon] IN ('KHACH','NHAN_VIEN')");
            t.HasCheckConstraint("CK_DotGoiMon_TrangThai",
                "[TrangThai] IN ('CHO_XAC_NHAN','DA_XAC_NHAN','DA_TU_CHOI','DA_HUY')");
            t.HasCheckConstraint("CK_DotGoiMon_NguoiTao",
                "([Nguon] = 'KHACH' AND [QuyenTruyCapKhachId] IS NOT NULL AND [NguoiTaoId] IS NULL) OR ([Nguon] = 'NHAN_VIEN' AND [NguoiTaoId] IS NOT NULL AND [QuyenTruyCapKhachId] IS NULL)");
            t.HasCheckConstraint("CK_DotGoiMon_QuyetDinh",
                "([TrangThai] IN ('CHO_XAC_NHAN','DA_HUY') AND [XacNhanLuc] IS NULL AND [NguoiXacNhanId] IS NULL AND [LyDoTuChoi] IS NULL)"
                + " OR ([TrangThai] = 'DA_XAC_NHAN' AND [XacNhanLuc] IS NOT NULL AND [NguoiXacNhanId] IS NOT NULL AND [LyDoTuChoi] IS NULL)"
                + " OR ([TrangThai] = 'DA_TU_CHOI' AND [XacNhanLuc] IS NOT NULL AND [NguoiXacNhanId] IS NOT NULL AND [LyDoTuChoi] IS NOT NULL)");
        });
        batch.HasKey(x => x.Id);
        batch.Property(x => x.Nguon).HasMaxLength(32).IsUnicode(false).IsRequired();
        batch.Property(x => x.TrangThai).HasMaxLength(32).IsUnicode(false).IsRequired();
        batch.Property(x => x.LyDoTuChoi).HasMaxLength(1000);
        batch.Property(x => x.Version).IsRowVersion();
        batch.HasIndex(x => new { x.DonHangId, x.SoDot }).IsUnique();
        batch.HasIndex(x => x.KhoaYeuCau).IsUnique();
        batch.HasIndex(x => x.TrangThai);
        batch.HasIndex(x => x.QuyenTruyCapKhachId);
        batch.HasIndex(x => x.NguoiTaoId);
        batch.HasIndex(x => x.NguoiXacNhanId);
        batch.HasMany(x => x.Portions).WithOne().HasForeignKey(x => x.DotGoiMonId).OnDelete(DeleteBehavior.NoAction);
        batch.Navigation(x => x.Portions).UsePropertyAccessMode(PropertyAccessMode.Field);

        var portion = modelBuilder.Entity<ChiTietDon>();
        portion.ToTable("ChiTietDon", t =>
        {
            t.HasCheckConstraint("CK_ChiTietDon_Gia", "[GiaMonChot] >= 0");
            t.HasCheckConstraint("CK_ChiTietDon_Tram", "[TramChot] IN ('BEP','BAR')");
            t.HasCheckConstraint("CK_ChiTietDon_TrangThai", "[TrangThai] IN ('CHO_XAC_NHAN','DA_XAC_NHAN','DA_HUY')");
        });
        portion.HasKey(x => x.Id);
        portion.Property(x => x.TenMonChot).HasMaxLength(150).IsRequired();
        portion.Property(x => x.TenBienTheChot).HasMaxLength(150).IsRequired();
        portion.Property(x => x.TramChot).HasMaxLength(32).IsUnicode(false).IsRequired();
        portion.Property(x => x.GhiChu).HasMaxLength(2000);
        portion.Property(x => x.TrangThai).HasMaxLength(32).IsUnicode(false).IsRequired();
        portion.Property(x => x.Version).IsRowVersion();
        portion.HasIndex(x => x.DotGoiMonId);
        portion.HasIndex(x => new { x.TramChot, x.TrangThai });
        portion.HasIndex(x => x.MonId);
        portion.HasIndex(x => x.BienTheId);
        portion.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.ChiTietDonId).OnDelete(DeleteBehavior.NoAction);
        portion.Navigation(x => x.Options).UsePropertyAccessMode(PropertyAccessMode.Field);

        var option = modelBuilder.Entity<TuyChonDaDat>();
        option.ToTable("TuyChonDaDat", t =>
        {
            t.HasCheckConstraint("CK_TuyChonDaDat_Gia", "[GiaCongChot] >= 0");
            t.HasCheckConstraint("CK_TuyChonDaDat_Loai", "[LoaiNhomChot] IN ('TOPPING','TUY_CHON')");
        });
        option.HasKey(x => new { x.ChiTietDonId, x.TuyChonId });
        option.Property(x => x.TenNhomChot).HasMaxLength(150).IsRequired();
        option.Property(x => x.LoaiNhomChot).HasMaxLength(32).IsUnicode(false).IsRequired();
        option.Property(x => x.TenTuyChonChot).HasMaxLength(150).IsRequired();
        option.HasIndex(x => x.TuyChonId);
    }
}
