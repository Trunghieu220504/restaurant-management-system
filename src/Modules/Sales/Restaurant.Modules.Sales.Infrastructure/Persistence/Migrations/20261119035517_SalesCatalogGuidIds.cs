using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Sales.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Catalog identifies dishes, variants, options and option groups by Guid (and its Contracts say so), but the first
    /// Sales migration stored them as bigint. SQL Server cannot convert bigint to uniqueidentifier, so the four columns
    /// are dropped and added again. That is only safe while no order exists: no endpoint could create one yet, and the
    /// guard below stops the migration (instead of destroying data) if a row is found anyway.
    /// </summary>
    public partial class SalesCatalogGuidIds : Migration
    {
        private const string Guard = @"
IF EXISTS (SELECT 1 FROM [sales].[ChiTietDon]) OR EXISTS (SELECT 1 FROM [sales].[TuyChonDaDat])
    THROW 50000, 'sales.ChiTietDon / sales.TuyChonDaDat already hold rows; converting the catalog id columns would lose their references.', 1;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Guard);

            migrationBuilder.DropPrimaryKey(name: "PK_TuyChonDaDat", schema: "sales", table: "TuyChonDaDat");
            migrationBuilder.DropIndex(name: "IX_TuyChonDaDat_TuyChonId", schema: "sales", table: "TuyChonDaDat");
            migrationBuilder.DropIndex(name: "IX_ChiTietDon_BienTheId", schema: "sales", table: "ChiTietDon");
            migrationBuilder.DropIndex(name: "IX_ChiTietDon_MonId", schema: "sales", table: "ChiTietDon");

            migrationBuilder.DropColumn(name: "TuyChonId", schema: "sales", table: "TuyChonDaDat");
            migrationBuilder.DropColumn(name: "NhomTuyChonId", schema: "sales", table: "TuyChonDaDat");
            migrationBuilder.DropColumn(name: "MonId", schema: "sales", table: "ChiTietDon");
            migrationBuilder.DropColumn(name: "BienTheId", schema: "sales", table: "ChiTietDon");

            migrationBuilder.AddColumn<Guid>(name: "TuyChonId", schema: "sales", table: "TuyChonDaDat",
                type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>(name: "NhomTuyChonId", schema: "sales", table: "TuyChonDaDat",
                type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>(name: "MonId", schema: "sales", table: "ChiTietDon",
                type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>(name: "BienTheId", schema: "sales", table: "ChiTietDon",
                type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);

            migrationBuilder.AddPrimaryKey(name: "PK_TuyChonDaDat", schema: "sales", table: "TuyChonDaDat",
                columns: new[] { "ChiTietDonId", "TuyChonId" });
            migrationBuilder.CreateIndex(name: "IX_TuyChonDaDat_TuyChonId", schema: "sales", table: "TuyChonDaDat", column: "TuyChonId");
            migrationBuilder.CreateIndex(name: "IX_ChiTietDon_BienTheId", schema: "sales", table: "ChiTietDon", column: "BienTheId");
            migrationBuilder.CreateIndex(name: "IX_ChiTietDon_MonId", schema: "sales", table: "ChiTietDon", column: "MonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Guard);

            migrationBuilder.DropPrimaryKey(name: "PK_TuyChonDaDat", schema: "sales", table: "TuyChonDaDat");
            migrationBuilder.DropIndex(name: "IX_TuyChonDaDat_TuyChonId", schema: "sales", table: "TuyChonDaDat");
            migrationBuilder.DropIndex(name: "IX_ChiTietDon_BienTheId", schema: "sales", table: "ChiTietDon");
            migrationBuilder.DropIndex(name: "IX_ChiTietDon_MonId", schema: "sales", table: "ChiTietDon");

            migrationBuilder.DropColumn(name: "TuyChonId", schema: "sales", table: "TuyChonDaDat");
            migrationBuilder.DropColumn(name: "NhomTuyChonId", schema: "sales", table: "TuyChonDaDat");
            migrationBuilder.DropColumn(name: "MonId", schema: "sales", table: "ChiTietDon");
            migrationBuilder.DropColumn(name: "BienTheId", schema: "sales", table: "ChiTietDon");

            migrationBuilder.AddColumn<long>(name: "TuyChonId", schema: "sales", table: "TuyChonDaDat",
                type: "bigint", nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>(name: "NhomTuyChonId", schema: "sales", table: "TuyChonDaDat",
                type: "bigint", nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>(name: "MonId", schema: "sales", table: "ChiTietDon",
                type: "bigint", nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>(name: "BienTheId", schema: "sales", table: "ChiTietDon",
                type: "bigint", nullable: false, defaultValue: 0L);

            migrationBuilder.AddPrimaryKey(name: "PK_TuyChonDaDat", schema: "sales", table: "TuyChonDaDat",
                columns: new[] { "ChiTietDonId", "TuyChonId" });
            migrationBuilder.CreateIndex(name: "IX_TuyChonDaDat_TuyChonId", schema: "sales", table: "TuyChonDaDat", column: "TuyChonId");
            migrationBuilder.CreateIndex(name: "IX_ChiTietDon_BienTheId", schema: "sales", table: "ChiTietDon", column: "BienTheId");
            migrationBuilder.CreateIndex(name: "IX_ChiTietDon_MonId", schema: "sales", table: "ChiTietDon", column: "MonId");
        }
    }
}
