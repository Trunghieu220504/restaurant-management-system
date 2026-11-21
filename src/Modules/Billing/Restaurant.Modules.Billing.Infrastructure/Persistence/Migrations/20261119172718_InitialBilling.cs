using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "HoaDon",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonHangId = table.Column<long>(type: "bigint", nullable: false),
                    MaHoaDon = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    NgayBanLapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DongLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoaDon", x => x.Id);
                    table.CheckConstraint("CK_HoaDon_DongLuc", "[DongLuc] IS NULL OR [DongLuc] >= [TaoLuc]");
                });

            migrationBuilder.CreateTable(
                name: "DieuChinhNghiaVu",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HoaDonId = table.Column<long>(type: "bigint", nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SuCoMonId = table.Column<long>(type: "bigint", nullable: true),
                    GiaoDichGocId = table.Column<long>(type: "bigint", nullable: true),
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NguoiDuyetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KhoaYeuCau = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DieuChinhNghiaVu", x => x.Id);
                    table.CheckConstraint("CK_DieuChinhNghiaVu_GiaoDichGoc", "[GiaoDichGocId] IS NULL OR [GiaoDichGocId] <> [Id]");
                    table.CheckConstraint("CK_DieuChinhNghiaVu_SoTien", "[SoTien] <> 0");
                    table.ForeignKey(
                        name: "FK_DieuChinhNghiaVu_DieuChinhNghiaVu_GiaoDichGocId",
                        column: x => x.GiaoDichGocId,
                        principalSchema: "billing",
                        principalTable: "DieuChinhNghiaVu",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DieuChinhNghiaVu_HoaDon_HoaDonId",
                        column: x => x.HoaDonId,
                        principalSchema: "billing",
                        principalTable: "HoaDon",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DongHoaDon",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HoaDonId = table.Column<long>(type: "bigint", nullable: false),
                    ChiTietDonId = table.Column<long>(type: "bigint", nullable: false),
                    TenMonChot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NoiDungTuyChonChot = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ThanhTienGoc = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DongHoaDon", x => x.Id);
                    table.CheckConstraint("CK_DongHoaDon_ThanhTienGoc", "[ThanhTienGoc] >= 0");
                    table.ForeignKey(
                        name: "FK_DongHoaDon_HoaDon_HoaDonId",
                        column: x => x.HoaDonId,
                        principalSchema: "billing",
                        principalTable: "HoaDon",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MienThu",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HoaDonId = table.Column<long>(type: "bigint", nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NguoiDuyetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KhoaYeuCau = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MienThu", x => x.Id);
                    table.CheckConstraint("CK_MienThu_SoTien", "[SoTien] > 0");
                    table.ForeignKey(
                        name: "FK_MienThu_HoaDon_HoaDonId",
                        column: x => x.HoaDonId,
                        principalSchema: "billing",
                        principalTable: "HoaDon",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ThanhToan",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HoaDonId = table.Column<long>(type: "bigint", nullable: false),
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhuongThuc = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    TrangThai = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    MaThamChieuNganHang = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    TienKhachDua = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: true),
                    TienThua = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: true),
                    NguoiThuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    XacNhanLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KhoaYeuCau = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThanhToan", x => x.Id);
                    table.CheckConstraint("CK_ThanhToan_PhuongThuc", "([PhuongThuc] = 'TIEN_MAT' AND [TienKhachDua] IS NOT NULL AND [TienThua] IS NOT NULL AND [TienKhachDua] >= [SoTien] AND [TienThua] = [TienKhachDua] - [SoTien]) OR ([PhuongThuc] = 'CHUYEN_KHOAN' AND [TienKhachDua] IS NULL AND [TienThua] IS NULL)");
                    table.CheckConstraint("CK_ThanhToan_SoTien", "[SoTien] > 0");
                    table.CheckConstraint("CK_ThanhToan_TrangThai", "[TrangThai] IN ('CHO_XAC_NHAN','THANH_CONG','HUY')");
                    table.ForeignKey(
                        name: "FK_ThanhToan_HoaDon_HoaDonId",
                        column: x => x.HoaDonId,
                        principalSchema: "billing",
                        principalTable: "HoaDon",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HoanTien",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ThanhToanId = table.Column<long>(type: "bigint", nullable: false),
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    PhuongThuc = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    TrangThai = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    NguoiDeNghiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NguoiDuyetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NguoiChiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaChiTra = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChiLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KhoaYeuCau = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoanTien", x => x.Id);
                    table.CheckConstraint("CK_HoanTien_PhuongThuc", "[PhuongThuc] IN ('TIEN_MAT','CHUYEN_KHOAN')");
                    table.CheckConstraint("CK_HoanTien_SoTien", "[SoTien] > 0");
                    table.CheckConstraint("CK_HoanTien_TrangThai", "[TrangThai] IN ('CHO_DUYET','DA_DUYET','DANG_CHI','CHO_DOI_SOAT','THANH_CONG','TU_CHOI','HUY')");
                    table.ForeignKey(
                        name: "FK_HoanTien_ThanhToan_ThanhToanId",
                        column: x => x.ThanhToanId,
                        principalSchema: "billing",
                        principalTable: "ThanhToan",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChungTu",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HoaDonId = table.Column<long>(type: "bigint", nullable: false),
                    SoChungTu = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Loai = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ThanhToanId = table.Column<long>(type: "bigint", nullable: true),
                    HoanTienId = table.Column<long>(type: "bigint", nullable: true),
                    NoiDungChot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NguoiLapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChungTu", x => x.Id);
                    table.CheckConstraint("CK_ChungTu_Loai", "([Loai] = 'HOA_DON' AND [ThanhToanId] IS NULL AND [HoanTienId] IS NULL) OR ([Loai] = 'PHIEU_THU' AND [ThanhToanId] IS NOT NULL AND [HoanTienId] IS NULL) OR ([Loai] = 'PHIEU_HOAN' AND [ThanhToanId] IS NULL AND [HoanTienId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ChungTu_HoaDon_HoaDonId",
                        column: x => x.HoaDonId,
                        principalSchema: "billing",
                        principalTable: "HoaDon",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChungTu_HoanTien_HoanTienId",
                        column: x => x.HoanTienId,
                        principalSchema: "billing",
                        principalTable: "HoanTien",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChungTu_ThanhToan_ThanhToanId",
                        column: x => x.ThanhToanId,
                        principalSchema: "billing",
                        principalTable: "ThanhToan",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChungTu_HoaDonId",
                schema: "billing",
                table: "ChungTu",
                column: "HoaDonId");

            migrationBuilder.CreateIndex(
                name: "IX_ChungTu_HoanTienId",
                schema: "billing",
                table: "ChungTu",
                column: "HoanTienId");

            migrationBuilder.CreateIndex(
                name: "IX_ChungTu_NguoiLapId",
                schema: "billing",
                table: "ChungTu",
                column: "NguoiLapId");

            migrationBuilder.CreateIndex(
                name: "IX_ChungTu_SoChungTu",
                schema: "billing",
                table: "ChungTu",
                column: "SoChungTu",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChungTu_ThanhToanId",
                schema: "billing",
                table: "ChungTu",
                column: "ThanhToanId");

            migrationBuilder.CreateIndex(
                name: "IX_DieuChinhNghiaVu_GiaoDichGocId",
                schema: "billing",
                table: "DieuChinhNghiaVu",
                column: "GiaoDichGocId");

            migrationBuilder.CreateIndex(
                name: "IX_DieuChinhNghiaVu_HoaDonId",
                schema: "billing",
                table: "DieuChinhNghiaVu",
                column: "HoaDonId");

            migrationBuilder.CreateIndex(
                name: "IX_DieuChinhNghiaVu_KhoaYeuCau",
                schema: "billing",
                table: "DieuChinhNghiaVu",
                column: "KhoaYeuCau",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DieuChinhNghiaVu_NgayBanId",
                schema: "billing",
                table: "DieuChinhNghiaVu",
                column: "NgayBanId");

            migrationBuilder.CreateIndex(
                name: "IX_DieuChinhNghiaVu_NguoiDuyetId",
                schema: "billing",
                table: "DieuChinhNghiaVu",
                column: "NguoiDuyetId");

            migrationBuilder.CreateIndex(
                name: "IX_DieuChinhNghiaVu_SuCoMonId",
                schema: "billing",
                table: "DieuChinhNghiaVu",
                column: "SuCoMonId");

            migrationBuilder.CreateIndex(
                name: "IX_DongHoaDon_ChiTietDonId",
                schema: "billing",
                table: "DongHoaDon",
                column: "ChiTietDonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DongHoaDon_HoaDonId",
                schema: "billing",
                table: "DongHoaDon",
                column: "HoaDonId");

            migrationBuilder.CreateIndex(
                name: "IX_HoaDon_DonHangId",
                schema: "billing",
                table: "HoaDon",
                column: "DonHangId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HoaDon_MaHoaDon",
                schema: "billing",
                table: "HoaDon",
                column: "MaHoaDon",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HoaDon_NgayBanLapId",
                schema: "billing",
                table: "HoaDon",
                column: "NgayBanLapId");

            migrationBuilder.CreateIndex(
                name: "IX_HoanTien_KhoaYeuCau",
                schema: "billing",
                table: "HoanTien",
                column: "KhoaYeuCau",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HoanTien_NgayBanId",
                schema: "billing",
                table: "HoanTien",
                column: "NgayBanId");

            migrationBuilder.CreateIndex(
                name: "IX_HoanTien_NguoiChiId",
                schema: "billing",
                table: "HoanTien",
                column: "NguoiChiId");

            migrationBuilder.CreateIndex(
                name: "IX_HoanTien_NguoiDeNghiId",
                schema: "billing",
                table: "HoanTien",
                column: "NguoiDeNghiId");

            migrationBuilder.CreateIndex(
                name: "IX_HoanTien_NguoiDuyetId",
                schema: "billing",
                table: "HoanTien",
                column: "NguoiDuyetId");

            migrationBuilder.CreateIndex(
                name: "IX_HoanTien_ThanhToanId",
                schema: "billing",
                table: "HoanTien",
                column: "ThanhToanId");

            migrationBuilder.CreateIndex(
                name: "IX_MienThu_HoaDonId",
                schema: "billing",
                table: "MienThu",
                column: "HoaDonId");

            migrationBuilder.CreateIndex(
                name: "IX_MienThu_KhoaYeuCau",
                schema: "billing",
                table: "MienThu",
                column: "KhoaYeuCau",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MienThu_NgayBanId",
                schema: "billing",
                table: "MienThu",
                column: "NgayBanId");

            migrationBuilder.CreateIndex(
                name: "IX_MienThu_NguoiDuyetId",
                schema: "billing",
                table: "MienThu",
                column: "NguoiDuyetId");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToan_HoaDonId",
                schema: "billing",
                table: "ThanhToan",
                column: "HoaDonId");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToan_KhoaYeuCau",
                schema: "billing",
                table: "ThanhToan",
                column: "KhoaYeuCau",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToan_NgayBanId",
                schema: "billing",
                table: "ThanhToan",
                column: "NgayBanId");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToan_NguoiThuId",
                schema: "billing",
                table: "ThanhToan",
                column: "NguoiThuId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChungTu",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "DieuChinhNghiaVu",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "DongHoaDon",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "MienThu",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "HoanTien",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "ThanhToan",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "HoaDon",
                schema: "billing");
        }
    }
}
