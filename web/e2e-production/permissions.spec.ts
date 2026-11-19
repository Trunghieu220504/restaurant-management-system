import { expect, test } from "@playwright/test";
import { mockIdentity } from "./helpers/identity";

const protectedRoutes = [
    "/tables",
    "/orders",
    "/menu",
    "/inventory",
    "/kitchen",
    "/reports",
    "/staff",
];

for (const path of protectedRoutes) {
    test(`an Admin without permissions cannot open ${path}`, async ({
        page,
    }) => {
        // A role label must not grant operational permissions implicitly.
        await mockIdentity(page, { roles: ["Admin"], permissions: [] });
        await page.goto("/more");
        await expect(
            page.getByRole("heading", { name: "Bạn không có quyền truy cập" }),
        ).toHaveCount(0);
        await expect(page.locator(`a[href="${path}"]`)).toHaveCount(0);
        await page.goto(path);
        await expect(
            page.getByRole("heading", { name: "Bạn không có quyền truy cập" }),
        ).toBeVisible();
        await expect(page.locator(`a[href="${path}"]`)).toHaveCount(0);
    });
}

test("granted menu/report permissions allow routes; revocation hides menu and blocks the open route", async ({
    page,
}) => {
    const state = await mockIdentity(page, {
        permissions: ["catalog.availability.manage", "reporting.read"],
    });
    await page.goto("/more");
    await expect(page.locator('a[href="/reports"]').last()).toBeVisible();
    await expect(page.locator('a[href="/menu"]').first()).toBeVisible();
    await expect(page.locator('a[href="/inventory"]')).toHaveCount(0);
    await page.locator('a[href="/reports"]').last().click();
    await expect(page).toHaveURL(/\/reports$/);
    await expect(
        page.getByRole("heading", { name: "Báo cáo KD", exact: true }),
    ).toBeVisible();
    await expect(
        page.getByRole("heading", { name: "Bạn không có quyền truy cập" }),
    ).toHaveCount(0);
    await page.locator('a[href="/menu"]').first().click();
    await expect(page).toHaveURL(/\/menu$/);
    await expect(
        page.getByRole("heading", { name: "QL món ăn" }),
    ).toBeVisible();
    await expect(
        page.getByRole("heading", { name: "Bạn không có quyền truy cập" }),
    ).toHaveCount(0);

    state.account.permissions = [];
    await page.evaluate(() => window.dispatchEvent(new Event("focus")));
    await expect(
        page.getByRole("heading", { name: "Bạn không có quyền truy cập" }),
    ).toBeVisible();
    await expect(page.locator('a[href="/menu"]')).toHaveCount(0);
});

test("MVP navigation omits retired prototype pages and preserves mobile access", async ({
    page,
}) => {
    const retiredRoutes = ["/customers", "/promotions", "/stores", "/settings"];
    await mockIdentity(page, {
        permissions: [
            "catalog.menu.manage",
            "fulfillment.kitchen.process",
            "reporting.read",
            "identity.accounts.manage",
            "seating.sessions.manage",
        ],
    });

    await page.goto("/");
    await expect(
        page.getByRole("heading", { name: "Truy cập nhanh" }),
    ).toBeVisible();
    for (const path of retiredRoutes) {
        await expect(page.locator(`a[href="${path}"]`)).toHaveCount(0);
    }

    await page.setViewportSize({ width: 390, height: 844 });
    await page
        .getByRole("navigation", {
            name: "Thanh điều hướng đáy màn hình di động",
        })
        .getByRole("link", { name: "Thêm" })
        .click();
    await expect(page).toHaveURL(/\/more$/);
    const more = page.getByRole("main");
    for (const path of [
        "/sales-day",
        "/kitchen",
        "/reports",
        "/staff",
        "/profile",
    ]) {
        await expect(more.locator(`a[href="${path}"]`)).toBeVisible();
    }
    await expect(more.locator('a[href^="/guest?"]')).toBeVisible();
    for (const path of retiredRoutes) {
        await expect(page.locator(`a[href="${path}"]`)).toHaveCount(0);
    }

    for (const path of retiredRoutes) {
        const response = await page.goto(path);
        expect(response?.status(), path).toBe(404);
        await expect(
            page.getByText("Không tìm thấy trang", { exact: true }),
        ).toBeVisible();
    }
});
