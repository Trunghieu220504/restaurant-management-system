using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Fulfillment.Infrastructure.Persistence.Migrations;

public partial class AddFulfillmentIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "YeuCauDaXuLy", schema: "fulfillment",
            columns: table => new
            {
                Operation = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                ActorScope = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                TicketId = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_YeuCauDaXuLy", x => new { x.Operation, x.ActorScope, x.Key }));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "YeuCauDaXuLy", schema: "fulfillment");
}
