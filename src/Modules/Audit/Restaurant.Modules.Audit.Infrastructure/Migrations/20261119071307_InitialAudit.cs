using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "NhatKy",
                schema: "audit",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceModule = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessDayId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhatKy", x => x.EventId);
                    table.CheckConstraint("CK_NhatKy_Utc", "DATEPART(TZOFFSET,[OccurredAtUtc])=0 AND DATEPART(TZOFFSET,[RecordedAtUtc])=0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKy_ActorId_OccurredAtUtc_EventId",
                schema: "audit",
                table: "NhatKy",
                columns: new[] { "ActorId", "OccurredAtUtc", "EventId" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKy_EntityType_EntityId_OccurredAtUtc_EventId",
                schema: "audit",
                table: "NhatKy",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc", "EventId" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKy_OccurredAtUtc_EventId",
                schema: "audit",
                table: "NhatKy",
                columns: new[] { "OccurredAtUtc", "EventId" },
                descending: new bool[0]);

            // EXEC keeps CREATE TRIGGER first in its batch, including inside an idempotent script.
            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER [audit].[TR_NhatKy_AppendOnly]
                ON [audit].[NhatKy] AFTER UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, ''Audit entries are append-only.'', 1;
                END;');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NhatKy",
                schema: "audit");
        }
    }
}
