using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Seating.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatingIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YeuCauDaXuLy",
                schema: "seating",
                columns: table => new
                {
                    Operation = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    ActorScope = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    ResultReference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauDaXuLy", x => new { x.Operation, x.ActorScope, x.Key });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YeuCauDaXuLy",
                schema: "seating");
        }
    }
}
