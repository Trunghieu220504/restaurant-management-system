using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Fulfillment.Domain;

namespace Restaurant.Modules.Fulfillment.Infrastructure.Persistence;

public sealed class FulfillmentDbContext(DbContextOptions<FulfillmentDbContext> options)
    : DbContext(options)
{
    public const string Schema = "fulfillment";

    public DbSet<PhieuCheBien> Tickets => Set<PhieuCheBien>();
    public DbSet<SuCoMon> Incidents => Set<SuCoMon>();
    public DbSet<YeuCauFulfillmentDaXuLy> Requests => Set<YeuCauFulfillmentDaXuLy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        // Picks up the technical Outbox and Inbox mappings in Messaging/.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FulfillmentDbContext).Assembly);

        // ChiTietDonId, NguoiBatDauId, NguoiHoanThanhId, NguoiGiaoId, NguoiBaoId and NguoiDuyetId are logical references to
        // Sales and Identity (no cross-module key). The keys between tickets and incidents are inside this module and use
        // NO ACTION: the first ticket is inserted first, then the incident, then the remake, so there is no circular insert.
        var ticket = modelBuilder.Entity<PhieuCheBien>();
        ticket.ToTable("PhieuCheBien", t =>
        {
            t.HasCheckConstraint("CK_PhieuCheBien_Lan", "[Lan] > 0");
            t.HasCheckConstraint("CK_PhieuCheBien_Tram", "[TramChot] IN ('BEP','BAR')");
            t.HasCheckConstraint("CK_PhieuCheBien_TrangThai",
                "[TrangThai] IN ('MOI','DANG_LAM','SAN_SANG','DA_GIAO','DA_HUY','LOI')");
            // Attempt 1 has no original and no incident; every later attempt has both (a remake needs an approved incident).
            t.HasCheckConstraint("CK_PhieuCheBien_LamLai",
                "([Lan] = 1 AND [PhieuGocId] IS NULL AND [SuCoNguonId] IS NULL) OR ([Lan] > 1 AND [PhieuGocId] IS NOT NULL AND [SuCoNguonId] IS NOT NULL)");
            t.HasCheckConstraint("CK_PhieuCheBien_NguoiThaoTac",
                "([NguoiBatDauId] IS NULL AND [BatDauLuc] IS NULL OR [NguoiBatDauId] IS NOT NULL AND [BatDauLuc] IS NOT NULL)"
                + " AND ([NguoiHoanThanhId] IS NULL AND [HoanThanhLuc] IS NULL OR [NguoiHoanThanhId] IS NOT NULL AND [HoanThanhLuc] IS NOT NULL)"
                + " AND ([NguoiGiaoId] IS NULL AND [GiaoLuc] IS NULL OR [NguoiGiaoId] IS NOT NULL AND [GiaoLuc] IS NOT NULL)");
            // A new ticket has not been touched. A delivered ticket was finished and delivered; a ready one was finished.
            t.HasCheckConstraint("CK_PhieuCheBien_TienDo",
                "([TrangThai] <> 'MOI' OR ([BatDauLuc] IS NULL AND [HoanThanhLuc] IS NULL AND [GiaoLuc] IS NULL))"
                + " AND ([TrangThai] <> 'DANG_LAM' OR [BatDauLuc] IS NOT NULL)"
                + " AND ([TrangThai] <> 'SAN_SANG' OR [HoanThanhLuc] IS NOT NULL)"
                + " AND ([TrangThai] <> 'DA_GIAO' OR ([HoanThanhLuc] IS NOT NULL AND [GiaoLuc] IS NOT NULL))");
        });
        ticket.HasKey(x => x.Id);
        ticket.Property(x => x.TramChot).HasMaxLength(32).IsUnicode(false).IsRequired();
        ticket.Property(x => x.TrangThai).HasMaxLength(32).IsUnicode(false).IsRequired();
        ticket.Property(x => x.Version).IsRowVersion();
        // One ticket per attempt of a portion: a replayed "batch confirmed" event cannot create the same ticket twice.
        ticket.HasIndex(x => new { x.ChiTietDonId, x.Lan }).IsUnique();
        // One incident allows one remake, and one ticket is remade at most once (a straight chain of attempts).
        ticket.HasIndex(x => x.SuCoNguonId).IsUnique().HasDatabaseName("UX_PhieuCheBien_SuCoNguon")
            .HasFilter("[SuCoNguonId] IS NOT NULL");
        ticket.HasIndex(x => x.PhieuGocId).IsUnique().HasDatabaseName("UX_PhieuCheBien_PhieuGoc")
            .HasFilter("[PhieuGocId] IS NOT NULL");
        // The station queue: open tickets of one station, oldest first.
        ticket.HasIndex(x => new { x.TramChot, x.TrangThai, x.TaoLuc }).HasDatabaseName("IX_PhieuCheBien_HangDoi");
        ticket.HasOne<PhieuCheBien>().WithMany().HasForeignKey(x => x.PhieuGocId).OnDelete(DeleteBehavior.NoAction);
        ticket.HasOne<SuCoMon>().WithMany().HasForeignKey(x => x.SuCoNguonId).OnDelete(DeleteBehavior.NoAction);

        var incident = modelBuilder.Entity<SuCoMon>();
        incident.ToTable("SuCoMon", t =>
        {
            t.HasCheckConstraint("CK_SuCoMon_TrangThai", "[TrangThai] IN ('CHO_DUYET','DA_DUYET','TU_CHOI')");
            t.HasCheckConstraint("CK_SuCoMon_QuyetDinh",
                "[QuyetDinh] IS NULL OR [QuyetDinh] IN ('LAM_LAI','HUY_VAN_THU','HUY_GIAM_NGHIA_VU')");
            // Pending: nothing decided. Approved: a decision, who and when. Rejected: who and when, but no decision.
            t.HasCheckConstraint("CK_SuCoMon_Duyet",
                "([TrangThai] = 'CHO_DUYET' AND [QuyetDinh] IS NULL AND [NguoiDuyetId] IS NULL AND [DuyetLuc] IS NULL AND [LyDoDuyet] IS NULL)"
                + " OR ([TrangThai] = 'DA_DUYET' AND [QuyetDinh] IS NOT NULL AND [NguoiDuyetId] IS NOT NULL AND [DuyetLuc] IS NOT NULL)"
                + " OR ([TrangThai] = 'TU_CHOI' AND [QuyetDinh] IS NULL AND [NguoiDuyetId] IS NOT NULL AND [DuyetLuc] IS NOT NULL)");
            t.HasCheckConstraint("CK_SuCoMon_LyDo", "LEN(LTRIM(RTRIM([LyDo]))) > 0");
        });
        incident.HasKey(x => x.Id);
        incident.Property(x => x.LyDo).HasMaxLength(1000).IsRequired();
        incident.Property(x => x.TrangThai).HasMaxLength(32).IsUnicode(false).IsRequired();
        incident.Property(x => x.QuyetDinh).HasMaxLength(32).IsUnicode(false);
        incident.Property(x => x.LyDoDuyet).HasMaxLength(1000);
        incident.Property(x => x.Version).IsRowVersion();
        incident.HasIndex(x => x.KhoaYeuCau).IsUnique();
        incident.HasIndex(x => x.PhieuCheBienId);
        // The approval screen only reads incidents that wait for a decision.
        incident.HasIndex(x => x.BaoLuc).HasDatabaseName("IX_SuCoMon_ChoDuyet").HasFilter("[TrangThai] = 'CHO_DUYET'");
        incident.HasOne<PhieuCheBien>().WithMany().HasForeignKey(x => x.PhieuCheBienId).OnDelete(DeleteBehavior.NoAction);

        var request = modelBuilder.Entity<YeuCauFulfillmentDaXuLy>();
        request.ToTable("YeuCauDaXuLy");
        request.HasKey(x => new { x.Operation, x.ActorScope, x.Key });
        request.Property(x => x.Operation).HasMaxLength(40).IsUnicode(false).IsRequired();
        request.Property(x => x.RequestHash).HasColumnType("binary(32)").IsRequired();

    }
}
