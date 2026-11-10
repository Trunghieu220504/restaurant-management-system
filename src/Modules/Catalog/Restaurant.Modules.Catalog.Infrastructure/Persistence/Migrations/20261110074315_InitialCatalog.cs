using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Modules.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "NhomMon",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    HoatDong = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhomMon", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NhomTuyChon",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Loai = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    HoatDong = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhomTuyChon", x => x.Id);
                    table.CheckConstraint("CK_NhomTuyChon_Loai", "[Loai] IN ('TOPPING','TUY_CHON')");
                });

            migrationBuilder.CreateTable(
                name: "Mon",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NhomMonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ma = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tram = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    AnhObjectKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    HoatDong = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mon", x => x.Id);
                    table.CheckConstraint("CK_Mon_Tram", "[Tram] IN ('BEP','BAR')");
                    table.ForeignKey(
                        name: "FK_Mon_NhomMon_NhomMonId",
                        column: x => x.NhomMonId,
                        principalSchema: "catalog",
                        principalTable: "NhomMon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TuyChon",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NhomTuyChonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GiaCong = table.Column<long>(type: "bigint", nullable: false),
                    HoatDong = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TuyChon", x => x.Id);
                    table.CheckConstraint("CK_TuyChon_Gia", "[GiaCong] >= 0");
                    table.ForeignKey(
                        name: "FK_TuyChon_NhomTuyChon_NhomTuyChonId",
                        column: x => x.NhomTuyChonId,
                        principalSchema: "catalog",
                        principalTable: "NhomTuyChon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BienThe",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ma = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gia = table.Column<long>(type: "bigint", nullable: false),
                    HoatDong = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BienThe", x => x.Id);
                    table.CheckConstraint("CK_BienThe_Gia", "[Gia] >= 0");
                    table.ForeignKey(
                        name: "FK_BienThe_Mon_MonId",
                        column: x => x.MonId,
                        principalSchema: "catalog",
                        principalTable: "Mon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HetMon",
                schema: "catalog",
                columns: table => new
                {
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Het = table.Column<bool>(type: "bit", nullable: false),
                    CapNhatLuc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NguoiCapNhatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HetMon", x => new { x.NgayBanId, x.MonId });
                    table.ForeignKey(
                        name: "FK_HetMon_Mon_MonId",
                        column: x => x.MonId,
                        principalSchema: "catalog",
                        principalTable: "Mon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonNhomTuyChon",
                schema: "catalog",
                columns: table => new
                {
                    MonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NhomTuyChonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChonToiThieu = table.Column<int>(type: "int", nullable: false),
                    ChonToiDa = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonNhomTuyChon", x => new { x.MonId, x.NhomTuyChonId });
                    table.CheckConstraint("CK_MonNhomTuyChon_Range", "[ChonToiThieu] >= 0 AND [ChonToiDa] >= [ChonToiThieu]");
                    table.ForeignKey(
                        name: "FK_MonNhomTuyChon_Mon_MonId",
                        column: x => x.MonId,
                        principalSchema: "catalog",
                        principalTable: "Mon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonNhomTuyChon_NhomTuyChon_NhomTuyChonId",
                        column: x => x.NhomTuyChonId,
                        principalSchema: "catalog",
                        principalTable: "NhomTuyChon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HetTuyChon",
                schema: "catalog",
                columns: table => new
                {
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TuyChonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Het = table.Column<bool>(type: "bit", nullable: false),
                    CapNhatLuc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NguoiCapNhatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HetTuyChon", x => new { x.NgayBanId, x.TuyChonId });
                    table.ForeignKey(
                        name: "FK_HetTuyChon_TuyChon_TuyChonId",
                        column: x => x.TuyChonId,
                        principalSchema: "catalog",
                        principalTable: "TuyChon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HetBienThe",
                schema: "catalog",
                columns: table => new
                {
                    NgayBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BienTheId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Het = table.Column<bool>(type: "bit", nullable: false),
                    CapNhatLuc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NguoiCapNhatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HetBienThe", x => new { x.NgayBanId, x.BienTheId });
                    table.ForeignKey(
                        name: "FK_HetBienThe_BienThe_BienTheId",
                        column: x => x.BienTheId,
                        principalSchema: "catalog",
                        principalTable: "BienThe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BienThe_MonId_Ma",
                schema: "catalog",
                table: "BienThe",
                columns: new[] { "MonId", "Ma" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HetBienThe_BienTheId",
                schema: "catalog",
                table: "HetBienThe",
                column: "BienTheId");

            migrationBuilder.CreateIndex(
                name: "IX_HetMon_MonId",
                schema: "catalog",
                table: "HetMon",
                column: "MonId");

            migrationBuilder.CreateIndex(
                name: "IX_HetTuyChon_TuyChonId",
                schema: "catalog",
                table: "HetTuyChon",
                column: "TuyChonId");

            migrationBuilder.CreateIndex(
                name: "IX_Mon_Ma",
                schema: "catalog",
                table: "Mon",
                column: "Ma",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mon_NhomMonId",
                schema: "catalog",
                table: "Mon",
                column: "NhomMonId");

            migrationBuilder.CreateIndex(
                name: "IX_MonNhomTuyChon_NhomTuyChonId",
                schema: "catalog",
                table: "MonNhomTuyChon",
                column: "NhomTuyChonId");

            migrationBuilder.CreateIndex(
                name: "IX_TuyChon_NhomTuyChonId",
                schema: "catalog",
                table: "TuyChon",
                column: "NhomTuyChonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HetBienThe",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "HetMon",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "HetTuyChon",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "MonNhomTuyChon",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "BienThe",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "TuyChon",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Mon",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "NhomTuyChon",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "NhomMon",
                schema: "catalog");
        }
    }
}
