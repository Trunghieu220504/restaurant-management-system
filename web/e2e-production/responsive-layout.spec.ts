import { mockPublicMenu } from "./helpers/public-menu";
import { expect, test } from "@playwright/test";
import { mockIdentity } from "./helpers/identity";

test("dashboard switches navigation when the window resizes", async ({
    page,
}) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await mockIdentity(page, { permissions: ["reporting.read"] });
    await page.goto("/");
    const menu = page.getByRole("button", { name: "Mở menu" });
    const sidebar = page.getByRole("navigation", {
        name: "Điều hướng chính",
        exact: true,
    });
    const mobileTabs = page.getByRole("navigation", {
        name: "Thanh điều hướng đáy màn hình di động",
    });
    await expect(mobileTabs).toBeVisible();
    await expect(sidebar).toBeHidden();
    await menu.click();
    await expect(sidebar).toBeVisible();
    await page.getByRole("button", { name: "Đóng menu" }).press("Enter");

    for (const width of [960, 1366]) {
        await page.setViewportSize({ width, height: 768 });
        await expect(sidebar).toBeVisible();
        await expect(menu).toBeHidden();
        await expect(mobileTabs).toBeHidden();
        const main = await page.getByRole("main").boundingBox();
        const nav = await sidebar.boundingBox();
        expect(main!.x).toBeGreaterThanOrEqual(nav!.x + nav!.width);
    }

    await page.setViewportSize({ width: 320, height: 640 });
    await expect(mobileTabs).toBeVisible();
    await expect(sidebar).toBeHidden();
    expect(
        await page.evaluate(() => document.documentElement.scrollWidth),
    ).toBeLessThanOrEqual(320);
});

test("QR menu uses multiple columns on laptop and keeps the cart on resize", async ({
    page,
}) => {
    await page.setViewportSize({ width: 1366, height: 768 });
    await mockPublicMenu(page);
    await page.goto("/guest?table=T04");
    const dishes = page
        .getByRole("region", { name: "Thực đơn" })
        .getByRole("article");
    await expect(dishes.nth(1)).toBeVisible();
    const first = await dishes.first().boundingBox();
    const second = await dishes.nth(1).boundingBox();
    expect(second!.x).toBeGreaterThan(first!.x);
    expect(second!.y).toBe(first!.y);
    await page
        .getByRole("button", { name: "Chọn", exact: true })
        .first()
        .click();
    const submit = page.getByRole("button", { name: "Gửi đợt gọi món" });
    await expect(submit).toBeInViewport();
    await page.setViewportSize({ width: 320, height: 640 });
    const mobileFirst = await dishes.first().boundingBox();
    const mobileSecond = await dishes.nth(1).boundingBox();
    expect(mobileSecond!.x).toBe(mobileFirst!.x);
    expect(mobileSecond!.y).toBeGreaterThan(mobileFirst!.y);
    await expect(submit).toBeInViewport();
    expect(
        await page.evaluate(() => document.documentElement.scrollWidth),
    ).toBeLessThanOrEqual(320);
});
