using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Seating.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSeating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "seating");

            migrationBuilder.CreateTable(
                name: "KhuVuc",
                schema: "seating",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ma = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    HoatDong = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhuVuc", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ban",
                schema: "seating",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KhuVucId = table.Column<long>(type: "bigint", nullable: false),
                    Ma = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    HoatDong = table.Column<bool>(type: "bit", nullable: false),
                    MaQrCongKhai = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ban", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ban_KhuVuc_KhuVucId",
                        column: x => x.KhuVucId,
                        principalSchema: "seating",
                        principalTable: "KhuVuc",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PhienSuDung",
                schema: "seating",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BanHienTaiId = table.Column<long>(type: "bigint", nullable: false),
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NguoiMoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DongLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiDongId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhienSuDung", x => x.Id);
                    table.CheckConstraint("CK_PhienSuDung_Closure", "([DongLuc] IS NULL AND [NguoiDongId] IS NULL) OR ([DongLuc] IS NOT NULL AND [NguoiDongId] IS NOT NULL AND [DongLuc] >= [MoLuc])");
                    table.ForeignKey(
                        name: "FK_PhienSuDung_Ban_BanHienTaiId",
                        column: x => x.BanHienTaiId,
                        principalSchema: "seating",
                        principalTable: "Ban",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChuyenBan",
                schema: "seating",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhienSuDungId = table.Column<long>(type: "bigint", nullable: false),
                    BanCuId = table.Column<long>(type: "bigint", nullable: false),
                    BanMoiId = table.Column<long>(type: "bigint", nullable: false),
                    ChuyenLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NguoiChuyenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChuyenBan", x => x.Id);
                    table.CheckConstraint("CK_ChuyenBan_DifferentTables", "[BanCuId] <> [BanMoiId]");
                    table.ForeignKey(
                        name: "FK_ChuyenBan_Ban_BanCuId",
                        column: x => x.BanCuId,
                        principalSchema: "seating",
                        principalTable: "Ban",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChuyenBan_Ban_BanMoiId",
                        column: x => x.BanMoiId,
                        principalSchema: "seating",
                        principalTable: "Ban",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChuyenBan_PhienSuDung_PhienSuDungId",
                        column: x => x.PhienSuDungId,
                        principalSchema: "seating",
                        principalTable: "PhienSuDung",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QuyenTruyCapKhach",
                schema: "seating",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhienSuDungId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HetHanLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ThuHoiLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuyenTruyCapKhach", x => x.Id);
                    table.CheckConstraint("CK_QuyenTruyCapKhach_Expiry", "[HetHanLuc] > [TaoLuc]");
                    table.ForeignKey(
                        name: "FK_QuyenTruyCapKhach_PhienSuDung_PhienSuDungId",
                        column: x => x.PhienSuDungId,
                        principalSchema: "seating",
                        principalTable: "PhienSuDung",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ban_KhuVucId",
                schema: "seating",
                table: "Ban",
                column: "KhuVucId");

            migrationBuilder.CreateIndex(
                name: "IX_Ban_Ma",
                schema: "seating",
                table: "Ban",
                column: "Ma",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ban_MaQrCongKhai",
                schema: "seating",
                table: "Ban",
                column: "MaQrCongKhai",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBan_BanCuId",
                schema: "seating",
                table: "ChuyenBan",
                column: "BanCuId");

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBan_BanMoiId",
                schema: "seating",
                table: "ChuyenBan",
                column: "BanMoiId");

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBan_NguoiChuyenId",
                schema: "seating",
                table: "ChuyenBan",
                column: "NguoiChuyenId");

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBan_PhienSuDungId",
                schema: "seating",
                table: "ChuyenBan",
                column: "PhienSuDungId");

            migrationBuilder.CreateIndex(
                name: "IX_KhuVuc_Ma",
                schema: "seating",
                table: "KhuVuc",
                column: "Ma",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhienSuDung_BanHienTaiId",
                schema: "seating",
                table: "PhienSuDung",
                column: "BanHienTaiId",
                unique: true,
                filter: "[DongLuc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhienSuDung_NgayBanId",
                schema: "seating",
                table: "PhienSuDung",
                column: "NgayBanId");

            migrationBuilder.CreateIndex(
                name: "IX_PhienSuDung_NguoiDongId",
                schema: "seating",
                table: "PhienSuDung",
                column: "NguoiDongId");

            migrationBuilder.CreateIndex(
                name: "IX_PhienSuDung_NguoiMoId",
                schema: "seating",
                table: "PhienSuDung",
                column: "NguoiMoId");

            migrationBuilder.CreateIndex(
                name: "IX_QuyenTruyCapKhach_PhienSuDungId",
                schema: "seating",
                table: "QuyenTruyCapKhach",
                column: "PhienSuDungId");

            migrationBuilder.CreateIndex(
                name: "IX_QuyenTruyCapKhach_TokenHash",
                schema: "seating",
                table: "QuyenTruyCapKhach",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChuyenBan",
                schema: "seating");

            migrationBuilder.DropTable(
                name: "QuyenTruyCapKhach",
                schema: "seating");

            migrationBuilder.DropTable(
                name: "PhienSuDung",
                schema: "seating");

            migrationBuilder.DropTable(
                name: "Ban",
                schema: "seating");

            migrationBuilder.DropTable(
                name: "KhuVuc",
                schema: "seating");
        }
    }
}
