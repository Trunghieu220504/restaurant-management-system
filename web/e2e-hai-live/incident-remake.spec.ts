import { expect, test, type BrowserContext } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
const credentials = JSON.parse(
    fs.readFileSync(process.env.HAI_DEMO_CREDENTIAL_FILE!, "utf8"),
);

test("real incident approval remakes a dish once without increasing the invoice", async ({
    browser,
}) => {
    const contexts: BrowserContext[] = [];
    const errors: string[] = [];
    const websocketReady: Record<string, boolean> = {};
    async function login(role: string) {
        const account = credentials.accounts.find(
            (row: { role: string }) => row.role === role,
        );
        const context = await browser.newContext({
            baseURL: credentials.origin,
        });
        contexts.push(context);
        const page = await context.newPage();
        page.setDefaultTimeout(15000);
        page.on("websocket", (socket) => {
            if (new URL(socket.url()).pathname !== "/hubs/operations") return;
            socket.on("framereceived", ({ payload }) => {
                if (payload.toString().includes("{}\u001e"))
                    websocketReady[role] = true;
            });
        });
        page.on("pageerror", (error) => errors.push(error.message));
        await page.goto("/login");
        await page
            .getByLabel("Tài khoản", { exact: true })
            .fill(account.userName);
        await page
            .getByLabel("Mật khẩu", { exact: true })
            .fill(account.password);
        const loggedIn = page.waitForResponse(
            (r) =>
                r.url().endsWith("/api/identity/login") &&
                r.request().method() === "POST",
        );
        await page
            .getByRole("button", { name: "Đăng nhập", exact: true })
            .click();
        const response = await loggedIn;
        expect(response.status()).toBe(200);
        const token = (await response.json()).accessToken as string;
        await expect(page).not.toHaveURL(/\/login(?:\?|$)/);
        async function api(url: string, data?: unknown) {
            const result = await context.request.fetch(url, {
                method: data === undefined ? "GET" : "POST",
                timeout: 15000,
                headers: {
                    Authorization: `Bearer ${token}`,
                    "Idempotency-Key": crypto.randomUUID(),
                },
                ...(data === undefined ? {} : { data }),
            });
            expect(result.ok(), `${url}: ${result.status()}`).toBe(true);
            return result.json();
        }
        return { page, api, token, context };
    }
    try {
        const staff = await login("Staff"),
            kitchen = await login("Kitchen"),
            manager = await login("Manager");
        const day = await staff.api("/api/salesday/current");
        const menu = await staff.api("/api/catalog/menu");
        const dish = menu.items.find(
            (item: { code: string }) => item.code === "HAI_DEMO_FOOD",
        );
        expect(dish).toBeTruthy();
        const order = await staff.api("/api/sales/orders/", {
            type: "MANG_DI",
            businessDayId: day.id,
            customerName: `HAI REMAKE ${Date.now()}`,
        });
        const batch = await staff.api(`/api/sales/orders/${order.id}/batches`, {
            source: "NHAN_VIEN",
            lines: [
                {
                    menuItemId: dish.id,
                    variantId: dish.variants[0].id,
                    optionIds: [],
                },
            ],
        });
        await staff.api(
            `/api/sales/orders/${order.id}/batches/${batch.number}/confirm`,
            {},
        );
        const snapshotUrl = `/api/fulfillment/snapshot?orderId=${order.id}`;
        await expect
            .poll(async () => (await staff.api(snapshotUrl)).tickets.length)
            .toBe(1);
        const original = (await staff.api(snapshotUrl)).tickets[0];
        const invoiceBefore = await staff.api(
            `/api/billing/orders/${order.id}/invoice`,
        );
        await kitchen.page.goto("/kitchen");
        await expect(
            kitchen.page.getByText("Đã kết nối thời gian thực", {
                exact: true,
            }),
        ).toBeVisible({ timeout: 20000 });
        const reason = `HAI remake acceptance ${order.code}`;
        await kitchen.page
            .getByLabel("Phiếu chế biến", { exact: true })
            .selectOption(String(original.id));
        await kitchen.page
            .getByLabel("Lý do báo sự cố", { exact: true })
            .fill(reason);
        const reported = kitchen.page.waitForResponse(
            (r) =>
                r.url().endsWith(`/tickets/${original.id}/incidents`) &&
                r.request().method() === "POST",
        );
        await kitchen.page
            .getByRole("button", { name: "Gửi báo cáo", exact: true })
            .click();
        const reportedResponse = await reported;
        expect(reportedResponse.ok()).toBe(true);
        const incident = await reportedResponse.json();
        await expect(
            kitchen.page
                .getByRole("status")
                .filter({ hasText: "Đã gửi báo cáo sự cố." }),
        ).toBeVisible();
        const denied = await kitchen.context.request.post(
            `/api/fulfillment/incidents/${incident.id}/decision`,
            {
                headers: { Authorization: `Bearer ${kitchen.token}` },
                data: {
                    version: incident.version,
                    decision: "LAM_LAI",
                    reason,
                    requestKey: crypto.randomUUID(),
                },
            },
        );
        expect(denied.status()).toBe(403);
        await manager.page.goto(`/orders?view=incidents&orderId=${order.id}`);
        await manager.page
            .locator("article")
            .filter({ hasText: reason })
            .getByRole("button", { name: "Xử lý", exact: true })
            .click();
        await manager.page
            .getByLabel("Quyết định", { exact: true })
            .selectOption("LAM_LAI");
        await manager.page
            .getByLabel("Lý do xử lý", { exact: true })
            .fill("Nghiệm thu làm lại một lần");
        const decisionResponse = manager.page.waitForResponse(
            (r) =>
                r.url().endsWith(`/incidents/${incident.id}/decision`) &&
                r.request().method() === "POST",
        );
        await manager.page
            .getByRole("button", { name: "Xác nhận quyết định", exact: true })
            .click();
        const decidedResponse = await decisionResponse;
        expect(decidedResponse.ok()).toBe(true);
        const decided = await decidedResponse.json();
        expect(decided.remakeTicketId).toBeTruthy();
        // Replaying the exact UI request must not create a third attempt.
        const replay = await manager.api(
            `/api/fulfillment/incidents/${incident.id}/decision`,
            decidedResponse.request().postDataJSON(),
        );
        expect(replay.remakeTicketId).toBe(decided.remakeTicketId);
        const snapshot = await staff.api(snapshotUrl);
        expect(snapshot.tickets).toHaveLength(2);
        const old = snapshot.tickets.find(
            (row: { id: number }) => String(row.id) === String(original.id),
        );
        const remake = snapshot.tickets.find(
            (row: { id: number }) =>
                String(row.id) === String(decided.remakeTicketId),
        );
        expect(old.status).toBe("LOI");
        expect(remake.status).toBe("MOI");
        expect(String(remake.portionId)).toBe(String(original.portionId));
        const invoiceAfter = await staff.api(
            `/api/billing/orders/${order.id}/invoice`,
        );
        expect(invoiceAfter.lines).toHaveLength(invoiceBefore.lines.length);
        expect(invoiceAfter.due).toBe(invoiceBefore.due);
        const card = kitchen.page.locator("li").filter({ hasText: order.code });
        await expect(card).toHaveCount(1, { timeout: 20000 });
        await expect(
            card.getByRole("button", { name: "Bắt đầu", exact: true }),
        ).toBeVisible();
        expect(errors).toEqual([]);
        expect(websocketReady.Kitchen).toBe(true);
        fs.writeFileSync(
            path.resolve(
                "../artifacts/hai-live-demo/incident-remake-evidence.json",
            ),
            JSON.stringify(
                {
                    checkedAt: new Date().toISOString(),
                    orderId: order.id,
                    orderCode: order.code,
                    incidentId: incident.id,
                    originalTicketId: original.id,
                    remakeTicketId: remake.id,
                    ticketCountAfterReplay: snapshot.tickets.length,
                    invoiceDueBefore: invoiceBefore.due,
                    invoiceDueAfter: invoiceAfter.due,
                    kitchenDecisionStatus: denied.status(),
                    browserErrors: errors,
                    status: "PASS",
                },
                null,
                2,
            ),
        );
        await manager.page.screenshot({
            path: path.resolve(
                "../artifacts/hai-live-demo/incident-remake.png",
            ),
            fullPage: true,
        });
    } catch (error) {
        console.error(error);
        throw error;
    } finally {
        await Promise.all(contexts.map((context) => context.close()));
    }
});
