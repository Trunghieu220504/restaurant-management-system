using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Fulfillment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fulfillment");

            migrationBuilder.CreateTable(
                name: "Inbox",
                schema: "fulfillment",
                columns: table => new
                {
                    Consumer = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => new { x.Consumer, x.EventId });
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "fulfillment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    AggregateVersion = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                    table.CheckConstraint("CK_Outbox_LanThu", "[Attempts] >= 0");
                    table.CheckConstraint("CK_Outbox_PhienBan", "[AggregateVersion] > 0 AND [SchemaVersion] > 0");
                });

            migrationBuilder.CreateTable(
                name: "PhieuCheBien",
                schema: "fulfillment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChiTietDonId = table.Column<long>(type: "bigint", nullable: false),
                    Lan = table.Column<int>(type: "int", nullable: false),
                    TramChot = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    PhieuGocId = table.Column<long>(type: "bigint", nullable: true),
                    SuCoNguonId = table.Column<long>(type: "bigint", nullable: true),
                    TrangThai = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NguoiBatDauId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BatDauLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HoanThanhLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiHoanThanhId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GiaoLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiGiaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuCheBien", x => x.Id);
                    table.CheckConstraint("CK_PhieuCheBien_LamLai", "([Lan] = 1 AND [PhieuGocId] IS NULL AND [SuCoNguonId] IS NULL) OR ([Lan] > 1 AND [PhieuGocId] IS NOT NULL AND [SuCoNguonId] IS NOT NULL)");
                    table.CheckConstraint("CK_PhieuCheBien_Lan", "[Lan] > 0");
                    table.CheckConstraint("CK_PhieuCheBien_NguoiThaoTac", "([NguoiBatDauId] IS NULL AND [BatDauLuc] IS NULL OR [NguoiBatDauId] IS NOT NULL AND [BatDauLuc] IS NOT NULL) AND ([NguoiHoanThanhId] IS NULL AND [HoanThanhLuc] IS NULL OR [NguoiHoanThanhId] IS NOT NULL AND [HoanThanhLuc] IS NOT NULL) AND ([NguoiGiaoId] IS NULL AND [GiaoLuc] IS NULL OR [NguoiGiaoId] IS NOT NULL AND [GiaoLuc] IS NOT NULL)");
                    table.CheckConstraint("CK_PhieuCheBien_TienDo", "([TrangThai] <> 'MOI' OR ([BatDauLuc] IS NULL AND [HoanThanhLuc] IS NULL AND [GiaoLuc] IS NULL)) AND ([TrangThai] <> 'DANG_LAM' OR [BatDauLuc] IS NOT NULL) AND ([TrangThai] <> 'SAN_SANG' OR [HoanThanhLuc] IS NOT NULL) AND ([TrangThai] <> 'DA_GIAO' OR ([HoanThanhLuc] IS NOT NULL AND [GiaoLuc] IS NOT NULL))");
                    table.CheckConstraint("CK_PhieuCheBien_Tram", "[TramChot] IN ('BEP','BAR')");
                    table.CheckConstraint("CK_PhieuCheBien_TrangThai", "[TrangThai] IN ('MOI','DANG_LAM','SAN_SANG','DA_GIAO','DA_HUY','LOI')");
                    table.ForeignKey(
                        name: "FK_PhieuCheBien_PhieuCheBien_PhieuGocId",
                        column: x => x.PhieuGocId,
                        principalSchema: "fulfillment",
                        principalTable: "PhieuCheBien",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SuCoMon",
                schema: "fulfillment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhieuCheBienId = table.Column<long>(type: "bigint", nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    BaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NguoiBaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrangThai = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    QuyetDinh = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    NguoiDuyetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DuyetLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LyDoDuyet = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    KhoaYeuCau = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuCoMon", x => x.Id);
                    table.CheckConstraint("CK_SuCoMon_Duyet", "([TrangThai] = 'CHO_DUYET' AND [QuyetDinh] IS NULL AND [NguoiDuyetId] IS NULL AND [DuyetLuc] IS NULL AND [LyDoDuyet] IS NULL) OR ([TrangThai] = 'DA_DUYET' AND [QuyetDinh] IS NOT NULL AND [NguoiDuyetId] IS NOT NULL AND [DuyetLuc] IS NOT NULL) OR ([TrangThai] = 'TU_CHOI' AND [QuyetDinh] IS NULL AND [NguoiDuyetId] IS NOT NULL AND [DuyetLuc] IS NOT NULL)");
                    table.CheckConstraint("CK_SuCoMon_LyDo", "LEN(LTRIM(RTRIM([LyDo]))) > 0");
                    table.CheckConstraint("CK_SuCoMon_QuyetDinh", "[QuyetDinh] IS NULL OR [QuyetDinh] IN ('LAM_LAI','HUY_VAN_THU','HUY_GIAM_NGHIA_VU')");
                    table.CheckConstraint("CK_SuCoMon_TrangThai", "[TrangThai] IN ('CHO_DUYET','DA_DUYET','TU_CHOI')");
                    table.ForeignKey(
                        name: "FK_SuCoMon_PhieuCheBien_PhieuCheBienId",
                        column: x => x.PhieuCheBienId,
                        principalSchema: "fulfillment",
                        principalTable: "PhieuCheBien",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_ChoGui",
                schema: "fulfillment",
                table: "Outbox",
                column: "NextAttemptAt",
                filter: "[ProcessedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Outbox_SuKien",
                schema: "fulfillment",
                table: "Outbox",
                columns: new[] { "Type", "AggregateId", "AggregateVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhieuCheBien_ChiTietDonId_Lan",
                schema: "fulfillment",
                table: "PhieuCheBien",
                columns: new[] { "ChiTietDonId", "Lan" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhieuCheBien_HangDoi",
                schema: "fulfillment",
                table: "PhieuCheBien",
                columns: new[] { "TramChot", "TrangThai", "TaoLuc" });

            migrationBuilder.CreateIndex(
                name: "UX_PhieuCheBien_PhieuGoc",
                schema: "fulfillment",
                table: "PhieuCheBien",
                column: "PhieuGocId",
                unique: true,
                filter: "[PhieuGocId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PhieuCheBien_SuCoNguon",
                schema: "fulfillment",
                table: "PhieuCheBien",
                column: "SuCoNguonId",
                unique: true,
                filter: "[SuCoNguonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SuCoMon_ChoDuyet",
                schema: "fulfillment",
                table: "SuCoMon",
                column: "BaoLuc",
                filter: "[TrangThai] = 'CHO_DUYET'");

            migrationBuilder.CreateIndex(
                name: "IX_SuCoMon_KhoaYeuCau",
                schema: "fulfillment",
                table: "SuCoMon",
                column: "KhoaYeuCau",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuCoMon_PhieuCheBienId",
                schema: "fulfillment",
                table: "SuCoMon",
                column: "PhieuCheBienId");

            migrationBuilder.AddForeignKey(
                name: "FK_PhieuCheBien_SuCoMon_SuCoNguonId",
                schema: "fulfillment",
                table: "PhieuCheBien",
                column: "SuCoNguonId",
                principalSchema: "fulfillment",
                principalTable: "SuCoMon",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PhieuCheBien_SuCoMon_SuCoNguonId",
                schema: "fulfillment",
                table: "PhieuCheBien");

            migrationBuilder.DropTable(
                name: "Inbox",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "SuCoMon",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "PhieuCheBien",
                schema: "fulfillment");
        }
    }
}
