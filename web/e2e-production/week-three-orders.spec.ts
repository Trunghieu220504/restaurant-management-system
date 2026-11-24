import { test, expect } from "@playwright/test";
import { mockIdentity } from "./helpers/identity";
import { mockPublicMenu } from "./helpers/public-menu";
test("staff builds batches with a topping, reviews a price change and handles takeaway", async ({
    page,
}) => {
    await mockIdentity(page, {
        permissions: [
            "sales.orders.manage",
            "sales.batches.confirm",
            "seating.sessions.manage",
        ],
    });
    let price = 30000;
    await page.route("**/api/catalog/availability", (route) =>
        route.fulfill({ json: { businessDayId: "open-day", soldOut: [] } }),
    );
    await page.route("**/api/sales/portions", (route) =>
        route.fulfill({ json: [] }),
    );
    const row = { active: true, version: "AQ==" };
    await page.route("**/api/catalog/menu", (route) =>
        route.fulfill({
            json: {
                groups: [{ ...row, id: "g", name: "Trà", sortOrder: 1 }],
                items: [
                    {
                        ...row,
                        id: "i",
                        groupId: "g",
                        code: "TEA",
                        name: "Trà đào",
                        station: "BAR",
                        imageObjectKey: null,
                        variants: [
                            {
                                ...row,
                                id: "v",
                                itemId: "i",
                                name: "Ly lớn",
                                code: "L",
                                price,
                            },
                        ],
                        optionGroups: [
                            {
                                optionGroupId: "og",
                                minSelections: 1,
                                maxSelections: 1,
                            },
                        ],
                    },
                ],
                optionGroups: [
                    {
                        ...row,
                        id: "og",
                        name: "Topping",
                        type: "TOPPING",
                        options: ["Trân châu", "Thạch"].map((name, i) => ({
                            ...row,
                            id: String(i),
                            optionGroupId: "og",
                            name,
                            extraPrice: 5000,
                        })),
                    },
                ],
            },
        }),
    );
    await page.route("**/api/seating/board", (route) =>
        route.fulfill({
            json: [{ tables: [{ name: "Bàn 01", session: { id: 42 } }] }],
        }),
    );
    await page.route("**/api/salesday/current", (route) =>
        route.fulfill({ json: { status: "DANG_BAN" } }),
    );
    await page.goto("/orders");
    await page.getByLabel("Bàn đang mở").selectOption("42");
    await page.getByLabel("Nhóm món").selectOption("g");
    await page.getByLabel("Tìm món").fill("đào");
    const dish = page.getByRole("button", { name: "Trà đào", exact: true });
    const add = async () => {
        await dish.click();
        const d = page.getByRole("dialog");
        await d.getByLabel(/Trân châu/).check();
        await d.getByRole("button", { name: "Thêm vào giỏ" }).click();
    };
    await dish.click();
    const d = page.getByRole("dialog");
    const pick = d.getByRole("button", { name: "Thêm vào giỏ" });
    await pick.click();
    await expect(d.getByRole("alert")).toBeVisible();
    await d.getByLabel(/Trân châu/).check();
    await d.getByLabel(/Thạch/).check();
    await pick.click();
    await expect(d).toBeVisible();
    await d.getByLabel(/Thạch/).uncheck();
    await pick.click();
    await page.getByLabel("Số lượng").fill("2");
    const send = page.getByRole("button", { name: "Gửi đợt món" });
    price = 32000;
    await send.click();
    await expect(page.getByText(/order.price.changed/)).toBeVisible();
    await send.click();
    await page.getByRole("button", { name: "Sửa phần món" }).click();
    await page.getByLabel("Số phần sửa").fill("2");
    await page.getByLabel("Ghi chú chế biến").fill("Ít đá");
    await page.getByRole("button", { name: "Lưu phần món" }).click();
    await expect(page.getByText(/· Ít đá/)).toBeVisible();
    await page.getByRole("button", { name: "Hủy phần món" }).click();
    await page.getByLabel("Lý do hủy").fill("Khách giảm một phần");
    await page.getByRole("button", { name: "Xác nhận hủy" }).click();
    await expect(page.getByText(/Đã hủy 1/)).toBeVisible();
    await page.getByRole("button", { name: "Xác nhận đợt" }).click();
    await page.getByLabel("Chỉ hiện đợt chờ xác nhận").uncheck();
    await expect(
        page.getByRole("heading", { name: /Đã xác nhận/ }),
    ).toBeVisible();
    await expect(
        page.getByRole("button", { name: "Hủy phần món" }),
    ).toHaveCount(0);
    await add();
    await send.click();
    await expect(page.getByRole("article")).toHaveCount(1);
    await page.getByLabel("Lý do từ chối").fill("Khách đổi món");
    await page.getByRole("button", { name: "Từ chối đợt" }).click();
    await expect(page.getByText("Lý do từ chối: Khách đổi món")).toBeVisible();
    await page.getByLabel("Loại đơn").selectOption("away");
    await add();
    await send.click();
    await expect(page.getByLabel("Tên khách")).toBeFocused();
    await page.getByLabel("Tên khách").fill("An");
    await page.getByLabel("Điện thoại").fill("0901234567");
    await page.getByLabel("Ngày nhận", { exact: true }).fill("27/11/2026");
    await page.getByLabel("Giờ nhận").fill("18:00");
    await send.click();
    const takeaway = page.getByRole("article").filter({
        has: page.getByRole("heading", { name: /Mang về · An/ }),
    });
    const pickupDetails = takeaway.getByText(/0901234567.*18:00/);
    await expect(pickupDetails).toBeVisible();
    await page.reload();
    await expect(pickupDetails).toBeVisible();
});
test("public QR at 360px keeps a private draft and sends no orders", async ({
    page,
}) => {
    await page.setViewportSize({ width: 360, height: 800 });
    const api: string[] = [];
    page.on("request", (r) => {
        if (new URL(r.url()).pathname.startsWith("/api/")) api.push(r.url());
    });
    await mockPublicMenu(page);
    await page.route("**/api/public/guest-access/exchange", async (r) => {
        expect(r.request().postDataJSON()).toEqual({
            tableCode: "T04",
            joinCode: "example",
        });
        return r.fulfill({
            json: {
                token: "test-guest",
                expiresAt: new Date(Date.now() + 60000).toISOString(),
            },
        });
    });
    await page.route("**/api/public/guest-access/session", async (r) => {
        expect(await r.request().headerValue("Authorization")).toBe(
            "Guest test-guest",
        );
        return r.fulfill({ json: { tableName: "Bàn 4" } });
    });
    await page.goto("/guest/T04#j=example");
    await expect(page.getByRole("status")).toContainText(
        "Đã vào phiên · Bàn 4",
    );
    await expect(page).toHaveURL(/\/guest\/T04$/);
    await page
        .getByRole("button", { name: "Chọn", exact: true })
        .first()
        .click();
    await expect(
        page.getByRole("button", { name: "Gửi đợt gọi món" }),
    ).toBeDisabled();
    expect(
        await page.evaluate(() => document.documentElement.scrollWidth),
    ).toBeLessThanOrEqual(360);
    expect(api.map((url) => new URL(url).pathname).sort()).toEqual([
        "/api/public/guest-access/exchange",
        "/api/public/guest-access/session",
        "/api/public/menu",
    ]);
});
