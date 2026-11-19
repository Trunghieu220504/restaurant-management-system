import { expect, test } from "@playwright/test";
import { mockIdentity } from "./helpers/identity";

test("unauthenticated protected route redirects to login", async ({ page }) => {
    await mockIdentity(page, { signedIn: false });
    await page.goto("/tables");
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByLabel("Tài khoản", { exact: true })).toBeVisible();
});

test("invalid login, successful login, logout and protected-route re-entry", async ({
    page,
}) => {
    const state = await mockIdentity(page, {
        signedIn: false,
        permissions: ["salesday.manage"],
    });
    await page.goto("/login");
    await page.getByLabel("Tài khoản", { exact: true }).fill("ui-test");
    await page.getByLabel("Mật khẩu", { exact: true }).fill("invalid-password");
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
    await expect(
        page.getByRole("alert").filter({ hasText: "Đăng nhập thất bại" }),
    ).toBeVisible();
    await expect(page).toHaveURL(/\/login$/);

    await page
        .getByLabel("Mật khẩu", { exact: true })
        .fill("UiTestPassword123!");
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
    await expect(page.getByLabel("Tiền mặt đầu ngày")).toBeVisible();
    expect(state.loginCalls).toBe(2);
    expect(
        await page.evaluate(() => ({
            local: localStorage.getItem("accessToken"),
            session: sessionStorage.getItem("accessToken"),
        })),
    ).toEqual({ local: null, session: null });

    await page
        .getByRole("button", { name: "Đăng xuất phiên làm việc" })
        .click();
    await expect(page).toHaveURL(/\/login$/);
    await page.goto("/sales-day");
    await expect(page).toHaveURL(/\/login$/);
    expect(state.signedIn).toBe(false);
});

test("password validation prevents invalid requests", async ({ page }) => {
    const state = await mockIdentity(page);
    await page.goto("/profile/password");
    await page
        .getByLabel("Mật khẩu hiện tại", { exact: true })
        .fill("UiTestPassword123!");
    await page.getByLabel("Mật khẩu mới", { exact: true }).fill("short");
    await page.getByLabel("Nhập lại mật khẩu mới").fill("short");
    await page
        .getByRole("button", { name: "Đổi mật khẩu", exact: true })
        .click();
    await expect(
        page.getByText("Mật khẩu mới cần ít nhất 12 ký tự."),
    ).toBeVisible();
    expect(state.passwordCalls).toBe(0);

    await page
        .getByLabel("Mật khẩu mới", { exact: true })
        .fill("UiNewPassword456!");
    await page
        .getByLabel("Nhập lại mật khẩu mới")
        .fill("DifferentPassword789!");
    await page
        .getByRole("button", { name: "Đổi mật khẩu", exact: true })
        .click();
    await expect(page.getByText("Mật khẩu nhập lại không khớp.")).toBeVisible();
    expect(state.passwordCalls).toBe(0);
});

for (const status of [400, 429, 204]) {
    test(`change password handles HTTP ${status}`, async ({ page }) => {
        const state = await mockIdentity(page);
        state.passwordStatus = status;
        await page.goto("/profile/password");
        await page
            .getByLabel("Mật khẩu hiện tại", { exact: true })
            .fill("UiTestPassword123!");
        await page
            .getByLabel("Mật khẩu mới", { exact: true })
            .fill("UiNewPassword456!");
        await page
            .getByLabel("Nhập lại mật khẩu mới")
            .fill("UiNewPassword456!");
        await page
            .getByRole("button", { name: "Đổi mật khẩu", exact: true })
            .click();

        if (status === 204) {
            await expect(page).toHaveURL(/\/login$/);
            await page.goto("/profile/password");
            await expect(page).toHaveURL(/\/login$/);
        } else {
            const message =
                status === 400
                    ? "Không đổi được mật khẩu"
                    : "Bạn đã thử quá nhiều lần";
            await expect(
                page.getByRole("alert").filter({ hasText: message }),
            ).toBeVisible();
            await expect(
                page.getByText("Internal password validator detail"),
            ).toHaveCount(0);
            await expect(page).toHaveURL(/\/profile\/password$/);
            if (status === 429) {
                await expect(
                    page.getByRole("button", {
                        name: "Đổi mật khẩu",
                        exact: true,
                    }),
                ).toBeDisabled();
            }
        }
        expect(state.passwordCalls).toBe(1);
    });
}
