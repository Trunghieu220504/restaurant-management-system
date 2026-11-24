import { expect, test } from "@playwright/test";

// These tests exercise compiled browser JS with a mocked API, not staging auth.
const account = {
    id: "build-smoke-manager",
    userName: "build-smoke",
    email: null,
    roles: ["Manager"],
    permissions: ["salesday.manage"],
};

test("production bundle restores a session through refresh", async ({
    page,
}) => {
    let refreshCalls = 0;
    await page.route("**/api/identity/refresh", (route) => {
        refreshCalls++;
        expect(route.request().method()).toBe("POST");
        return route.fulfill({
            json: {
                accessToken: "build-smoke-access",
                expiresAt: new Date(Date.now() + 900_000).toISOString(),
                account,
            },
        });
    });
    await page.route("**/api/identity/me", (route) => {
        expect(route.request().headers().authorization).toBe(
            "Bearer build-smoke-access",
        );
        return route.fulfill({ json: account });
    });
    await page.route("**/api/salesday/current", (route) =>
        route.fulfill({ json: null }),
    );

    await page.goto("/sales-day");
    await expect(page.getByLabel("Tiền mặt đầu ngày")).toBeVisible();
    expect(refreshCalls).toBe(1);
    await page.reload();
    await expect(page.getByLabel("Tiền mặt đầu ngày")).toBeVisible();
    expect(refreshCalls).toBe(2);
    await expect(page).toHaveURL(/\/sales-day$/);
});

test("five concurrent expired requests share one refresh in the production bundle", async ({
    page,
}) => {
    let refreshCalls = 0;
    let expired = false;
    let rejected = 0;
    let recovered = 0;
    let releaseExpired!: () => void;
    const allExpired = new Promise<void>((resolve) => {
        releaseExpired = resolve;
    });
    await page.route("**/api/identity/refresh", (route) => {
        refreshCalls++;
        return route.fulfill({
            json: {
                accessToken: `concurrent-access-${refreshCalls}`,
                expiresAt: new Date(Date.now() + 900_000).toISOString(),
                account,
            },
        });
    });
    await page.route("**/api/identity/me", async (route) => {
        const token = route.request().headers().authorization;
        if (expired && token === "Bearer concurrent-access-1") {
            rejected++;
            if (rejected === 5) releaseExpired();
            await allExpired;
            return route.fulfill({ status: 401 });
        }
        if (expired) {
            expect(token).toBe("Bearer concurrent-access-2");
            recovered++;
        }
        return route.fulfill({ json: account });
    });
    await page.route("**/api/salesday/current", (route) =>
        route.fulfill({ json: null }),
    );
    await page.goto("/sales-day");
    await expect(page.getByLabel("Tiền mặt đầu ngày")).toBeVisible();
    expect(refreshCalls).toBe(1);

    expired = true;
    await page.evaluate(() => {
        for (let i = 0; i < 5; i++) window.dispatchEvent(new Event("focus"));
    });
    await expect.poll(() => recovered).toBe(5);
    expect(rejected).toBe(5);
    expect(refreshCalls).toBe(2); // one restore + one refresh for all five 401s
    await expect(page).toHaveURL(/\/sales-day$/);
});

test("production bundle refreshes after a protected API returns 401", async ({
    page,
}) => {
    let refreshCalls = 0;
    let expiredRequests = 0;
    let retriedRequests = 0;
    await page.route("**/api/identity/refresh", (route) => {
        refreshCalls++;
        return route.fulfill({
            json: {
                accessToken: `build-smoke-access-${refreshCalls}`,
                expiresAt: new Date(Date.now() + 900_000).toISOString(),
                account,
            },
        });
    });
    await page.route("**/api/identity/me", (route) =>
        route.fulfill({ json: account }),
    );
    await page.route("**/api/salesday/current", (route) => {
        if (expiredRequests === 0) {
            expiredRequests++;
            return route.fulfill({ status: 401 });
        }
        retriedRequests++;
        return route.fulfill({ json: null });
    });

    await page.goto("/sales-day");
    await expect(page.getByLabel("Tiền mặt đầu ngày")).toBeVisible();
    expect(expiredRequests).toBeGreaterThan(0);
    expect(retriedRequests).toBeGreaterThan(0);
    expect(refreshCalls).toBe(2);
    await expect(page).toHaveURL(/\/sales-day$/);
});
