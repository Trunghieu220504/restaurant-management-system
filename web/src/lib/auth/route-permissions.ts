/**
 * Quyền cần có để thấy menu và mở từng khu vực của trang quản trị.
 *
 * Mã quyền lấy từ IdentityCatalog.cs (backend) và có trong `permissions` của
 * `/api/identity/me`. Chỉ cần có MỘT quyền trong danh sách là được vào.
 * Đường dẫn không có trong bảng (Thêm, Hồ sơ, Ngày bán...) thì mọi tài khoản
 * đã đăng nhập đều mở được. Ẩn menu chỉ là tiện lợi: backend vẫn kiểm quyền
 * ở từng API.
 */
export const routePermissions: Readonly<Record<string, readonly string[]>> = {
    "/tables": ["seating.sessions.manage", "seating.catalog.manage"],
    "/orders": ["sales.orders.manage"],
    "/menu": ["catalog.menu.manage", "catalog.availability.manage"],
    "/inventory": [
        "inventory.catalog.manage",
        "inventory.purchases.manage",
        "inventory.counts.enter",
        "inventory.counts.approve",
        "inventory.adjustments.approve",
    ],
    "/kitchen": ["fulfillment.kitchen.process", "fulfillment.bar.process"],
    "/reports": ["reporting.read"],
    "/staff": ["identity.accounts.manage", "identity.subordinates.manage"],
};

/**
 * Trang QR của khách là trang công khai; nút xem thử trong trang quản trị chỉ
 * hiện cho người mở phiên bàn (người đưa mã QR cho khách).
 */
export const guestQrPreviewPermissions: readonly string[] = [
    "seating.sessions.manage",
];

type Can = (permission: string) => boolean;

export function hasAnyPermission(
    required: readonly string[] | undefined,
    can: Can,
): boolean {
    return !required || required.some((permission) => can(permission));
}

export function requiredPermissions(
    href: string,
): readonly string[] | undefined {
    const path = href.split(/[?#]/, 1)[0] || "/";
    const route = Object.keys(routePermissions).find(
        (prefix) => path === prefix || path.startsWith(`${prefix}/`),
    );
    return route ? routePermissions[route] : undefined;
}

export function canAccess(href: string, can: Can): boolean {
    return hasAnyPermission(requiredPermissions(href), can);
}
