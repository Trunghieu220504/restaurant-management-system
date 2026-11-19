using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Sales.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "sales",
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

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_ChoGui",
                schema: "sales",
                table: "Outbox",
                column: "NextAttemptAt",
                filter: "[ProcessedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Outbox_SuKien",
                schema: "sales",
                table: "Outbox",
                columns: new[] { "Type", "AggregateId", "AggregateVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "sales");
        }
    }
}
