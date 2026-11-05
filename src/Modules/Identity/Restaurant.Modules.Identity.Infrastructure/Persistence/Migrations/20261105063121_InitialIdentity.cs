using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Restaurant.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                schema: "identity",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminBootstrap",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminBootstrap", x => x.Id);
                    table.CheckConstraint("CK_AdminBootstrap_Singleton", "[Id] = 1");
                    table.ForeignKey(
                        name: "FK_AdminBootstrap_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "identity",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermission",
                schema: "identity",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermission", x => new { x.RoleId, x.PermissionCode });
                    table.ForeignKey(
                        name: "FK_RolePermission_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermission_Permission_PermissionCode",
                        column: x => x.PermissionCode,
                        principalSchema: "identity",
                        principalTable: "Permission",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("56e070a0-9b9e-49f5-8000-000000000001"), "56e070a0-9b9e-49f5-8000-000000000001", "Admin", "ADMIN" },
                    { new Guid("56e070a0-9b9e-49f5-8000-000000000002"), "56e070a0-9b9e-49f5-8000-000000000002", "Manager", "MANAGER" },
                    { new Guid("56e070a0-9b9e-49f5-8000-000000000003"), "56e070a0-9b9e-49f5-8000-000000000003", "Staff", "STAFF" },
                    { new Guid("56e070a0-9b9e-49f5-8000-000000000004"), "56e070a0-9b9e-49f5-8000-000000000004", "Kitchen", "KITCHEN" },
                    { new Guid("56e070a0-9b9e-49f5-8000-000000000005"), "56e070a0-9b9e-49f5-8000-000000000005", "Bar", "BAR" }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "Permission",
                columns: new[] { "Code", "Name" },
                values: new object[,]
                {
                    { "billing.payments.collect", "Thu tien va xac nhan chuyen khoan" },
                    { "billing.refunds.approve", "Duyet hoan tien" },
                    { "billing.refunds.pay", "Chi va doi soat hoan tien" },
                    { "billing.refunds.request", "De nghi hoan tien" },
                    { "billing.waivers.approve", "Mien thu" },
                    { "catalog.availability.manage", "Danh dau het va mo ban lai" },
                    { "catalog.menu.manage", "Quan ly thuc don va gia" },
                    { "fulfillment.bar.process", "Che bien tai bar" },
                    { "fulfillment.incidents.approve", "Duyet huy va lam lai" },
                    { "fulfillment.incidents.report", "Bao su co mon" },
                    { "fulfillment.kitchen.process", "Che bien tai bep" },
                    { "fulfillment.portions.serve", "Giao tung phan mon" },
                    { "identity.accounts.manage", "Quan ly tai khoan moi cap" },
                    { "identity.roles.manage", "Quan ly vai tro va quyen" },
                    { "identity.subordinates.manage", "Quan ly tai khoan cap duoi" },
                    { "inventory.adjustments.approve", "Dieu chinh ton" },
                    { "inventory.catalog.manage", "Quan ly nguyen lieu va don vi" },
                    { "inventory.counts.approve", "Duyet so dem" },
                    { "inventory.counts.enter", "Nhap so dem nhap" },
                    { "inventory.purchases.manage", "Lap va sua phieu nhap" },
                    { "reporting.read", "Xem bao cao co ban" },
                    { "sales.batches.confirm", "Xac nhan dot goi mon" },
                    { "sales.orders.manage", "Lap va cap nhat don hang" },
                    { "salesday.manage", "Mo doi soat va chot ngay ban" },
                    { "seating.catalog.manage", "Quan ly khu vuc va ban" },
                    { "seating.sessions.manage", "Mo chuyen va dong phien ban" }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "RolePermission",
                columns: new[] { "PermissionCode", "RoleId" },
                values: new object[,]
                {
                    { "identity.accounts.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000001") },
                    { "identity.roles.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000001") },
                    { "billing.payments.collect", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "billing.refunds.approve", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "billing.refunds.pay", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "billing.refunds.request", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "billing.waivers.approve", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "catalog.availability.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "catalog.menu.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "fulfillment.incidents.approve", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "fulfillment.incidents.report", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "fulfillment.portions.serve", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "identity.subordinates.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "inventory.adjustments.approve", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "inventory.catalog.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "inventory.counts.approve", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "inventory.counts.enter", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "inventory.purchases.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "reporting.read", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "sales.batches.confirm", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "sales.orders.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "salesday.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "seating.catalog.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "seating.sessions.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000002") },
                    { "billing.payments.collect", new Guid("56e070a0-9b9e-49f5-8000-000000000003") },
                    { "billing.refunds.request", new Guid("56e070a0-9b9e-49f5-8000-000000000003") },
                    { "catalog.availability.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000003") },
                    { "fulfillment.incidents.report", new Guid("56e070a0-9b9e-49f5-8000-000000000003") },
                    { "fulfillment.portions.serve", new Guid("56e070a0-9b9e-49f5-8000-000000000003") },
                    { "inventory.counts.enter", new Guid("56e070a0-9b9e-49f5-8000-000000000003") },
                    { "sales.batches.confirm", new Guid("56e070a0-9b9e-49f5-8000-000000000003") },
                    { "sales.orders.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000003") },
                    { "seating.sessions.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000003") },
                    { "catalog.availability.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000004") },
                    { "fulfillment.incidents.report", new Guid("56e070a0-9b9e-49f5-8000-000000000004") },
                    { "fulfillment.kitchen.process", new Guid("56e070a0-9b9e-49f5-8000-000000000004") },
                    { "inventory.counts.enter", new Guid("56e070a0-9b9e-49f5-8000-000000000004") },
                    { "catalog.availability.manage", new Guid("56e070a0-9b9e-49f5-8000-000000000005") },
                    { "fulfillment.bar.process", new Guid("56e070a0-9b9e-49f5-8000-000000000005") },
                    { "fulfillment.incidents.report", new Guid("56e070a0-9b9e-49f5-8000-000000000005") },
                    { "inventory.counts.enter", new Guid("56e070a0-9b9e-49f5-8000-000000000005") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminBootstrap_UserId",
                schema: "identity",
                table: "AdminBootstrap",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "identity",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "identity",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "identity",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "identity",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "identity",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_PermissionCode",
                schema: "identity",
                table: "RolePermission",
                column: "PermissionCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminBootstrap",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "RolePermission",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Permission",
                schema: "identity");
        }
    }
}
