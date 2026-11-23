using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.SalesDay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalDaySettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "salesday",
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
                name: "TongHopNgay",
                schema: "salesday",
                columns: table => new
                {
                    DayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotVersion = table.Column<int>(type: "int", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LateClose = table.Column<bool>(type: "bit", nullable: false),
                    LateReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OpeningCash = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    CountedCash = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    DifferenceReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DrawerPurchases = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    Receipts = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    Refunds = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    Charges = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    Adjustments = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    Waivers = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    NetRevenue = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    RoundingDifference = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    GrossProfit = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TongHopNgay", x => x.DayId);
                    table.ForeignKey(
                        name: "FK_TongHopNgay_NgayBan_DayId",
                        column: x => x.DayId,
                        principalSchema: "salesday",
                        principalTable: "NgayBan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TongHopNgayPhuongThuc",
                schema: "salesday",
                columns: table => new
                {
                    DayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Receipts = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    Refunds = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false),
                    Net = table.Column<decimal>(type: "decimal(28,0)", precision: 28, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TongHopNgayPhuongThuc", x => new { x.DayId, x.Method });
                    table.ForeignKey(
                        name: "FK_TongHopNgayPhuongThuc_TongHopNgay_DayId",
                        column: x => x.DayId,
                        principalSchema: "salesday",
                        principalTable: "TongHopNgay",
                        principalColumn: "DayId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_ChoGui",
                schema: "salesday",
                table: "Outbox",
                column: "NextAttemptAt",
                filter: "[ProcessedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Outbox_SuKien",
                schema: "salesday",
                table: "Outbox",
                columns: new[] { "Type", "AggregateId", "AggregateVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "salesday");

            migrationBuilder.DropTable(
                name: "TongHopNgayPhuongThuc",
                schema: "salesday");

            migrationBuilder.DropTable(
                name: "TongHopNgay",
                schema: "salesday");
        }
    }
}
