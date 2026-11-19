using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Application.Authorization;

namespace Restaurant.Modules.Identity.Infrastructure.Persistence;

internal static class IdentityCatalog
{
    internal static readonly Guid AdminRoleId = Guid.Parse("56e070a0-9b9e-49f5-8000-000000000001");

    internal static void Seed(ModelBuilder model)
    {
        var roles = new[]
        {
            (AdminRoleId, "Admin"),
            (Guid.Parse("56e070a0-9b9e-49f5-8000-000000000002"), "Manager"),
            (Guid.Parse("56e070a0-9b9e-49f5-8000-000000000003"), "Staff"),
            (Guid.Parse("56e070a0-9b9e-49f5-8000-000000000004"), "Kitchen"),
            (Guid.Parse("56e070a0-9b9e-49f5-8000-000000000005"), "Bar")
        };
        model.Entity<IdentityRole<Guid>>().HasData(roles.Select(r => new IdentityRole<Guid>
        {
            Id = r.Item1,
            Name = r.Item2,
            NormalizedName = r.Item2.ToUpperInvariant(),
            ConcurrencyStamp = r.Item1.ToString()
        }));

        var permissions = new (string Code, string Name, string[] Roles)[]
        {
            (PermissionCodes.IdentityAccountsManage, "Quan ly tai khoan moi cap", ["Admin"]),
            (PermissionCodes.IdentityRolesManage, "Quan ly vai tro va quyen", ["Admin"]),
            (PermissionCodes.IdentitySubordinatesManage, "Quan ly tai khoan cap duoi", ["Manager"]),
            (PermissionCodes.SeatingCatalogManage, "Quan ly khu vuc va ban", ["Manager"]),
            (PermissionCodes.SeatingSessionsManage, "Mo chuyen va dong phien ban", ["Manager", "Staff"]),
            (PermissionCodes.CatalogMenuManage, "Quan ly thuc don va gia", ["Manager"]),
            (PermissionCodes.CatalogAvailabilityManage, "Danh dau het va mo ban lai", ["Manager", "Staff", "Kitchen", "Bar"]),
            (PermissionCodes.SalesOrdersManage, "Lap va cap nhat don hang", ["Manager", "Staff"]),
            (PermissionCodes.SalesBatchesConfirm, "Xac nhan dot goi mon", ["Manager", "Staff"]),
            (PermissionCodes.FulfillmentKitchenProcess, "Che bien tai bep", ["Kitchen"]),
            (PermissionCodes.FulfillmentBarProcess, "Che bien tai bar", ["Bar"]),
            (PermissionCodes.FulfillmentIncidentsReport, "Bao su co mon", ["Staff", "Kitchen", "Bar", "Manager"]),
            (PermissionCodes.FulfillmentIncidentsApprove, "Duyet huy va lam lai", ["Manager"]),
            (PermissionCodes.FulfillmentPortionsServe, "Giao tung phan mon", ["Manager", "Staff"]),
            (PermissionCodes.BillingPaymentsCollect, "Thu tien va xac nhan chuyen khoan", ["Manager", "Staff"]),
            (PermissionCodes.BillingRefundsRequest, "De nghi hoan tien", ["Manager", "Staff"]),
            (PermissionCodes.BillingRefundsApprove, "Duyet hoan tien", ["Manager"]),
            (PermissionCodes.BillingRefundsPay, "Chi va doi soat hoan tien", ["Manager"]),
            (PermissionCodes.BillingWaiversApprove, "Mien thu", ["Manager"]),
            (PermissionCodes.InventoryCatalogManage, "Quan ly nguyen lieu va don vi", ["Manager"]),
            (PermissionCodes.InventoryPurchasesManage, "Lap va sua phieu nhap", ["Manager"]),
            (PermissionCodes.InventoryCountsEnter, "Nhap so dem nhap", ["Manager", "Staff", "Kitchen", "Bar"]),
            (PermissionCodes.InventoryCountsApprove, "Duyet so dem", ["Manager"]),
            (PermissionCodes.InventoryAdjustmentsApprove, "Dieu chinh ton", ["Manager"]),
            (PermissionCodes.SalesdayManage, "Mo doi soat va chot ngay ban", ["Manager"]),
            (PermissionCodes.ReportingRead, "Xem bao cao co ban", ["Manager"])
        };
        model.Entity<Permission>().HasData(permissions.Select(p => new Permission { Code = p.Code, Name = p.Name }));
        model.Entity<RolePermission>().HasData(permissions.SelectMany(p => p.Roles.Select(role => new RolePermission
        {
            RoleId = roles.Single(r => r.Item2 == role).Item1,
            PermissionCode = p.Code
        })));
    }
}
