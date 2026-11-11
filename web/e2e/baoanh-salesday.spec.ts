import { expect, test } from "@playwright/test";

for (const permitted of [true, false]) {
    test(`Bao Anh auth to Phuong SalesDay, manage=${permitted}`, async ({
        page,
    }) => {
        const account = {
            id: "manager",
            userName: "duy",
            email: null,
            roles: [permitted ? "Manager" : "Staff"],
            permissions: permitted ? ["salesday.manage"] : [],
        };
        let day: object | null = null;
        let refreshCalls = 0;
        await page.route("**/api/identity/refresh", async (route) => {
            refreshCalls++;
            await route.fulfill({ status: 404 });
        });
        await page.route("**/api/identity/login", async (route) => {
            expect(route.request().postDataJSON()).toEqual({
                userName: "duy",
                password: "test-password",
            });
            await route.fulfill({
                json: {
                    accessToken: "integration-token",
                    expiresAt: new Date(Date.now() + 900000).toISOString(),
                    account,
                },
            });
        });
        await page.route("**/api/identity/me", (route) =>
            route.fulfill({ json: account }),
        );
        await page.route("**/api/identity/logout", (route) =>
            route.fulfill({ status: 204 }),
        );
        await page.route("**/api/salesday/current", (route) =>
            route.fulfill({
                body: JSON.stringify(day),
                contentType: "application/json",
            }),
        );
        await page.route("**/api/salesday/open", async (route) => {
            expect(route.request().headers().authorization).toBe(
                "Bearer integration-token",
            );
            expect(route.request().headers()["idempotency-key"]).toMatch(
                /^[a-f0-9-]{36}$/,
            );
            day = {
                ...route.request().postDataJSON(),
                id: "day",
                status: "DANG_BAN",
                openedAt: new Date().toISOString(),
            };
            await route.fulfill({ status: 201, json: day });
        });
        await page.goto("/login");
        await page.getByLabel("Tài khoản", { exact: true }).fill("duy");
        await page
            .getByLabel("Mật khẩu", { exact: true })
            .fill("test-password");
        await page
            .getByRole("button", { name: "Đăng nhập", exact: true })
            .click();
        await expect(page).toHaveURL(/\/sales-day$/);
        if (permitted) {
            await page.getByLabel("Tiền mặt đầu ngày").fill("1500000");
            await page
                .getByRole("button", { name: "Mở ngày bán", exact: true })
                .click();
            await expect(
                page.getByText("ĐANG BÁN", { exact: true }),
            ).toBeVisible();
            await page
                .getByRole("button", { name: "Tải lại ngày bán" })
                .click();
            await expect(
                page.getByText("ĐANG BÁN", { exact: true }),
            ).toBeVisible();
        } else {
            await expect(
                page.getByText("Bạn không có quyền mở ngày bán."),
            ).toBeVisible();
            await expect(
                page.getByRole("button", { name: "Mở ngày bán", exact: true }),
            ).toHaveCount(0);
        }
        expect(refreshCalls).toBe(0);
        expect(
            await page.evaluate(() => localStorage.getItem("accessToken")),
        ).toBeNull();
        await page.reload();
        await expect(page).toHaveURL(/\/login$/);
        expect(refreshCalls).toBe(0);
    });
}
