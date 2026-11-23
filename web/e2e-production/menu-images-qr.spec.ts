import { test, expect } from "@playwright/test";
import { mockIdentity } from "./helpers/identity";
import { mockPublicMenu } from "./helpers/public-menu";

test("a closed session loses guest access while the public menu stays readable", async ({
    page,
}) => {
    await mockPublicMenu(page);
    await page.route("**/api/public/guest-access/exchange", (r) =>
        r.fulfill({
            json: {
                token: "only-for-this-session",
                expiresAt: new Date(Date.now() + 60000).toISOString(),
            },
        }),
    );
    await page.route("**/api/public/guest-access/session", (r) =>
        r.fulfill({ status: 401, json: { code: "qr.token.revoked" } }),
    );
    await page.goto("/guest/T04#j=invite");
    await expect(page.getByRole("status")).toContainText("Phiên bàn đã đóng");
    await expect(
        page.getByRole("heading", { name: "Trà đào · Ly" }),
    ).toBeVisible();
});

for (const [status, code, message] of [
    [400, "qr.code.expired", "Mã tham gia đã hết hạn"],
    [429, "qr.rate.limited", "Vui lòng chờ 7 giây"],
] as const) {
    test("QR " + code + " keeps public menu readable", async ({ page }) => {
        await mockPublicMenu(page);
        await page.route("**/api/public/guest-access/exchange", (r) =>
            r.fulfill({
                status,
                headers: { "Retry-After": "7" },
                json: { code },
            }),
        );
        await page.goto("/guest/T04#j=single-use");
        await expect(page).toHaveURL(/\/guest\/T04$/);
        await expect(page.getByRole("status")).toContainText(message);
        await expect(
            page.getByRole("heading", { name: "Trà đào · Ly" }),
        ).toBeVisible();
    });
}

test("manager uploads a resized WebP and staff and guest menus render the published image", async ({
    page,
}) => {
    const id = "12345678-1234-1234-1234-123456789abc",
        key = "menu/" + id.replaceAll("-", "") + "/" + "a".repeat(64) + ".webp";
    let saved: string | null = null,
        bytes: Buffer | null = null;
    await mockIdentity(page, {
        permissions: ["catalog.menu.manage", "sales.orders.manage"],
    });
    const active = { active: true, version: "AQ==" };
    await page.route("**/api/catalog/menu", (r) =>
        r.fulfill({
            json: {
                groups: [{ ...active, id: "g", name: "Trà", sortOrder: 1 }],
                items: [
                    {
                        ...active,
                        id,
                        name: "Trà đào",
                        groupId: "g",
                        code: "TRA",
                        station: "BAR",
                        imageObjectKey: saved,
                        variants: [
                            {
                                ...active,
                                id: "v",
                                itemId: id,
                                name: "Ly",
                                code: "V",
                                price: 30000,
                            },
                        ],
                        optionGroups: [],
                    },
                ],
                optionGroups: [],
            },
        }),
    );
    await page.route("**/api/catalog/availability", (r) =>
        r.fulfill({ json: { businessDayId: "day", soldOut: [] } }),
    );
    await page.route("**/api/sales/portions", (r) => r.fulfill({ json: [] }));
    await page.route("**/image/upload", (r) =>
        r.fulfill({
            json: {
                uploadId: "temp",
                uploadUrl: "https://test-bucket.example/upload",
            },
        }),
    );
    await page.route("https://test-bucket.example/upload", async (r) => {
        expect(await r.request().headerValue("authorization")).toBeNull();
        expect(await r.request().headerValue("content-type")).toBe(
            "image/webp",
        );
        bytes = r.request().postDataBuffer();
        expect(bytes?.subarray(8, 12).toString()).toBe("WEBP");
        return r.fulfill({
            status: 200,
            headers: { "Access-Control-Allow-Origin": "*" },
        });
    });
    await page.route("**/items/*/image", (r) => {
        expect(r.request().postDataJSON()).toEqual({
            uploadId: "temp",
            version: "AQ==",
        });
        saved = key;
        return r.fulfill({ json: key });
    });
    await page.route("**/api/public/menu-images/**", (r) =>
        r.fulfill({ contentType: "image/webp", body: bytes! }),
    );
    await mockPublicMenu(page, () => saved);
    await page.goto("/menu");
    const png = await page.evaluate(() => {
        const c = document.createElement("canvas");
        c.width = 1400;
        c.height = 700;
        return c.toDataURL("image/png").split(",")[1];
    });
    await page.getByLabel("Ảnh món Trà đào").setInputFiles({
        name: "tea.png",
        mimeType: "image/png",
        buffer: Buffer.from(png, "base64"),
    });
    for (const path of ["/menu", "/orders", "/guest/T04"]) {
        if (path !== "/menu") await page.goto(path);
        const image = page
            .locator('img[src*="/api/public/menu-images/"]')
            .first();
        await expect(image).toHaveAttribute("loading", "lazy");
        await expect
            .poll(() =>
                image.evaluate((i) => (i as HTMLImageElement).naturalWidth),
            )
            .toBe(960);
    }
});
