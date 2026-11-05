using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Identity.Domain;

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
            ("identity.accounts.manage", "Quan ly tai khoan moi cap", ["Admin"]),
            ("identity.roles.manage", "Quan ly vai tro va quyen", ["Admin"]),
            ("identity.subordinates.manage", "Quan ly tai khoan cap duoi", ["Manager"]),
            ("seating.catalog.manage", "Quan ly khu vuc va ban", ["Manager"]),
            ("seating.sessions.manage", "Mo chuyen va dong phien ban", ["Manager", "Staff"]),
            ("catalog.menu.manage", "Quan ly thuc don va gia", ["Manager"]),
            ("catalog.availability.manage", "Danh dau het va mo ban lai", ["Manager", "Staff", "Kitchen", "Bar"]),
            ("sales.orders.manage", "Lap va cap nhat don hang", ["Manager", "Staff"]),
            ("sales.batches.confirm", "Xac nhan dot goi mon", ["Manager", "Staff"]),
            ("fulfillment.kitchen.process", "Che bien tai bep", ["Kitchen"]),
            ("fulfillment.bar.process", "Che bien tai bar", ["Bar"]),
            ("fulfillment.incidents.report", "Bao su co mon", ["Staff", "Kitchen", "Bar", "Manager"]),
            ("fulfillment.incidents.approve", "Duyet huy va lam lai", ["Manager"]),
            ("fulfillment.portions.serve", "Giao tung phan mon", ["Manager", "Staff"]),
            ("billing.payments.collect", "Thu tien va xac nhan chuyen khoan", ["Manager", "Staff"]),
            ("billing.refunds.request", "De nghi hoan tien", ["Manager", "Staff"]),
            ("billing.refunds.approve", "Duyet hoan tien", ["Manager"]),
            ("billing.refunds.pay", "Chi va doi soat hoan tien", ["Manager"]),
            ("billing.waivers.approve", "Mien thu", ["Manager"]),
            ("inventory.catalog.manage", "Quan ly nguyen lieu va don vi", ["Manager"]),
            ("inventory.purchases.manage", "Lap va sua phieu nhap", ["Manager"]),
            ("inventory.counts.enter", "Nhap so dem nhap", ["Manager", "Staff", "Kitchen", "Bar"]),
            ("inventory.counts.approve", "Duyet so dem", ["Manager"]),
            ("inventory.adjustments.approve", "Dieu chinh ton", ["Manager"]),
            ("salesday.manage", "Mo doi soat va chot ngay ban", ["Manager"]),
            ("reporting.read", "Xem bao cao co ban", ["Manager"])
        };
        model.Entity<Permission>().HasData(permissions.Select(p => new Permission { Code = p.Code, Name = p.Name }));
        model.Entity<RolePermission>().HasData(permissions.SelectMany(p => p.Roles.Select(role => new RolePermission
        {
            RoleId = roles.Single(r => r.Item2 == role).Item1,
            PermissionCode = p.Code
        })));
    }
}
