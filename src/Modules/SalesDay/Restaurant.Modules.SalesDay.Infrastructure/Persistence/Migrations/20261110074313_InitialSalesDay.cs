using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.SalesDay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSalesDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "salesday");

            migrationBuilder.CreateTable(
                name: "NgayBan",
                schema: "salesday",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OpenedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpeningCash = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ActiveSlot = table.Column<int>(type: "int", nullable: true),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NgayBan", x => x.Id);
                    table.CheckConstraint("CK_NgayBan_Cash", "[OpeningCash] >= 0");
                    table.CheckConstraint("CK_NgayBan_State", "([Status] IN ('DANG_BAN','CHO_CHOT') AND [ActiveSlot] = 1 AND [ActiveSlot] IS NOT NULL) OR ([Status] = 'DA_CHOT' AND [ActiveSlot] IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NgayBan_ActiveSlot",
                schema: "salesday",
                table: "NgayBan",
                column: "ActiveSlot",
                unique: true,
                filter: "[ActiveSlot] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NgayBan_BusinessDate",
                schema: "salesday",
                table: "NgayBan",
                column: "BusinessDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NgayBan_RequestKey",
                schema: "salesday",
                table: "NgayBan",
                column: "RequestKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NgayBan",
                schema: "salesday");
        }
    }
}
