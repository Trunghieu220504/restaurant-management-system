import { describe, expect, it } from "vitest";
import {
    canAccess,
    guestQrPreviewPermissions,
    hasAnyPermission,
    requiredPermissions,
} from "./route-permissions";

const granted =
    (...permissions: string[]) =>
    (permission: string) =>
        permissions.includes(permission);

describe("route permissions", () => {
    it("matches nested paths and ignores query strings", () => {
        expect(requiredPermissions("/orders/123")).toEqual([
            "sales.orders.manage",
        ]);
        expect(requiredPermissions("/kitchen?station=bar")).toEqual([
            "fulfillment.kitchen.process",
            "fulfillment.bar.process",
        ]);
        expect(requiredPermissions("/ordersx")).toBeUndefined();
    });

    it("needs any one of the listed permissions", () => {
        expect(canAccess("/kitchen", granted("fulfillment.bar.process"))).toBe(
            true,
        );
        expect(canAccess("/kitchen", granted("sales.orders.manage"))).toBe(
            false,
        );
    });

    it("keeps routes without a rule open to every signed-in account", () => {
        const none = granted();
        expect(canAccess("/more", none)).toBe(true);
        expect(canAccess("/profile/password", none)).toBe(true);
        expect(canAccess("/sales-day", none)).toBe(true);
    });

    it("hides every protected module from an account without permissions", () => {
        const none = granted();
        for (const href of [
            "/tables",
            "/orders",
            "/menu",
            "/inventory",
            "/kitchen",
            "/reports",
            "/staff",
        ]) {
            expect(canAccess(href, none)).toBe(false);
        }
        expect(hasAnyPermission(guestQrPreviewPermissions, none)).toBe(false);
    });
});
