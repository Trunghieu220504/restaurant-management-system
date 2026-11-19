import { chromium } from "@playwright/test";
import { writeFile } from "node:fs/promises";

const baseURL = "https://staging.quanlynh.id.vn";
const report = {
    environment: "staging-public-no-credentials",
    startedAt: new Date().toISOString(),
    tests: [],
};
const browser = await chromium.launch();
const context = await browser.newContext({ baseURL });
const page = await context.newPage();
const check = (condition, id) => {
    if (!condition) throw new Error(id);
    report.tests.push({ id, status: "PASS" });
};
try {
    const refresh = page.waitForResponse(
        (r) => new URL(r.url()).pathname === "/api/identity/refresh",
    );
    await page.goto("/login");
    await page.getByLabel("Tài khoản", { exact: true }).waitFor();
    check(
        (await refresh).status() === 401,
        "REFRESH_BUILD_FLAG-real-unauthenticated-refresh",
    );
    for (const route of ["/sales-day", "/profile/password"]) {
        await page.goto(route);
        await page.waitForURL((url) => url.pathname === "/login");
        check(true, `ANONYMOUS_REDIRECT-${route}`);
    }
    for (const route of ["/api/identity/me", "/api/salesday/current"]) {
        check(
            (await context.request.get(route)).status() === 401,
            `ANONYMOUS_API-${route}`,
        );
    }
    const root = await context.request.get("/");
    check(
        root.status() === 200 && !!root.headers()["strict-transport-security"],
        "HTTPS-HSTS",
    );
} catch (error) {
    report.tests.push({
        id: "public-browser",
        status: "FAIL",
        error: String(error.message).split("\n")[0],
    });
    process.exitCode = 1;
} finally {
    await browser.close();
    report.finishedAt = new Date().toISOString();
    await writeFile(
        process.env.WEEK1_REPORT_FILE,
        JSON.stringify(report, null, 2),
    );
    console.log(JSON.stringify(report));
}
