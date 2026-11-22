using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Restaurant.Modules.Seating.Infrastructure.Persistence.Migrations;

// This command-only table is managed by SQL; consumption is a conditional UPDATE.
[DbContext(typeof(SeatingDbContext))]
[Migration("20261122000100_AddGuestInvitations")]
public sealed class AddGuestInvitations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE seating.MaThamGiaPhien (
            MaHash binary(32) NOT NULL PRIMARY KEY,
            PhienSuDungId bigint NOT NULL REFERENCES seating.PhienSuDung(Id),
            NguoiCapId uniqueidentifier NOT NULL,
            TaoLuc datetime2 NOT NULL,
            HetHanLuc datetime2 NOT NULL,
            DaDungLuc datetime2 NULL,
            ThuHoiLuc datetime2 NULL,
            CONSTRAINT CK_MaThamGia_Expiry CHECK (HetHanLuc > TaoLuc));
        CREATE INDEX IX_MaThamGia_Phien ON seating.MaThamGiaPhien(PhienSuDungId);
        """);
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE seating.MaThamGiaPhien");
}
