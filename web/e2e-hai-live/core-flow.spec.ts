import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
const credentials = JSON.parse(
    fs.readFileSync(process.env.HAI_DEMO_CREDENTIAL_FILE!, "utf8"),
);
const output = path.resolve("../artifacts/hai-live-demo");

test("real order, kitchen/bar, delivery, payment and close without mocks", async ({
    browser,
}) => {
    const contexts: BrowserContext[] = [];
    const browserErrors: string[] = [];
    async function login(role: string) {
        const account = credentials.accounts.find(
            (row: { role: string }) => row.role === role,
        );
        const context = await browser.newContext({
            baseURL: credentials.origin,
            viewport: { width: 1440, height: 1000 },
        });
        contexts.push(context);
        const page = await context.newPage();
        page.on("pageerror", (error) => browserErrors.push(error.message));
        await page.goto("/login");
        await page
            .getByLabel("Tài khoản", { exact: true })
            .fill(account.userName);
        await page
            .getByLabel("Mật khẩu", { exact: true })
            .fill(account.password);
        const signedIn = page.waitForResponse(
            (response) =>
                response.url().endsWith("/api/identity/login") &&
                response.request().method() === "POST",
        );
        await page
            .getByRole("button", { name: "Đăng nhập", exact: true })
            .click();
        const response = await signedIn;
        expect(response.status()).toBe(200);
        const token = (await response.json()).accessToken as string;
        await expect(page).not.toHaveURL(/\/login(?:\?|$)/);
        return { context, page, token };
    }
    try {
        const staff = await login("Staff"),
            kitchen = await login("Kitchen"),
            bar = await login("Bar");
        for (const page of [kitchen.page, bar.page]) {
            await page.goto("/kitchen");
            await expect(
                page.getByText("Đã kết nối thời gian thực", { exact: true }),
            ).toBeVisible({ timeout: 20000 });
            await page
                .getByRole("button", { name: "Bật âm thanh", exact: true })
                .click();
        }
        await staff.page.goto("/orders");
        await expect(
            staff.page.getByRole("heading", {
                name: "Gọi món, phục vụ và thu tiền",
            }),
        ).toBeVisible();
        const customer = "HAI UAT " + Date.now();
        await staff.page
            .getByLabel("Tên khách", { exact: true })
            .fill(customer);
        await staff.page
            .getByRole("button", { name: "Tạo đơn mới", exact: true })
            .click();
        const heading = staff.page.getByRole("heading", {
            name: new RegExp("DH-.*" + customer),
        });
        await expect(heading).toBeVisible();
        const code = (await heading.innerText()).match(/DH-\d+/)![0];
        async function add(name: string, batch: number, topping: boolean) {
            await staff.page
                .getByLabel("Món", { exact: true })
                .selectOption({ label: name });
            if (topping)
                await staff.page
                    .getByRole("checkbox", { name: /Demo topping/ })
                    .check();
            await staff.page
                .getByRole("button", { name: "Gửi đợt món", exact: true })
                .click();
            await expect(
                staff.page.getByRole("heading", {
                    name: `Đợt ${batch} · Chờ xác nhận`,
                    exact: true,
                }),
            ).toBeVisible();
            await staff.page
                .getByRole("button", {
                    name: `Xác nhận đợt ${batch}`,
                    exact: true,
                })
                .click();
            await expect(
                staff.page.getByRole("heading", {
                    name: `Đợt ${batch} · Đã xác nhận`,
                    exact: true,
                }),
            ).toBeVisible();
        }
        await add("Demo food", 1, false);
        await add("Demo tea", 2, true);
        async function prepare(page: Page, dish: string) {
            const card = page
                .locator("li")
                .filter({ hasText: code })
                .filter({ hasText: dish });
            await expect(card).toHaveCount(1, { timeout: 20000 });
            await card
                .getByRole("button", { name: "Bắt đầu", exact: true })
                .click();
            await expect(
                card.getByRole("button", { name: "Hoàn thành", exact: true }),
            ).toBeVisible();
            await card
                .getByRole("button", { name: "Hoàn thành", exact: true })
                .click();
            await expect(
                card.getByRole("button", { name: "Giao món", exact: true }),
            ).toHaveCount(0);
        }
        await prepare(kitchen.page, "Demo food");
        await prepare(bar.page, "Demo tea");
        const serving = staff.page.getByRole("region", {
            name: "Món chờ giao",
            exact: true,
        });
        for (const dish of ["Demo food", "Demo tea"]) {
            const card = serving
                .locator("li")
                .filter({ hasText: code })
                .filter({ hasText: dish });
            await expect(card).toHaveCount(1, { timeout: 20000 });
            await card
                .getByRole("button", { name: "Giao món", exact: true })
                .click();
            await expect(card).toHaveCount(0);
        }
        await expect(staff.page.getByTestId("invoice-due")).toHaveText(
            "45.000 VND",
        );
        await staff.page
            .getByRole("button", { name: "Điền số tiền còn lại", exact: true })
            .click();
        await staff.page
            .getByRole("button", { name: "Ghi nhận khoản thu", exact: true })
            .click();
        await expect(staff.page.getByTestId("invoice-due")).toHaveText("0 VND");
        const authorization = { Authorization: "Bearer " + staff.token };
        const list = await staff.context.request.get("/api/sales/orders/", {
            headers: authorization,
        });
        expect(list.ok()).toBeTruthy();
        const order = (await list.json()).items.find(
            (row: { code: string }) => row.code === code,
        );
        expect(order).toBeTruthy();
        const invoiceResponse = await staff.context.request.get(
            `/api/billing/orders/${order.id}/invoice`,
            { headers: authorization },
        );
        const invoice = await invoiceResponse.json();
        expect(Number(invoice.due)).toBe(0);
        expect(invoice.payments).toHaveLength(1);
        const snapshotResponse = await staff.context.request.get(
            `/api/fulfillment/snapshot?orderId=${order.id}`,
            { headers: authorization },
        );
        const snapshot = await snapshotResponse.json();
        expect(snapshot.tickets).toHaveLength(2);
        expect(
            snapshot.tickets.every(
                (ticket: { status: string }) => ticket.status === "DA_GIAO",
            ),
        ).toBeTruthy();
        await staff.page.screenshot({
            path: path.join(output, "paid-order.png"),
            fullPage: true,
        });
        await staff.page
            .getByRole("button", {
                name: "Đóng đơn đã xử lý xong",
                exact: true,
            })
            .click();
        await expect(
            staff.page.getByText(/Đã đóng đơn\. Với ăn tại bàn/),
        ).toBeVisible();
        await staff.page.setViewportSize({ width: 390, height: 844 });
        await expect
            .poll(() =>
                staff.page.evaluate(
                    () =>
                        document.documentElement.scrollWidth <=
                        window.innerWidth,
                ),
            )
            .toBeTruthy();
        await staff.page.screenshot({
            path: path.join(output, "orders-mobile.png"),
            fullPage: true,
        });
        expect(browserErrors).toEqual([]);
        fs.writeFileSync(
            path.join(output, "core-flow-evidence.json"),
            JSON.stringify(
                {
                    status: "PASS",
                    source: "real UI + API + SQL + SignalR",
                    orderCode: code,
                    orderId: order.id,
                    portions: 2,
                    payments: 1,
                    due: 0,
                    closed: true,
                    browserErrors: 0,
                    mobileWidth: 390,
                    physicalDeviceAudio: "NOT_TESTED",
                },
                null,
                2,
            ),
        );
    } finally {
        await Promise.all(contexts.map((context) => context.close()));
    }
});
