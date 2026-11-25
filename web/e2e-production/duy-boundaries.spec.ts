import { expect, test } from "@playwright/test";
import { mockIdentity } from "./helpers/identity";

test("development operations demo is not available in production", async ({
    page,
}) => {
    await mockIdentity(page);
    const response = await page.goto("/dev/week-three");
    expect(response?.status()).toBe(404);
    await expect(
        page.getByRole("heading", { name: "Vận hành · Tuần 2–3" }),
    ).toHaveCount(0);
});

test("production kitchen never displays fixture tickets as real operations", async ({
    page,
}) => {
    await mockIdentity(page, { permissions: ["fulfillment.kitchen.process"] });
    await page.goto("/kitchen");
    await expect(
        page.getByRole("heading", { name: "Phiếu chế biến" }),
    ).toBeVisible();
    await expect(page.getByText(/Phiếu #/)).toHaveCount(0);
    await expect(
        page.getByRole("button", { name: "Phiếu mới", exact: true }),
    ).toHaveCount(0);
});
