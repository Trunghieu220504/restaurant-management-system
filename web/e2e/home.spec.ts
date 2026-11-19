import { expect, test } from "@playwright/test";

test("unauthenticated home redirects to login", async ({ page }) => {
    await page.goto("/");

    await expect(page).toHaveURL(/\/login(?:\?.*)?$/);
    await expect(page.getByRole("textbox").first()).toBeVisible();
    await expect(page.locator('input[type="password"]')).toBeVisible();
    await expect(page.getByRole("button", { name: /.+/ }).last()).toBeEnabled();
});
