import { expect, test } from "@playwright/test";
import {
    assertCompleteMenu,
    createCompleteMenuItem,
    loginAndOpenMenu,
    menuNames,
    type MenuSnapshot,
} from "./catalog-workflow";

test("real Catalog API: create a dish, variant and topping through UI, then read GET /menu", async ({
    page,
}, testInfo) => {
    test.skip(
        process.env.CATALOG_REAL_API !== "1",
        "Set CATALOG_REAL_API=1 against a disposable real backend/database.",
    );
    const userName = process.env.CATALOG_TEST_USER;
    const password = process.env.CATALOG_TEST_PASSWORD;
    expect(
        userName,
        "CATALOG_TEST_USER must identify a test account with catalog.menu.manage",
    ).toBeTruthy();
    expect(
        password,
        "CATALOG_TEST_PASSWORD must be supplied explicitly",
    ).toBeTruthy();
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    const token = await loginAndOpenMenu(page, userName!, password!);
    const names = menuNames(
        `${Date.now().toString(36)}-${testInfo.workerIndex}`.toUpperCase(),
    );
    await createCompleteMenuItem(page, names);

    // No route interception is used in this test: the request goes through the
    // same Next rewrite to the actual authenticated Catalog endpoint.
    const response = await page.request.get("/api/catalog/menu", {
        headers: { Authorization: `Bearer ${token}` },
    });
    expect(response.status()).toBe(200);
    const menu = (await response.json()) as MenuSnapshot;
    assertCompleteMenu(menu, names);
    await testInfo.attach("catalog-get-menu.json", {
        body: JSON.stringify(menu, null, 2),
        contentType: "application/json",
    });

    await page.reload();
    // In the default no-refresh configuration, re-authentication is expected.
    const loginField = page.getByLabel("Tài khoản", { exact: true });
    const itemButton = page.getByRole("button", {
        name: `Chi tiết món ${names.item}`,
        exact: true,
    });
    await expect(loginField.or(itemButton)).toBeVisible();
    if (await loginField.isVisible()) {
        await loginAndOpenMenu(page, userName!, password!);
    }
    await itemButton.click();
    await expect(page.getByText(names.variant, { exact: true })).toBeVisible();
    await expect(
        page.getByText(names.optionGroup, { exact: true }),
    ).toBeVisible();
    expect(errors).toEqual([]);
});
