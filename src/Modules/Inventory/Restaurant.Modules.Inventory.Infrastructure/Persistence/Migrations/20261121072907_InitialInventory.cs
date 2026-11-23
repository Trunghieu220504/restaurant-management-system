using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "DonVi",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ma = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LoaiDaiLuong = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonVi", x => x.Id);
                    table.CheckConstraint("CK_DonVi_Loai", "[LoaiDaiLuong] IN ('KHOI_LUONG','THE_TICH','DEM')");
                });

            migrationBuilder.CreateTable(
                name: "NhaCungCap",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ten = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DienThoai = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DiaChi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HoatDong = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhaCungCap", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NguyenLieu",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ma = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DonViChuanId = table.Column<long>(type: "bigint", nullable: false),
                    CheDo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LuuTru = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NguyenLieu", x => x.Id);
                    table.CheckConstraint("CK_NguyenLieu_CheDo", "[CheDo] IN ('THEO_DOI_TON','TINH_KHI_MUA')");
                    table.ForeignKey(
                        name: "FK_NguyenLieu_DonVi_DonViChuanId",
                        column: x => x.DonViChuanId,
                        principalSchema: "inventory",
                        principalTable: "DonVi",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PhieuNhap",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NhaCungCapId = table.Column<long>(type: "bigint", nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TraBangTienKet = table.Column<bool>(type: "bit", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TaoLuc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NguoiLapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KhoaYeuCau = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DauVanTay = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuNhap", x => x.Id);
                    table.CheckConstraint("CK_PhieuNhap_TrangThai", "[TrangThai] IN ('NHAP','HIEU_LUC','HUY')");
                    table.ForeignKey(
                        name: "FK_PhieuNhap_NhaCungCap_NhaCungCapId",
                        column: x => x.NhaCungCapId,
                        principalSchema: "inventory",
                        principalTable: "NhaCungCap",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KiemKeTon",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NguyenLieuId = table.Column<long>(type: "bigint", nullable: false),
                    CheDoChot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TonDau = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GiaTriTonDau = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    SoDemNhap = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SoDemDuyet = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    NguoiDuyetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DuyetLuc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ChotLuc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GiaTriTonCuoi = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KiemKeTon", x => x.Id);
                    table.CheckConstraint("CK_KiemKeTon_Ton", "[TonDau] >= 0 AND [GiaTriTonDau] >= 0 AND ([SoDemDuyet] IS NULL OR [SoDemDuyet] >= 0)");
                    table.ForeignKey(
                        name: "FK_KiemKeTon_NguyenLieu_NguyenLieuId",
                        column: x => x.NguyenLieuId,
                        principalSchema: "inventory",
                        principalTable: "NguyenLieu",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QuyDoi",
                schema: "inventory",
                columns: table => new
                {
                    NguyenLieuId = table.Column<long>(type: "bigint", nullable: false),
                    DonViNhapId = table.Column<long>(type: "bigint", nullable: false),
                    DonViChuanId = table.Column<long>(type: "bigint", nullable: false),
                    HeSoVeChuan = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuyDoi", x => new { x.NguyenLieuId, x.DonViNhapId });
                    table.CheckConstraint("CK_QuyDoi_HeSo", "[HeSoVeChuan] > 0");
                    table.ForeignKey(
                        name: "FK_QuyDoi_DonVi_DonViChuanId",
                        column: x => x.DonViChuanId,
                        principalSchema: "inventory",
                        principalTable: "DonVi",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuyDoi_DonVi_DonViNhapId",
                        column: x => x.DonViNhapId,
                        principalSchema: "inventory",
                        principalTable: "DonVi",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuyDoi_NguyenLieu_NguyenLieuId",
                        column: x => x.NguyenLieuId,
                        principalSchema: "inventory",
                        principalTable: "NguyenLieu",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChiTietPhieuNhap",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhieuNhapId = table.Column<long>(type: "bigint", nullable: false),
                    NguyenLieuId = table.Column<long>(type: "bigint", nullable: false),
                    DonViId = table.Column<long>(type: "bigint", nullable: false),
                    SoLuong = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DonViChuanChotId = table.Column<long>(type: "bigint", nullable: false),
                    HeSoChot = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    DonGiaNhap = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    CheDoChot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SoLuongChuan = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ThanhTien = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietPhieuNhap", x => x.Id);
                    table.CheckConstraint("CK_ChiTietPhieuNhap_CheDo", "[CheDoChot] IN ('THEO_DOI_TON','TINH_KHI_MUA')");
                    table.CheckConstraint("CK_ChiTietPhieuNhap_Gia", "[DonGiaNhap] >= 0 AND [ThanhTien] >= 0");
                    table.CheckConstraint("CK_ChiTietPhieuNhap_SoLuong", "[SoLuong] > 0 AND [HeSoChot] > 0 AND [SoLuongChuan] > 0");
                    table.ForeignKey(
                        name: "FK_ChiTietPhieuNhap_DonVi_DonViChuanChotId",
                        column: x => x.DonViChuanChotId,
                        principalSchema: "inventory",
                        principalTable: "DonVi",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChiTietPhieuNhap_NguyenLieu_NguyenLieuId",
                        column: x => x.NguyenLieuId,
                        principalSchema: "inventory",
                        principalTable: "NguyenLieu",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChiTietPhieuNhap_PhieuNhap_PhieuNhapId",
                        column: x => x.PhieuNhapId,
                        principalSchema: "inventory",
                        principalTable: "PhieuNhap",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChiTietPhieuNhap_QuyDoi_NguyenLieuId_DonViId",
                        columns: x => new { x.NguyenLieuId, x.DonViId },
                        principalSchema: "inventory",
                        principalTable: "QuyDoi",
                        principalColumns: new[] { "NguyenLieuId", "DonViNhapId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietPhieuNhap_DonViChuanChotId",
                schema: "inventory",
                table: "ChiTietPhieuNhap",
                column: "DonViChuanChotId");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietPhieuNhap_NguyenLieuId_DonViId",
                schema: "inventory",
                table: "ChiTietPhieuNhap",
                columns: new[] { "NguyenLieuId", "DonViId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietPhieuNhap_PhieuNhapId",
                schema: "inventory",
                table: "ChiTietPhieuNhap",
                column: "PhieuNhapId");

            migrationBuilder.CreateIndex(
                name: "IX_DonVi_Ma",
                schema: "inventory",
                table: "DonVi",
                column: "Ma",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KiemKeTon_NgayBanId_NguyenLieuId",
                schema: "inventory",
                table: "KiemKeTon",
                columns: new[] { "NgayBanId", "NguyenLieuId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KiemKeTon_NguyenLieuId",
                schema: "inventory",
                table: "KiemKeTon",
                column: "NguyenLieuId");

            migrationBuilder.CreateIndex(
                name: "IX_NguyenLieu_DonViChuanId",
                schema: "inventory",
                table: "NguyenLieu",
                column: "DonViChuanId");

            migrationBuilder.CreateIndex(
                name: "IX_NguyenLieu_Ma",
                schema: "inventory",
                table: "NguyenLieu",
                column: "Ma",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhieuNhap_KhoaYeuCau",
                schema: "inventory",
                table: "PhieuNhap",
                column: "KhoaYeuCau",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhieuNhap_NgayBanId_TrangThai",
                schema: "inventory",
                table: "PhieuNhap",
                columns: new[] { "NgayBanId", "TrangThai" });

            migrationBuilder.CreateIndex(
                name: "IX_PhieuNhap_NhaCungCapId",
                schema: "inventory",
                table: "PhieuNhap",
                column: "NhaCungCapId");

            migrationBuilder.CreateIndex(
                name: "IX_QuyDoi_DonViChuanId",
                schema: "inventory",
                table: "QuyDoi",
                column: "DonViChuanId");

            migrationBuilder.CreateIndex(
                name: "IX_QuyDoi_DonViNhapId",
                schema: "inventory",
                table: "QuyDoi",
                column: "DonViNhapId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChiTietPhieuNhap",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "KiemKeTon",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "PhieuNhap",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "QuyDoi",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "NhaCungCap",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "NguyenLieu",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "DonVi",
                schema: "inventory");
        }
    }
}
