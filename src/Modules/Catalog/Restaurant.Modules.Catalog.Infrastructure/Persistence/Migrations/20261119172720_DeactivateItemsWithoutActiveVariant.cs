using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeactivateItemsWithoutActiveVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE m SET HoatDong = 0
                FROM catalog.Mon AS m
                WHERE m.HoatDong = 1 AND NOT EXISTS (
                    SELECT 1 FROM catalog.BienThe AS v WHERE v.MonId = m.Id AND v.HoatDong = 1
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restoring availability requires an operator to select a valid variant and price.
        }
    }
}
