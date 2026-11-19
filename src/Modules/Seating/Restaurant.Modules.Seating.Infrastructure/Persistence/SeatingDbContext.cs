using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Seating.Domain;

namespace Restaurant.Modules.Seating.Infrastructure.Persistence;

public sealed class SeatingDbContext(DbContextOptions<SeatingDbContext> options)
    : DbContext(options)
{
    public const string Schema = "seating";

    public DbSet<KhuVuc> Areas => Set<KhuVuc>();
    public DbSet<Ban> Tables => Set<Ban>();
    public DbSet<PhienSuDung> Sessions => Set<PhienSuDung>();
    public DbSet<ChuyenBan> Transfers => Set<ChuyenBan>();
    public DbSet<QuyenTruyCapKhach> Grants => Set<QuyenTruyCapKhach>();
    public DbSet<YeuCauDaXuLy> Requests => Set<YeuCauDaXuLy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SeatingDbContext).Assembly);

        var area = modelBuilder.Entity<KhuVuc>();
        area.ToTable("KhuVuc");
        area.HasKey(x => x.Id);
        area.Property(x => x.Ma).HasMaxLength(32).IsUnicode(false).IsRequired();
        area.Property(x => x.Ten).HasMaxLength(150).IsRequired();
        area.Property(x => x.Version).IsRowVersion();
        area.HasIndex(x => x.Ma).IsUnique();

        var table = modelBuilder.Entity<Ban>();
        table.ToTable("Ban");
        table.HasKey(x => x.Id);
        table.Property(x => x.Ma).HasMaxLength(32).IsUnicode(false).IsRequired();
        table.Property(x => x.Ten).HasMaxLength(150).IsRequired();
        table.Property(x => x.MaQrCongKhai).HasMaxLength(100).IsUnicode(false).IsRequired();
        table.Property(x => x.Version).IsRowVersion();
        table.HasIndex(x => x.Ma).IsUnique();
        table.HasIndex(x => x.MaQrCongKhai).IsUnique();
        table.HasOne<KhuVuc>().WithMany().HasForeignKey(x => x.KhuVucId).OnDelete(DeleteBehavior.NoAction);

        var session = modelBuilder.Entity<PhienSuDung>();
        session.ToTable("PhienSuDung", t =>
        {
            t.HasCheckConstraint("CK_PhienSuDung_Closure", "([DongLuc] IS NULL AND [NguoiDongId] IS NULL) OR ([DongLuc] IS NOT NULL AND [NguoiDongId] IS NOT NULL AND [DongLuc] >= [MoLuc])");
        });
        session.HasKey(x => x.Id);
        session.Property(x => x.Version).IsRowVersion();
        session.HasOne<Ban>().WithMany().HasForeignKey(x => x.BanHienTaiId).OnDelete(DeleteBehavior.NoAction);
        session.HasIndex(x => x.BanHienTaiId).IsUnique().HasFilter("[DongLuc] IS NULL");
        session.HasIndex(x => x.NgayBanId);
        session.HasIndex(x => x.NguoiMoId);
        session.HasIndex(x => x.NguoiDongId);

        var move = modelBuilder.Entity<ChuyenBan>();
        move.ToTable("ChuyenBan", t => t.HasCheckConstraint("CK_ChuyenBan_DifferentTables", "[BanCuId] <> [BanMoiId]"));
        move.HasKey(x => x.Id);
        move.Property(x => x.LyDo).HasMaxLength(1000).IsRequired();
        move.HasOne<PhienSuDung>().WithMany().HasForeignKey(x => x.PhienSuDungId).OnDelete(DeleteBehavior.NoAction);
        move.HasOne<Ban>().WithMany().HasForeignKey(x => x.BanCuId).OnDelete(DeleteBehavior.NoAction);
        move.HasOne<Ban>().WithMany().HasForeignKey(x => x.BanMoiId).OnDelete(DeleteBehavior.NoAction);
        move.HasIndex(x => x.NguoiChuyenId);

        var grant = modelBuilder.Entity<QuyenTruyCapKhach>();
        grant.ToTable("QuyenTruyCapKhach", t =>
        {
            t.HasCheckConstraint("CK_QuyenTruyCapKhach_Expiry", "[HetHanLuc] > [TaoLuc]");
        });
        grant.HasKey(x => x.Id);
        grant.Property(x => x.Id).ValueGeneratedNever();
        grant.Property(x => x.TokenHash).HasColumnType("binary(32)").IsRequired();
        grant.Property(x => x.Version).IsRowVersion();
        grant.HasIndex(x => x.TokenHash).IsUnique();
        grant.HasOne<PhienSuDung>().WithMany().HasForeignKey(x => x.PhienSuDungId).OnDelete(DeleteBehavior.NoAction);

        var request = modelBuilder.Entity<YeuCauDaXuLy>();
        request.ToTable("YeuCauDaXuLy");
        request.HasKey(x => new { x.Operation, x.ActorScope, x.Key }).HasName("PK_YeuCauDaXuLy");
        request.Property(x => x.Operation).HasMaxLength(40).IsUnicode(false).IsRequired();
        request.Property(x => x.RequestHash).HasColumnType("binary(32)").IsRequired();
        request.Property(x => x.ResultReference).HasMaxLength(100).IsUnicode(false).IsRequired();
        request.Property(x => x.State).HasMaxLength(20).IsUnicode(false).IsRequired();
    }
}
