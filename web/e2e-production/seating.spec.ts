import { test, expect } from "@playwright/test";
import { mockIdentity } from "./helpers/identity";

test("staff retries an open command with the same key and sees the refreshed session", async ({
    page,
}) => {
    await mockIdentity(page, { permissions: ["seating.sessions.manage"] });
    const keys: string[] = [];
    let opened = false;
    await page.route("**/api/seating/**", async (route) => {
        const request = route.request();
        expect(await request.headerValue("authorization")).toBe(
            "Bearer ui-test-access",
        );
        const path = new URL(request.url()).pathname;
        if (path === "/api/seating/board" && request.method() === "GET") {
            return route.fulfill({
                json: [
                    {
                        id: 1,
                        code: "A",
                        name: "Sảnh",
                        tables: [
                            {
                                id: 2,
                                code: "T2",
                                name: "Bàn số hai",
                                status: opened ? "DANG_DUNG" : "TRONG",
                                session: opened
                                    ? {
                                          id: 3,
                                          openedAt: "2026-11-18T03:00:00Z",
                                      }
                                    : null,
                            },
                        ],
                    },
                ],
            });
        }
        expect(path).toBe("/api/seating/tables/2/sessions");
        expect(request.method()).toBe("POST");
        const key = await request.headerValue("idempotency-key");
        expect(key).toMatch(/^[\da-f-]{36}$/i);
        keys.push(key!);
        if (keys.length === 1) return route.fulfill({ status: 503, json: {} });
        opened = true;
        return route.fulfill({ status: 200, json: { id: 3 } });
    });
    await page.goto("/tables");
    const table = page.getByRole("article", { name: "Bàn số hai" });
    await expect(table.getByText("Trống", { exact: true })).toBeVisible();
    await expect(
        page.getByRole("button", { name: "Thêm khu vực" }),
    ).toHaveCount(0);
    await table.getByRole("button", { name: "Mở phiên" }).click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("button", { name: "Mở phiên" }).click();
    await expect(dialog.getByRole("alert")).toBeVisible();
    await dialog.getByRole("button", { name: "Mở phiên" }).click();
    await expect(table.getByText("Đang dùng", { exact: true })).toBeVisible();
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBe(keys[1]);
});
