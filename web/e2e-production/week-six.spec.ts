import { test, expect } from "@playwright/test";
import { mockIdentity } from "./helpers/identity";
import { join } from "node:path";
import { tmpdir } from "node:os";

test("manager navigates to reports, filters closed dates and separates revenue and cash", async ({
    page,
}) => {
    await mockIdentity(page, {
        roles: ["Manager"],
        permissions: ["reporting.read"],
    });
    await page.route("**/api/reporting/daily?*", (r) =>
        r.fulfill({ json: { days: [] } }),
    );
    await page.route("**/api/reporting/range?*", (r) => {
        const params = new URL(r.request().url()).searchParams;
        expect([params.get("from"), params.get("to")]).toEqual([
            "2026-11-20",
            "2026-11-21",
        ]);
        return r.fulfill({
            json: {
                netRevenue: 2800000,
                cost: 700000,
                grossProfit: 2100000,
                drawerPurchases: 200000,
                days: [
                    {
                        dayId: "1",
                        date: "2026-11-21",
                        netRevenue: 2800000,
                        receipts: 3000000,
                        refunds: 100000,
                        drawerPurchases: 200000,
                    },
                ],
            },
        });
    });
    await page.goto("/more");
    await page.locator('a[href="/reports"]').last().click();
    await expect(
        page.getByText("Chưa có ngày đã chốt trong kỳ đã chọn."),
    ).toBeVisible();
    await page.getByLabel("Từ ngày", { exact: true }).fill("20/11/2026");
    await page.getByLabel("Đến ngày", { exact: true }).fill("20/11/2026");
    await page
        .getByRole("button", { name: "Mở lịch: Đến ngày", exact: true })
        .click();
    const calendar = page.getByRole("dialog", { name: "Chọn ngày: Đến ngày" });
    await expect(calendar.getByText("Tháng 11 năm 2026")).toBeVisible();
    await calendar
        .getByRole("button", { name: "21/11/2026", exact: true })
        .click();
    await expect(page.getByLabel("Đến ngày", { exact: true })).toHaveValue(
        "21/11/2026",
    );
    await page.getByRole("button", { name: "Xem báo cáo" }).click();
    await expect(
        page.getByRole("heading", { name: "Dòng tiền", exact: true }),
    ).toBeVisible();
    await expect(page.getByText("3.000.000 đ").first()).toBeVisible();
    await expect(
        page.getByRole("rowheader", { name: "21/11/2026" }),
    ).toBeVisible();
    for (const width of [1366, 360]) {
        await page.setViewportSize({ width, height: 820 });
        expect(
            await page.evaluate(() => document.documentElement.scrollWidth),
        ).toBeLessThanOrEqual(width);
        await page.screenshot({
            path: join(tmpdir(), "bao-six-report-" + width + ".png"),
        });
    }
    await page.getByRole("link", { name: "Về mục Thêm" }).click();
    await expect(page).toHaveURL(/\/more$/);
});
test("staff with a report claim still cannot open manager reports", async ({
    page,
}) => {
    await mockIdentity(page, {
        roles: ["Staff"],
        permissions: ["reporting.read"],
    });
    let requests = 0;
    await page.route("**/api/reporting/**", (r) => {
        requests++;
        return r.fulfill({ status: 403 });
    });
    await page.goto("/more");
    await expect(page.locator('a[href="/reports"]')).toHaveCount(0);
    await page.goto("/reports");
    await expect(
        page.getByRole("heading", { name: "Bạn không có quyền truy cập" }),
    ).toBeVisible();
    expect(requests).toBe(0);
});
test("manage a subordinate account and sign in with its reset password", async ({
    page,
}) => {
    const identity = await mockIdentity(page, {
        roles: ["Manager"],
        permissions: ["identity.subordinates.manage"],
    });
    let account:
        | {
              id: string;
              userName: string;
              email: string;
              roles: string[];
              active: boolean;
              lockedOut: boolean;
          }
        | undefined;
    let password = "";
    await page.route("**/api/identity/accounts**", async (r) => {
        const req = r.request(),
            path = new URL(req.url()).pathname,
            method = req.method();
        if (method === "GET")
            return r.fulfill({
                json: {
                    items: account ? [account] : [],
                    total: account ? 1 : 0,
                },
            });
        if (method === "POST" && path.endsWith("/accounts")) {
            const b = req.postDataJSON();
            expect(b.roles).toEqual(["Staff"]);
            password = b.password;
            account = {
                id: "created-user",
                userName: b.userName,
                email: b.email,
                roles: b.roles,
                active: true,
                lockedOut: false,
            };
        } else if (account) {
            if (path.endsWith("/roles") && method === "PUT")
                account.roles = req.postDataJSON().roles;
            else if (path.endsWith("/reset-password"))
                password = req.postDataJSON().newPassword;
            else if (path.endsWith("/deactivate")) account.active = false;
            else if (path.endsWith("/activate")) account.active = true;
            else throw Error("Unexpected account operation " + path);
        }
        return r.fulfill({ json: account });
    });
    await page.goto("/more");
    await page.locator('a[href="/staff"]').last().click();
    await page.getByRole("button", { name: "Tạo tài khoản" }).click();
    await expect(page.getByLabel("Quản trị", { exact: true })).toHaveCount(0);
    await page.getByLabel("Tên đăng nhập").fill("nhanvien06");
    await page.getByLabel("Email").fill("nv06@example.test");
    await page.getByLabel("Mật khẩu mới").fill("TestOnlyPass123!");
    await page.getByRole("button", { name: "Xác nhận" }).click();
    const row = page.getByRole("listitem").filter({ hasText: "nhanvien06" });
    await expect(row).toBeVisible();
    await page.setViewportSize({ width: 1366, height: 820 });
    await page.screenshot({
        path: join(tmpdir(), "bao-six-accounts-1366.png"),
    });
    await page.setViewportSize({ width: 360, height: 820 });
    expect(
        await page.evaluate(() => document.documentElement.scrollWidth),
    ).toBeLessThanOrEqual(360);
    await page.screenshot({ path: join(tmpdir(), "bao-six-accounts-360.png") });
    await row.getByRole("button", { name: "Đổi vai trò" }).click();
    await page.getByLabel("Bar", { exact: true }).check();
    await page.getByRole("button", { name: "Xác nhận" }).click();
    await expect(row).toContainText("Bar");
    for (const action of [
        "Khóa tài khoản",
        "Mở lại tài khoản",
        "Đặt lại mật khẩu",
    ]) {
        await row.getByRole("button", { name: action }).click();
        if (action === "Đặt lại mật khẩu")
            await page.getByLabel("Mật khẩu mới").fill("ResetOnlyPass123!");
        await page.getByRole("button", { name: "Xác nhận" }).click();
        await expect(row).toBeVisible();
    }
    await page.setViewportSize({ width: 1366, height: 820 });
    await page
        .getByRole("button", { name: "Đăng xuất phiên làm việc" })
        .click();
    await expect(page).toHaveURL(/\/login$/);
    await page.route("**/api/identity/login", (r) => {
        const body = r.request().postDataJSON();
        if (
            !account?.active ||
            body.userName !== account.userName ||
            body.password !== password
        )
            return r.fulfill({ status: 401 });
        Object.assign(identity.account, {
            id: account.id,
            userName: account.userName,
            roles: account.roles,
            permissions: [],
        });
        identity.signedIn = true;
        return r.fulfill({
            json: {
                accessToken: "test",
                expiresAt: new Date(Date.now() + 60000).toISOString(),
                account: identity.account,
            },
        });
    });
    await page.getByLabel("Tài khoản", { exact: true }).fill("nhanvien06");
    await page
        .getByLabel("Mật khẩu", { exact: true })
        .fill("ResetOnlyPass123!");
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
    await expect(page).toHaveURL(/\/sales-day$/);
    await expect(page.locator('a[href="/profile"]').first()).toContainText(
        "nhanvien06",
    );
});
test("reports distinguish loading, retryable API errors and empty results", async ({
    page,
}) => {
    await mockIdentity(page, {
        roles: ["Manager"],
        permissions: ["reporting.read"],
    });
    let release!: () => void;
    const ready = new Promise<void>((resolve) => {
        release = resolve;
    });
    let count = 0;
    await page.route("**/api/reporting/daily?*", async (r) => {
        if (++count === 1) {
            await ready;
            return r.fulfill({
                status: 400,
                json: { code: "reporting.range.invalid" },
            });
        }
        return r.fulfill({ json: { days: [] } });
    });
    await page.goto("/reports", { waitUntil: "domcontentloaded" });
    await expect(
        page.getByRole("status").filter({ hasText: "Đang tải dữ liệu" }),
    ).toBeVisible();
    release();
    await expect(
        page.getByRole("alert").filter({ hasText: "Ngày kết thúc" }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Thử lại" }).click();
    await expect(
        page.getByText("Chưa có ngày đã chốt trong kỳ đã chọn."),
    ).toBeVisible();
});
test("taken account name shows the API code in Vietnamese and the form remains editable", async ({
    page,
}) => {
    await mockIdentity(page, {
        roles: ["Admin"],
        permissions: ["identity.accounts.manage"],
    });
    await page.route("**/api/identity/accounts**", (r) =>
        r.request().method() === "POST"
            ? r.fulfill({
                  status: 409,
                  json: { code: "identity.account.user_name_taken" },
              })
            : r.fulfill({ json: { items: [], total: 0 } }),
    );
    await page.goto("/staff");
    await expect(page.getByText("Không có tài khoản phù hợp.")).toBeVisible();
    await page.getByRole("button", { name: "Tạo tài khoản" }).click();
    await page.getByLabel("Tên đăng nhập").fill("existing");
    await page.getByLabel("Email").fill("existing@example.test");
    await page.getByLabel("Mật khẩu mới").fill("FakeOnlyPass123!");
    await page.getByRole("button", { name: "Xác nhận" }).click();
    await expect(
        page
            .getByRole("alert")
            .filter({ hasText: "Tên đăng nhập đã được sử dụng." }),
    ).toBeVisible();
    await expect(page.getByLabel("Tên đăng nhập")).toBeEditable();
    await page.getByRole("button", { name: "Hủy", exact: true }).click();
    await page.getByRole("link", { name: "Về mục Thêm" }).click();
    await expect(page).toHaveURL(/\/more$/);
});
