import { test, expect } from "@playwright/test";
import { mockPublicMenu } from "./helpers/public-menu";

test("a read-only grant is sent to the menu without enabling order submission", async ({
    page,
}) => {
    await page.clock.install();
    let revoked = false;
    const menuHeaders: (string | undefined)[] = [];
    page.on("request", (request) => {
        if (new URL(request.url()).pathname === "/api/public/menu")
            menuHeaders.push(request.headers().authorization);
    });
    await mockPublicMenu(page);
    await page.route("**/api/public/guest-access/exchange", (route) =>
        route.fulfill({
            json: {
                token: "read-only-test-grant",
                expiresAt: new Date(Date.now() + 60000).toISOString(),
            },
        }),
    );
    await page.route("**/api/public/guest-access/session", (route) =>
        revoked
            ? route.fulfill({ status: 401, json: { code: "qr.token.revoked" } })
            : route.fulfill({
                  json: { tableName: "Bàn 1", canSubmitOrders: false },
              }),
    );
    await page.goto("/guest/T01#j=one");
    await expect
        .poll(() => menuHeaders.includes("Guest read-only-test-grant"))
        .toBe(true);
    await page
        .getByRole("button", { name: "Chọn", exact: true })
        .first()
        .click();
    await expect(
        page.getByRole("button", { name: "Gửi đợt gọi món" }),
    ).toBeDisabled();
    revoked = true;
    await page.clock.fastForward(21000);
    await expect(page.getByRole("status")).toContainText("Phiên bàn đã đóng");
    await expect.poll(() => menuHeaders.length).toBeGreaterThanOrEqual(3);
    expect(menuHeaders.at(-1)).toBeUndefined();
    await expect(
        page.getByRole("button", { name: "Gửi đợt gọi món" }),
    ).toBeDisabled();
});
