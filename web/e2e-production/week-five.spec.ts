import { test, expect } from "@playwright/test";
import { mockPublicMenu } from "./helpers/public-menu";
import { mockIdentity } from "./helpers/identity";
test("verified guest retries a limited batch and tracks it at 360px", async ({
    page,
}) => {
    await page.setViewportSize({ width: 360, height: 740 });
    await mockPublicMenu(page);
    const api = "**/api/public/guest-access/";
    await page.route(api + "exchange", (r) =>
        r.fulfill({
            json: {
                token: "grant",
                expiresAt: new Date(Date.now() + 90000).toISOString(),
            },
        }),
    );
    await page.route(api + "session", (r) =>
        r.fulfill({ json: { tableName: "Bàn 1", canSubmitOrders: true } }),
    );
    const keys: string[] = [];
    await page.route(api + "batches", async (r) => {
        expect(await r.request().headerValue("Authorization")).toBe(
            "Guest grant",
        );
        keys.push((await r.request().headerValue("Idempotency-Key"))!);
        const portion = {
            menuItemId: "i0",
            variantId: "v0",
            note: "",
            optionIds: [],
        };
        expect(r.request().postDataJSON().lines).toEqual([portion, portion]);
        if (keys.length === 1)
            return r.fulfill({ status: 429, headers: { "Retry-After": "1" } });
        return r.fulfill({ json: { id: "batch", status: "CHO_XAC_NHAN" } });
    });
    await page.route(api + "batches/batch", (r) =>
        r.fulfill({ json: { id: "batch", status: "DA_XAC_NHAN" } }),
    );
    await page.goto("/guest/T01#j=one");
    await page
        .getByRole("button", { name: "Chọn", exact: true })
        .first()
        .click();
    const send = page.getByRole("button", { name: "Gửi đợt gọi món" });
    await page.getByRole("button", { name: /Tăng số lượng Trà đào/ }).click();
    await send.click();
    await expect(page.getByRole("main").getByRole("alert")).toContainText(
        "Quá nhiều yêu cầu",
    );
    await send.click();
    await expect(page.getByText("Đợt vừa gửi · Đã xác nhận")).toBeVisible();
    expect(keys[0]).toBeTruthy();
    expect(keys).toEqual([keys[0], keys[0]]);
    expect(
        await page.evaluate(() => document.documentElement.scrollWidth),
    ).toBeLessThanOrEqual(360);
});
test("keyboard skips navigation and staff recover a failed menu request", async ({
    page,
}) => {
    await mockIdentity(page, { permissions: ["catalog.menu.manage"] });
    let status = 500;
    await page.route("**/api/catalog/menu", (r) =>
        r.fulfill({
            status,
            json: { groups: [], items: [], optionGroups: [] },
        }),
    );
    await page.goto("/menu");
    await expect(
        page
            .getByRole("main")
            .getByRole("alert")
            .getByRole("button", { name: "Thử lại" }),
    ).toBeVisible();
    await page.keyboard.press("Tab");
    await expect(
        page.getByRole("link", { name: "Đến nội dung chính" }),
    ).toBeFocused();
    await page.keyboard.press("Enter");
    await expect(page.getByRole("main")).toBeFocused();
    status = 200;
    await page.getByRole("button", { name: "Thử lại" }).click();
    await expect(page.getByRole("main").getByRole("alert")).toHaveCount(0);
});
for (const status of [403, 500]) {
    test("staff menu remains usable after HTTP " + status, async ({ page }) => {
        await mockIdentity(page, { permissions: ["catalog.menu.manage"] });
        await page.route("**/api/catalog/menu", (r) =>
            r.fulfill({ status, json: {} }),
        );
        await page.goto("/menu");
        await expect(page.getByRole("main").getByRole("alert")).toBeVisible();
        await expect(
            page.getByRole("button", { name: "Thử lại" }),
        ).toBeEnabled();
    });
}
test("an unavailable variant can be removed after the server rejects a cart", async ({
    page,
}) => {
    await mockPublicMenu(page);
    const api = "**/api/public/guest-access/";
    await page.route(api + "exchange", (r) =>
        r.fulfill({
            json: {
                token: "grant",
                expiresAt: new Date(Date.now() + 60000).toISOString(),
            },
        }),
    );
    await page.route(api + "session", (r) =>
        r.fulfill({ json: { tableName: "Bàn 1", canSubmitOrders: true } }),
    );
    await page.route(api + "batches", async (r) => {
        await page.route("**/api/public/menu", (r) =>
            r.fulfill({ json: { businessDayId: "day", groups: [] } }),
        );
        return r.fulfill({
            status: 400,
            json: {
                code: "sales.batch.line_invalid",
                lineErrors: [{ lineNo: 1, code: "catalog.item.sold_out" }],
            },
        });
    });
    await page.goto("/guest/T01#j=one");
    await page
        .getByRole("button", { name: "Chọn", exact: true })
        .first()
        .click();
    const send = page.getByRole("button", { name: "Gửi đợt gọi món" });
    await send.click();
    await expect(send).toBeDisabled();
    await page.getByRole("button", { name: "Bỏ món hết" }).click();
    await expect(send).toHaveCount(0);
});
