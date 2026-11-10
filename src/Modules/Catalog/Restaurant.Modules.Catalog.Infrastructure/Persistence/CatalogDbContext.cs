using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Catalog.Domain;
namespace Restaurant.Modules.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        var nhommon = modelBuilder.Entity<NhomMon>();
        nhommon.ToTable("NhomMon");
        nhommon.HasKey(x => x.Id);
        nhommon.Property(x => x.Version).IsRowVersion();
        nhommon.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        var mon = modelBuilder.Entity<Mon>();
        mon.ToTable("Mon");
        mon.HasKey(x => x.Id);
        mon.Property(x => x.Version).IsRowVersion();
        mon.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        var bienthe = modelBuilder.Entity<BienThe>();
        bienthe.ToTable("BienThe");
        bienthe.HasKey(x => x.Id);
        bienthe.Property(x => x.Version).IsRowVersion();
        bienthe.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        var nhomtuychon = modelBuilder.Entity<NhomTuyChon>();
        nhomtuychon.ToTable("NhomTuyChon");
        nhomtuychon.HasKey(x => x.Id);
        nhomtuychon.Property(x => x.Version).IsRowVersion();
        nhomtuychon.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        var tuychon = modelBuilder.Entity<TuyChon>();
        tuychon.ToTable("TuyChon");
        tuychon.HasKey(x => x.Id);
        tuychon.Property(x => x.Version).IsRowVersion();
        tuychon.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        mon.Property(x => x.Ma).HasMaxLength(50).IsRequired();
        mon.HasIndex(x => x.Ma).IsUnique();
        mon.Property(x => x.Tram).HasMaxLength(8).IsRequired();
        mon.Property(x => x.AnhObjectKey).HasMaxLength(512);
        mon.ToTable(t => t.HasCheckConstraint("CK_Mon_Tram", "[Tram] IN ('BEP','BAR')"));
        mon.HasOne<NhomMon>().WithMany().HasForeignKey(x => x.NhomMonId).OnDelete(DeleteBehavior.Restrict);
        bienthe.Property(x => x.Ma).HasMaxLength(50).IsRequired();
        bienthe.HasIndex(x => new { x.MonId, x.Ma }).IsUnique();
        bienthe.HasOne<Mon>().WithMany().HasForeignKey(x => x.MonId).OnDelete(DeleteBehavior.Restrict);
        bienthe.ToTable(t => t.HasCheckConstraint("CK_BienThe_Gia", "[Gia] >= 0"));
        nhomtuychon.Property(x => x.Loai).HasMaxLength(16).IsRequired();
        nhomtuychon.ToTable(t => t.HasCheckConstraint("CK_NhomTuyChon_Loai", "[Loai] IN ('TOPPING','TUY_CHON')"));
        tuychon.HasOne<NhomTuyChon>().WithMany().HasForeignKey(x => x.NhomTuyChonId).OnDelete(DeleteBehavior.Restrict);
        tuychon.ToTable(t => t.HasCheckConstraint("CK_TuyChon_Gia", "[GiaCong] >= 0"));
        var assignment = modelBuilder.Entity<MonNhomTuyChon>();
        assignment.ToTable("MonNhomTuyChon", t => t.HasCheckConstraint("CK_MonNhomTuyChon_Range", "[ChonToiThieu] >= 0 AND [ChonToiDa] >= [ChonToiThieu]"));
        assignment.HasKey(x => new { x.MonId, x.NhomTuyChonId });
        assignment.HasOne<Mon>().WithMany().HasForeignKey(x => x.MonId).OnDelete(DeleteBehavior.Restrict);
        assignment.HasOne<NhomTuyChon>().WithMany().HasForeignKey(x => x.NhomTuyChonId).OnDelete(DeleteBehavior.Restrict);
        var hetmon = modelBuilder.Entity<HetMon>();
        hetmon.ToTable("HetMon");
        hetmon.HasKey(x => new { x.NgayBanId, x.MonId });
        hetmon.HasOne<Mon>().WithMany().HasForeignKey(x => x.MonId).OnDelete(DeleteBehavior.Restrict);
        hetmon.Property(x => x.Version).IsRowVersion();
        var hetbienthe = modelBuilder.Entity<HetBienThe>();
        hetbienthe.ToTable("HetBienThe");
        hetbienthe.HasKey(x => new { x.NgayBanId, x.BienTheId });
        hetbienthe.HasOne<BienThe>().WithMany().HasForeignKey(x => x.BienTheId).OnDelete(DeleteBehavior.Restrict);
        hetbienthe.Property(x => x.Version).IsRowVersion();
        var hettuychon = modelBuilder.Entity<HetTuyChon>();
        hettuychon.ToTable("HetTuyChon");
        hettuychon.HasKey(x => new { x.NgayBanId, x.TuyChonId });
        hettuychon.HasOne<TuyChon>().WithMany().HasForeignKey(x => x.TuyChonId).OnDelete(DeleteBehavior.Restrict);
        hettuychon.Property(x => x.Version).IsRowVersion();
    }
}
