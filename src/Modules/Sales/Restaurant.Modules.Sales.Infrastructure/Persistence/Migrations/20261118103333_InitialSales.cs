using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Sales.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.CreateTable(
                name: "DonHang",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaDon = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Loai = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    PhienSuDungId = table.Column<long>(type: "bigint", nullable: true),
                    NhanBanLucTao = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TenKhach = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TrangThai = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    DongLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NguoiTaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SoLanCapNhat = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonHang", x => x.Id);
                    table.CheckConstraint("CK_DonHang_Dong", "([TrangThai] = 'MO' AND [DongLuc] IS NULL) OR ([TrangThai] <> 'MO' AND [DongLuc] IS NOT NULL AND [DongLuc] >= [TaoLuc])");
                    table.CheckConstraint("CK_DonHang_Loai", "[Loai] IN ('TAI_BAN','MANG_DI')");
                    table.CheckConstraint("CK_DonHang_Phien", "([Loai] = 'TAI_BAN' AND [PhienSuDungId] IS NOT NULL) OR ([Loai] = 'MANG_DI' AND [PhienSuDungId] IS NULL AND [NhanBanLucTao] IS NULL)");
                    table.CheckConstraint("CK_DonHang_TrangThai", "[TrangThai] IN ('MO','DONG','HUY')");
                });

            migrationBuilder.CreateTable(
                name: "DotGoiMon",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonHangId = table.Column<long>(type: "bigint", nullable: false),
                    SoDot = table.Column<int>(type: "int", nullable: false),
                    Nguon = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    QuyenTruyCapKhachId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NguoiTaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TrangThai = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    XacNhanLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiXacNhanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LyDoTuChoi = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    KhoaYeuCau = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DotGoiMon", x => x.Id);
                    table.CheckConstraint("CK_DotGoiMon_NguoiTao", "([Nguon] = 'KHACH' AND [QuyenTruyCapKhachId] IS NOT NULL AND [NguoiTaoId] IS NULL) OR ([Nguon] = 'NHAN_VIEN' AND [NguoiTaoId] IS NOT NULL AND [QuyenTruyCapKhachId] IS NULL)");
                    table.CheckConstraint("CK_DotGoiMon_Nguon", "[Nguon] IN ('KHACH','NHAN_VIEN')");
                    table.CheckConstraint("CK_DotGoiMon_QuyetDinh", "([TrangThai] IN ('CHO_XAC_NHAN','DA_HUY') AND [XacNhanLuc] IS NULL AND [NguoiXacNhanId] IS NULL AND [LyDoTuChoi] IS NULL) OR ([TrangThai] = 'DA_XAC_NHAN' AND [XacNhanLuc] IS NOT NULL AND [NguoiXacNhanId] IS NOT NULL AND [LyDoTuChoi] IS NULL) OR ([TrangThai] = 'DA_TU_CHOI' AND [XacNhanLuc] IS NOT NULL AND [NguoiXacNhanId] IS NOT NULL AND [LyDoTuChoi] IS NOT NULL)");
                    table.CheckConstraint("CK_DotGoiMon_SoDot", "[SoDot] > 0");
                    table.CheckConstraint("CK_DotGoiMon_TrangThai", "[TrangThai] IN ('CHO_XAC_NHAN','DA_XAC_NHAN','DA_TU_CHOI','DA_HUY')");
                    table.ForeignKey(
                        name: "FK_DotGoiMon_DonHang_DonHangId",
                        column: x => x.DonHangId,
                        principalSchema: "sales",
                        principalTable: "DonHang",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChiTietDon",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DotGoiMonId = table.Column<long>(type: "bigint", nullable: false),
                    MonId = table.Column<long>(type: "bigint", nullable: false),
                    BienTheId = table.Column<long>(type: "bigint", nullable: false),
                    TenMonChot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TenBienTheChot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    GiaMonChot = table.Column<long>(type: "bigint", nullable: false),
                    TramChot = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TrangThai = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietDon", x => x.Id);
                    table.CheckConstraint("CK_ChiTietDon_Gia", "[GiaMonChot] >= 0");
                    table.CheckConstraint("CK_ChiTietDon_Tram", "[TramChot] IN ('BEP','BAR')");
                    table.CheckConstraint("CK_ChiTietDon_TrangThai", "[TrangThai] IN ('CHO_XAC_NHAN','DA_XAC_NHAN','DA_HUY')");
                    table.ForeignKey(
                        name: "FK_ChiTietDon_DotGoiMon_DotGoiMonId",
                        column: x => x.DotGoiMonId,
                        principalSchema: "sales",
                        principalTable: "DotGoiMon",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TuyChonDaDat",
                schema: "sales",
                columns: table => new
                {
                    ChiTietDonId = table.Column<long>(type: "bigint", nullable: false),
                    TuyChonId = table.Column<long>(type: "bigint", nullable: false),
                    NhomTuyChonId = table.Column<long>(type: "bigint", nullable: false),
                    TenNhomChot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LoaiNhomChot = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    TenTuyChonChot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    GiaCongChot = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TuyChonDaDat", x => new { x.ChiTietDonId, x.TuyChonId });
                    table.CheckConstraint("CK_TuyChonDaDat_Gia", "[GiaCongChot] >= 0");
                    table.CheckConstraint("CK_TuyChonDaDat_Loai", "[LoaiNhomChot] IN ('TOPPING','TUY_CHON')");
                    table.ForeignKey(
                        name: "FK_TuyChonDaDat_ChiTietDon_ChiTietDonId",
                        column: x => x.ChiTietDonId,
                        principalSchema: "sales",
                        principalTable: "ChiTietDon",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDon_BienTheId",
                schema: "sales",
                table: "ChiTietDon",
                column: "BienTheId");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDon_DotGoiMonId",
                schema: "sales",
                table: "ChiTietDon",
                column: "DotGoiMonId");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDon_MonId",
                schema: "sales",
                table: "ChiTietDon",
                column: "MonId");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDon_TramChot_TrangThai",
                schema: "sales",
                table: "ChiTietDon",
                columns: new[] { "TramChot", "TrangThai" });

            migrationBuilder.CreateIndex(
                name: "IX_DonHang_MaDon",
                schema: "sales",
                table: "DonHang",
                column: "MaDon",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonHang_NgayBanId_TrangThai",
                schema: "sales",
                table: "DonHang",
                columns: new[] { "NgayBanId", "TrangThai" });

            migrationBuilder.CreateIndex(
                name: "IX_DonHang_NguoiTaoId",
                schema: "sales",
                table: "DonHang",
                column: "NguoiTaoId");

            migrationBuilder.CreateIndex(
                name: "UX_DonHang_PhienMo",
                schema: "sales",
                table: "DonHang",
                column: "PhienSuDungId",
                unique: true,
                filter: "[TrangThai] = 'MO' AND [PhienSuDungId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DotGoiMon_DonHangId_SoDot",
                schema: "sales",
                table: "DotGoiMon",
                columns: new[] { "DonHangId", "SoDot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DotGoiMon_KhoaYeuCau",
                schema: "sales",
                table: "DotGoiMon",
                column: "KhoaYeuCau",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DotGoiMon_NguoiTaoId",
                schema: "sales",
                table: "DotGoiMon",
                column: "NguoiTaoId");

            migrationBuilder.CreateIndex(
                name: "IX_DotGoiMon_NguoiXacNhanId",
                schema: "sales",
                table: "DotGoiMon",
                column: "NguoiXacNhanId");

            migrationBuilder.CreateIndex(
                name: "IX_DotGoiMon_QuyenTruyCapKhachId",
                schema: "sales",
                table: "DotGoiMon",
                column: "QuyenTruyCapKhachId");

            migrationBuilder.CreateIndex(
                name: "IX_DotGoiMon_TrangThai",
                schema: "sales",
                table: "DotGoiMon",
                column: "TrangThai");

            migrationBuilder.CreateIndex(
                name: "IX_TuyChonDaDat_TuyChonId",
                schema: "sales",
                table: "TuyChonDaDat",
                column: "TuyChonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TuyChonDaDat",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "ChiTietDon",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "DotGoiMon",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "DonHang",
                schema: "sales");
        }
    }
}
