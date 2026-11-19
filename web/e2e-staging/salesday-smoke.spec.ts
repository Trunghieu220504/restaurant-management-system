import { expect, test } from "@playwright/test";

test.afterEach(async ({ page }) => {
    const logout = page.getByRole("button", {
        name: "Đăng xuất phiên làm việc",
    });
    if (await logout.isVisible()) {
        await logout.click();
        await expect(page).toHaveURL(/\/login$/);
    }
});

test("staging readiness is healthy", async ({ request }) => {
    const response = await request.get("/health/ready");
    expect(response.status()).toBe(200);
    expect((await response.text()).trim()).toBe("Healthy");
});

test("real login and current SalesDay", async ({ page }) => {
    const username = process.env.STAGING_USERNAME;
    const password = process.env.STAGING_PASSWORD;
    test.skip(
        !username || !password,
        "Approved staging test credentials are not configured.",
    );
    await page.goto("/login");
    await page.getByLabel("Tài khoản", { exact: true }).fill(username!);
    await page.getByLabel("Mật khẩu", { exact: true }).fill(password!);
    const current = page.waitForResponse(
        (response) =>
            response.url().endsWith("/api/salesday/current") &&
            response.request().method() === "GET",
    );
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
    await expect(page).toHaveURL(/\/sales-day$/);
    expect((await current).status()).toBe(200);
    await expect(
        page.getByRole("heading", { name: "Ngày bán", exact: true }),
    ).toBeVisible();
});

test("approved open-day write followed by real menu read", async ({
    page,
    request,
}) => {
    test.skip(
        process.env.STAGING_ALLOW_OPEN_DAY !== "true",
        "Write disabled: obtain Phuong/Hai approval before enabling.",
    );
    const username = process.env.STAGING_USERNAME;
    const password = process.env.STAGING_PASSWORD;
    const date = process.env.STAGING_BUSINESS_DATE;
    const cash = Number(process.env.STAGING_OPENING_CASH);
    const menuPath = process.env.STAGING_MENU_PATH;
    test.skip(
        !username || !password || !date || !menuPath,
        "Missing approved account/date/menu API contract; not a PASS.",
    );
    expect(date).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(Number.isSafeInteger(cash) && cash >= 0).toBe(true);
    // Restrict the destination to the same-origin API; never send a token to a supplied host.
    expect(menuPath).toMatch(/^\/api\/[a-zA-Z0-9/_-]+$/);
    await page.goto("/login");
    await page.getByLabel("Tài khoản", { exact: true }).fill(username!);
    await page.getByLabel("Mật khẩu", { exact: true }).fill(password!);
    const currentPromise = page.waitForResponse(
        (response) =>
            response.url().endsWith("/api/salesday/current") &&
            response.request().method() === "GET",
    );
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
    const response = await currentPromise;
    expect(response.status()).toBe(200);
    const text = await response.text();
    const current = text.trim() ? JSON.parse(text) : null;
    test.skip(
        current !== null,
        "An active day exists; do not close/delete it to run smoke tests.",
    );
    const authorization = response.request().headers().authorization;
    expect(Boolean(authorization)).toBe(true);
    const preflight = await request.get(menuPath!, {
        headers: { Authorization: authorization },
    });
    expect(preflight.status()).toBe(200);
    expect(preflight.headers()["content-type"]).toContain("application/json");
    expect(await preflight.json()).not.toBeNull();
    await expect(page.getByLabel("Ngày kinh doanh")).toBeVisible();
    await page.getByLabel("Ngày kinh doanh").fill(date!);
    await page.getByLabel("Tiền mặt đầu ngày").fill(String(cash));
    const opened = page.waitForResponse(
        (r) =>
            r.url().endsWith("/api/salesday/open") &&
            r.request().method() === "POST",
    );
    await page
        .getByRole("button", { name: "Mở ngày bán", exact: true })
        .click();
    const result = await opened;
    expect([200, 201]).toContain(result.status());
    expect(result.request().headers()["idempotency-key"]).toMatch(
        /^[a-f0-9-]{36}$/,
    );
    await expect(page.getByText("ĐANG BÁN", { exact: true })).toBeVisible();
    const reload = page.waitForResponse((r) =>
        r.url().endsWith("/api/salesday/current"),
    );
    await page.getByRole("button", { name: "Tải lại ngày bán" }).click();
    const stored = await (await reload).json();
    expect(stored.businessDate).toBe(date);
    expect(Number(stored.openingCash)).toBe(cash);
    const menu = await request.get(menuPath!, {
        headers: { Authorization: result.request().headers().authorization },
    });
    expect(menu.status()).toBe(200);
    expect(menu.headers()["content-type"]).toContain("application/json");
    expect(await menu.json()).not.toBeNull();
});
