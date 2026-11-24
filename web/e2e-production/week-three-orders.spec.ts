import { test, expect } from "@playwright/test";
import { mockIdentity } from "./helpers/identity";
import { mockPublicMenu } from "./helpers/public-menu";
test("staff sends real Sales-shaped commands and reloads server-owned batches", async ({
    page,
}) => {
    await mockIdentity(page, {
        permissions: [
            "sales.orders.manage",
            "sales.batches.confirm",
            "seating.sessions.manage",
        ],
    });
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
                                price: 30000,
                            },
                        ],
                        optionGroups: [
                            {
                                optionGroupId: "og",
                                minSelections: 0,
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
                        options: [
                            {
                                ...row,
                                id: "o",
                                optionGroupId: "og",
                                name: "Trân châu",
                                extraPrice: 5000,
                            },
                        ],
                    },
                ],
            },
        }),
    );
    await page.route("**/api/catalog/availability", (route) =>
        route.fulfill({ json: { businessDayId: "day", soldOut: [] } }),
    );
    await page.route("**/api/seating/board", (route) =>
        route.fulfill({
            json: [
                { tables: [{ id: 1, name: "Bàn 01", session: { id: 42 } }] },
            ],
        }),
    );
    await page.route("**/api/salesday/current", (route) =>
        route.fulfill({ json: { id: "day", status: "DANG_BAN" } }),
    );
    await page.route("**/api/sales/portions", (route) =>
        route.fulfill({ json: [] }),
    );
    const batches: {
        id: number;
        number: number;
        status: string;
        total: number;
        portions: {
            id: number;
            menuItemName: string;
            variantName: string;
            optionNames: string[];
            unitPrice: number;
            status: string;
        }[];
    }[] = [];
    let created = false,
        priceChanged = true,
        createCalls = 0;
    const keys: string[] = [];
    await page.route("**/api/sales/orders**", async (route) => {
        const request = route.request(),
            pathname = new URL(request.url()).pathname;
        if (request.method() === "GET")
            return route.fulfill({
                json: {
                    total: created ? 1 : 0,
                    items: created
                        ? [
                              {
                                  id: 1,
                                  code: "DH-0001",
                                  type: "TAI_BAN",
                                  businessDayId: "day",
                                  sessionId: 42,
                                  tableLabel: "Bàn 01",
                                  customerName: null,
                                  batches,
                              },
                          ]
                        : [],
                },
            });
        if (pathname.replace(/\/$/, "") === "/api/sales/orders") {
            expect(request.postDataJSON()).toMatchObject({
                type: "TAI_BAN",
                sessionId: 42,
                businessDayId: null,
            });
            created = true;
            createCalls++;
            return route.fulfill({ status: 201, json: { id: 1 } });
        }
        if (pathname.endsWith("/batches")) {
            keys.push((await request.headerValue("Idempotency-Key"))!);
            expect(request.postDataJSON()).toMatchObject({
                source: "NHAN_VIEN",
                lines: [{ menuItemId: "i", variantId: "v", optionIds: ["o"] }],
            });
            if (priceChanged) {
                priceChanged = false;
                return route.fulfill({
                    status: 409,
                    json: { code: "sales.pricing.changed" },
                });
            }
            batches.push({
                id: 1,
                number: 1,
                status: "CHO_XAC_NHAN",
                total: 35000,
                portions: [
                    {
                        id: 1,
                        menuItemName: "Trà đào",
                        variantName: "Ly lớn",
                        optionNames: ["Trân châu"],
                        unitPrice: 35000,
                        status: "CHO_XAC_NHAN",
                    },
                ],
            });
            return route.fulfill({ status: 201, json: { number: 1 } });
        }
        if (pathname.endsWith("/confirm")) {
            batches[0].status = "DA_XAC_NHAN";
            return route.fulfill({ json: {} });
        }
        throw new Error("Unexpected Sales request");
    });
    await page.goto("/orders");
    await page.getByLabel("Phục vụ", { exact: true }).selectOption("42");
    await page
        .getByRole("button", { name: "Tạo đơn mới", exact: true })
        .click();
    await expect(
        page.getByRole("heading", { name: "DH-0001 · Bàn 01" }),
    ).toBeVisible();
    await page.getByLabel("Món", { exact: true }).selectOption("i");
    await page.getByRole("checkbox", { name: /Trân châu/ }).check();
    await page
        .getByRole("button", { name: "Gửi đợt món", exact: true })
        .click();
    await expect(
        page.getByRole("alert").filter({ hasText: "Giá món đã thay đổi" }),
    ).toBeVisible();
    await page
        .getByRole("button", { name: "Gửi đợt món", exact: true })
        .click();
    await expect(
        page.getByRole("heading", { name: "Đợt 1 · Chờ xác nhận" }),
    ).toBeVisible();
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).toBe(keys[0]);
    await page
        .getByRole("button", { name: "Xác nhận đợt 1", exact: true })
        .click();
    await expect(
        page.getByRole("heading", { name: "Đợt 1 · Đã xác nhận" }),
    ).toBeVisible();
    await page.reload();
    await page.getByRole("button", { name: /DH-0001.*Bàn 01/ }).click();
    await expect(
        page.getByRole("heading", { name: "Đợt 1 · Đã xác nhận" }),
    ).toBeVisible();
    expect(createCalls).toBe(1);
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
