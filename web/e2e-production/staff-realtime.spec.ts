import { test, expect, type Page } from "@playwright/test";
import { mockIdentity } from "./helpers/identity";

test("two browsers see a transferred session, availability and portion changes within two seconds", async ({
    page,
    browser,
    baseURL,
}, testInfo) => {
    const otherBrowser = await browser.browserType().launch();
    try {
        const other = await otherBrowser.newPage({ baseURL });
        let tableId = 1,
            sold = false,
            committed = 0,
            note = "",
            version = "1",
            cancelled = false;
        const row = { active: true, version: "AQ==" };
        const catalog = {
            groups: [{ ...row, id: "g", name: "Trà", sortOrder: 1 }],
            items: [
                {
                    ...row,
                    id: "i",
                    groupId: "g",
                    code: "TRA",
                    name: "Trà đào",
                    station: "BAR",
                    imageObjectKey: null,
                    variants: [
                        {
                            ...row,
                            id: "v",
                            itemId: "i",
                            code: "L",
                            name: "Ly",
                            price: 30000,
                        },
                    ],
                    optionGroups: [],
                },
            ],
            optionGroups: [],
        };
        const latencies: Record<string, number> = {};
        async function withinBudget(
            label: string,
            visible: () => Promise<void>,
        ) {
            await visible();
            latencies[label] = Date.now() - committed;
            expect(latencies[label], label).toBeLessThanOrEqual(2000);
        }
        async function setup(p: Page) {
            await mockIdentity(p, {
                permissions: [
                    "seating.sessions.manage",
                    "sales.orders.manage",
                    "catalog.availability.manage",
                ],
            });
            await p.route("**/api/seating/board", (route) =>
                route.fulfill({
                    json: [
                        {
                            id: 1,
                            code: "A",
                            name: "Sảnh",
                            tables: [1, 2].map((id) => ({
                                id,
                                name: "Bàn " + id,
                                code: "T" + id,
                                session:
                                    id === tableId
                                        ? {
                                              id: 42,
                                              openedAt: "2026-11-22T01:00:00Z",
                                          }
                                        : null,
                            })),
                        },
                    ],
                }),
            );
            await p.route("**/api/seating/sessions/42/transfer", (route) => {
                expect(route.request().postDataJSON()).toEqual({
                    toTableId: "2",
                    reason: "Khách đổi chỗ",
                });
                tableId = 2;
                committed = Date.now();
                return route.fulfill({ json: { id: 42 } });
            });
            await p.route("**/api/catalog/menu", (route) =>
                route.fulfill({ json: catalog }),
            );
            await p.route("**/api/catalog/availability", (route) => {
                if (route.request().method() === "PUT") {
                    expect(route.request().postDataJSON()).toEqual({
                        businessDayId: "day",
                        kind: "ITEM",
                        targetId: "i",
                        soldOut: !sold,
                    });
                    sold = !sold;
                    committed = Date.now();
                    return route.fulfill({ status: 204 });
                }
                return route.fulfill({
                    json: {
                        businessDayId: "day",
                        soldOut: sold ? [{ kind: "ITEM", targetId: "i" }] : [],
                    },
                });
            });
            await p.route("**/api/sales/portions", (route) =>
                route.fulfill({
                    json: cancelled
                        ? []
                        : [
                              {
                                  id: "7",
                                  orderCode: "DH07",
                                  dish: "Phở",
                                  variant: "Tô",
                                  note,
                                  status: "CHO_XAC_NHAN",
                                  version,
                              },
                          ],
                }),
            );
            await p.route("**/api/sales/portions/7/*", (route) => {
                const data = route.request().postDataJSON();
                expect(data.version).toBe(version);
                if (route.request().method() === "PUT") {
                    note = data.note;
                    version = "2";
                } else {
                    expect(data.reason).toBe("Khách đổi ý");
                    cancelled = true;
                }
                committed = Date.now();
                return route.fulfill({ status: 204 });
            });
        }
        await Promise.all([setup(page), setup(other)]);
        await Promise.all([page.goto("/tables"), other.goto("/tables")]);
        const card = (p: Page, id: number) =>
            p.getByRole("article", { name: "Bàn " + id, exact: true });
        await expect(card(other, 1)).toContainText("Đang dùng");
        await card(page, 1).getByRole("button", { name: "Chuyển bàn" }).click();
        const dialog = page.getByRole("dialog");
        await dialog.getByLabel("Bàn đích").selectOption("2");
        await dialog.getByLabel("Lý do").fill("Khách đổi chỗ");
        await dialog.getByRole("button", { name: "Chuyển bàn" }).click();
        await withinBudget("transfer", async () => {
            await Promise.all(
                [page, other].map(async (p) => {
                    await card(p, 2)
                        .getByText("Đang dùng", { exact: true })
                        .waitFor({ timeout: 2000 });
                    await card(p, 1)
                        .getByText("Trống", { exact: true })
                        .waitFor({ timeout: 2000 });
                }),
            );
        });
        await Promise.all([page.goto("/menu"), other.goto("/orders")]);
        const dish = other.getByRole("button", {
            name: "Trà đào",
            exact: true,
        });
        await expect(dish).toBeEnabled();
        const saleSwitch = page.getByRole("switch", { name: "Bán Trà đào" });
        await expect(saleSwitch).toBeChecked();
        await saleSwitch.click();
        await withinBudget("soldOut", async () => {
            await expect(
                other.getByRole("button", { name: "Trà đào · Hết món" }),
            ).toBeDisabled({ timeout: 2000 });
        });
        await expect(saleSwitch).not.toBeChecked();
        await expect(page.getByText("Đã hết", { exact: true })).toBeVisible();
        await saleSwitch.click();
        await withinBudget("restock", async () => {
            await expect(dish).toBeEnabled({ timeout: 2000 });
        });
        await page.goto("/orders");
        const live = (p: Page) =>
            p.getByRole("region", { name: "Phần món trên đơn đang mở" });
        await live(page).getByRole("button", { name: "Sửa ghi chú" }).click();
        await live(page).getByLabel("Ghi chú chế biến").fill("Ít hành");
        await live(page).getByRole("button", { name: "Lưu thay đổi" }).click();
        await withinBudget("edit", () =>
            live(other)
                .getByText("Ít hành", { exact: true })
                .waitFor({ timeout: 2000 }),
        );
        await live(page).getByRole("button", { name: "Hủy phần món" }).click();
        await live(page).getByLabel("Lý do hủy").fill("Khách đổi ý");
        await live(page).getByRole("button", { name: "Lưu thay đổi" }).click();
        await withinBudget("cancel", () =>
            live(other)
                .getByText("Chưa có phần món.")
                .waitFor({ timeout: 2000 }),
        );
        await testInfo.attach("observed-ms", {
            body: JSON.stringify(latencies),
            contentType: "application/json",
        });
        console.log("BAO-4.1 UI fixture latency (ms)", latencies);
    } finally {
        await otherBrowser.close();
    }
});
