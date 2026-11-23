using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StockCountValuation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "CK_KiemKeTon_Ton", schema: "inventory", table: "KiemKeTon");

            migrationBuilder.AlterColumn<decimal>(
                name: "TonDau",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "SoDemNhap",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SoDemDuyet",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "GiaTriTonDau",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "GiaTriTonCuoi",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DieuChinhGiaTriChot",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DieuChinhLuongChot",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DonViChotId",
                schema: "inventory",
                table: "KiemKeTon",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GiaBinhQuanChot",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GiaTriNhapChot",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GiaVonChot",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LuongDungChot",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NguoiNhapId",
                schema: "inventory",
                table: "KiemKeTon",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NhapLuc",
                schema: "inventory",
                table: "KiemKeTon",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TongNhapChot",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DieuChinhTon",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NgayBanGocId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NguyenLieuId = table.Column<long>(type: "bigint", nullable: false),
                    SoLuongChuan = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    GiaTriDieuChinh = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    NguoiDuyetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaoLuc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    KhoaYeuCau = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DieuChinhTon", x => x.Id);
                    table.CheckConstraint("CK_DieuChinhTon_Sign", "[SoLuongChuan] <> 0 AND (SIGN([SoLuongChuan]) = SIGN([GiaTriDieuChinh]) OR [GiaTriDieuChinh] = 0)");
                    table.ForeignKey(
                        name: "FK_DieuChinhTon_NguyenLieu_NguyenLieuId",
                        column: x => x.NguyenLieuId,
                        principalSchema: "inventory",
                        principalTable: "NguyenLieu",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DieuChinhTon_KhoaYeuCau",
                schema: "inventory",
                table: "DieuChinhTon",
                column: "KhoaYeuCau",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DieuChinhTon_NgayBanId_NguyenLieuId",
                schema: "inventory",
                table: "DieuChinhTon",
                columns: new[] { "NgayBanId", "NguyenLieuId" });

            migrationBuilder.CreateIndex(
                name: "IX_DieuChinhTon_NguyenLieuId",
                schema: "inventory",
                table: "DieuChinhTon",
                column: "NguyenLieuId");
            RestoreCountConstraint(migrationBuilder);
        }

        private static void RestoreCountConstraint(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddCheckConstraint(name: "CK_KiemKeTon_Ton", schema: "inventory", table: "KiemKeTon",
                sql: "[TonDau] >= 0 AND [GiaTriTonDau] >= 0 AND ([SoDemDuyet] IS NULL OR [SoDemDuyet] >= 0)");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "CK_KiemKeTon_Ton", schema: "inventory", table: "KiemKeTon");

            migrationBuilder.DropTable(
                name: "DieuChinhTon",
                schema: "inventory");

            migrationBuilder.DropColumn(
                name: "DieuChinhGiaTriChot",
                schema: "inventory",
                table: "KiemKeTon");

            migrationBuilder.DropColumn(
                name: "DieuChinhLuongChot",
                schema: "inventory",
                table: "KiemKeTon");

            migrationBuilder.DropColumn(
                name: "DonViChotId",
                schema: "inventory",
                table: "KiemKeTon");

            migrationBuilder.DropColumn(
                name: "GiaBinhQuanChot",
                schema: "inventory",
                table: "KiemKeTon");

            migrationBuilder.DropColumn(
                name: "GiaTriNhapChot",
                schema: "inventory",
                table: "KiemKeTon");

            migrationBuilder.DropColumn(
                name: "GiaVonChot",
                schema: "inventory",
                table: "KiemKeTon");

            migrationBuilder.DropColumn(
                name: "LuongDungChot",
                schema: "inventory",
                table: "KiemKeTon");

            migrationBuilder.DropColumn(
                name: "NguoiNhapId",
                schema: "inventory",
                table: "KiemKeTon");

            migrationBuilder.DropColumn(
                name: "NhapLuc",
                schema: "inventory",
                table: "KiemKeTon");

            migrationBuilder.DropColumn(
                name: "TongNhapChot",
                schema: "inventory",
                table: "KiemKeTon");

            migrationBuilder.AlterColumn<decimal>(
                name: "TonDau",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(28,8)",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "SoDemNhap",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(28,8)",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SoDemDuyet",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(28,8)",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "GiaTriTonDau",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(18,0)",
                precision: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(28,8)",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "GiaTriTonCuoi",
                schema: "inventory",
                table: "KiemKeTon",
                type: "decimal(18,0)",
                precision: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(28,8)",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);
            RestoreCountConstraint(migrationBuilder);
        }
    }
}
