import { expect, test } from "@playwright/test";

for (const viewport of [
    { width: 1440, height: 1000 },
    { width: 390, height: 844 },
]) {
    test(`week one components and open day ${viewport.width}`, async ({
        page,
    }) => {
        await page.setViewportSize(viewport);
        await page.goto("/dev/components");
        await expect(
            page.getByText("Đã kết nối", { exact: true }),
        ).toBeVisible();
        await page
            .getByRole("button", { name: "Món mới", exact: true })
            .click();
        await expect(page.getByRole("listitem")).toHaveCount(2);
        await page.getByRole("button", { name: "Gửi lại sự kiện" }).click();
        await expect(page.getByRole("listitem")).toHaveCount(2);
        await page
            .getByRole("button", { name: "Ngắt kết nối", exact: true })
            .click();
        await expect(
            page.getByText("Mất kết nối", { exact: true }),
        ).toBeVisible();
        await page
            .getByRole("button", { name: "Kết nối lại", exact: true })
            .click();
        await expect(page.getByRole("listitem")).toHaveCount(1);
        await page.getByRole("button", { name: "Gửi lại sự kiện" }).click();
        await expect(page.getByRole("listitem")).toHaveCount(1);
        await page.getByLabel("Tiền mặt đầu ngày").fill("1500000");
        await page
            .getByRole("button", { name: "Mở ngày bán", exact: true })
            .click();
        await expect(page.getByText("ĐANG BÁN", { exact: true })).toBeVisible();
        await expect(
            page.getByText("1.500.000", { exact: false }).last(),
        ).toBeVisible();
        await expect(
            page.getByRole("button", { name: "Mở ngày bán", exact: true }),
        ).toHaveCount(0);
        const overflowing = await page.evaluate(
            () => document.documentElement.scrollWidth > window.innerWidth,
        );
        expect(overflowing).toBe(false);
        await page.screenshot({
            path: `test-results/duy-${viewport.width}.png`,
            fullPage: true,
        });
    });
}

test("mock covers states, permission and failure", async ({ page }) => {
    await page.goto("/dev/components");
    await page.getByLabel("Tình huống").selectOption("CHO_CHOT");
    await expect(page.getByText("CHỜ CHỐT", { exact: true })).toBeVisible();
    await page.getByLabel("Tình huống").selectOption("DA_CHOT");
    await expect(page.getByText("ĐÃ CHỐT", { exact: true })).toBeVisible();
    await page.getByLabel("Tình huống").selectOption("forbidden");
    await expect(
        page.getByText("Bạn không có quyền mở ngày bán."),
    ).toBeVisible();
    await page.getByLabel("Tình huống").selectOption("error");
    await expect(
        page
            .getByRole("alert")
            .filter({ hasText: "Không kết nối được máy chủ" }),
    ).toBeVisible();
});

test("live adapter retains the idempotency key after a network failure", async ({
    page,
}) => {
    const keys: (string | undefined)[] = [];
    await page.route("**/api/salesday/current", (route) =>
        route.fulfill({ json: null }),
    );
    await page.route("**/api/salesday/open", async (route) => {
        keys.push(route.request().headers()["idempotency-key"]);
        if (keys.length === 1) return route.abort("failed");
        return route.fulfill({
            status: 201,
            json: {
                id: "1",
                businessDate: "2026-11-10",
                openingCash: 0,
                status: "DANG_BAN",
                openedAt: "2026-11-10T01:00:00Z",
            },
        });
    });
    await page.goto("/dev/components");
    await expect(page.getByText("Đã kết nối", { exact: true })).toBeVisible();
    await page.getByText("Kết nối API thật", { exact: true }).click();
    await page.getByLabel("Access token tạm thời").fill("browser-test-token");
    await page.getByRole("button", { name: "Kết nối", exact: true }).click();
    await page
        .getByRole("button", { name: "Mở ngày bán", exact: true })
        .click();
    await expect(
        page.getByRole("alert").filter({ hasText: "Không kết nối được" }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Thử lại yêu cầu" }).click();
    await page
        .getByRole("button", { name: "Mở ngày bán", exact: true })
        .click();
    await expect(page.getByText("ĐANG BÁN", { exact: true })).toBeVisible();
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).toBe(keys[0]);
    expect(
        await page.evaluate(() => localStorage.getItem("accessToken")),
    ).toBeNull();
});
