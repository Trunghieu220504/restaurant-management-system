import { chromium } from "@playwright/test";
import { readFile, writeFile } from "node:fs/promises";
import { randomBytes } from "node:crypto";

// Real browser + real HTTP only: no route mocks, clock changes, HAR or traces.
const session = JSON.parse(
    await readFile(process.env.WEEK1_SESSION_FILE, "utf8"),
);
const baseURL = process.env.WEEK1_BASE_URL;
const local = session.environment === "isolated-local";
if (!["isolated-local", "staging"].includes(session.environment))
    throw new Error("Unknown session environment.");
if (
    !baseURL ||
    (local
        ? !["localhost", "127.0.0.1"].includes(new URL(baseURL).hostname)
        : baseURL !== "https://staging.quanlynh.id.vn" ||
          process.env.WEEK1_TEST_DATA_CONFIRMED !== "true")
) {
    throw new Error(
        "Explicit allowed target and test-data confirmation required.",
    );
}
for (const key of ["manager", "staff", "lockout"]) {
    if (
        !session.accounts?.[key]?.userName?.startsWith("w1-e2e-") ||
        !session.accounts[key].password
    )
        throw new Error(
            "Dedicated w1-e2e accounts required; never use a personal or bootstrap account.",
        );
}
const output = process.env.WEEK1_REPORT_FILE;
if (!output) throw new Error("Set WEEK1_REPORT_FILE.");
const loginData = ({ userName, password }) => ({ userName, password });
if (process.argv.includes("--cleanup-only")) {
    for (const key of ["manager", "staff"]) {
        const response = await fetch(`${baseURL}/api/identity/login`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(loginData(session.accounts[key])),
        });
        if (response.status !== 401)
            throw new Error(
                "Disabled test account verification failed (expected 401, not rate-limit 429).",
            );
    }
    console.log(
        "CLEANUP: Manager/Staff login rejected=401 after exact-ID deactivation.",
    );
    process.exit(0);
}
const report = {
    environment: session.environment,
    baseURL,
    startedAt: new Date().toISOString(),
    tests: [],
};
const secrets = Object.values(session.accounts).map((a) => a.password);
const redact = (s) =>
    secrets.reduce((text, secret) => text.replaceAll(secret, "[redacted]"), s);
function check(value, label) {
    if (!value) throw new Error(label);
}
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
let browser;
const contexts = [];
async function page() {
    const context = await browser.newContext({ baseURL });
    contexts.push(context);
    const page = await context.newPage();
    const network = [];
    page.on("response", (response) => {
        const path = new URL(response.url()).pathname;
        if (path.startsWith("/api/"))
            network.push({ path, status: response.status(), at: Date.now() });
    });
    return { context, page, network };
}
async function login(tab, account) {
    await tab.page.goto("/login");
    await tab.page
        .getByLabel("Tài khoản", { exact: true })
        .fill(account.userName);
    await tab.page
        .getByLabel("Mật khẩu", { exact: true })
        .fill(account.password);
    const pending = tab.page.waitForResponse(
        (r) => new URL(r.url()).pathname === "/api/identity/login",
    );
    await tab.page
        .getByRole("button", { name: "Đăng nhập", exact: true })
        .click();
    const response = await pending;
    check(response.status() === 200, "Dedicated account login must return 200");
    const data = await response.json();
    secrets.push(data.accessToken);
    await tab.page.waitForURL(/\/sales-day$/);
    return data;
}
async function me(tab, token) {
    return tab.context.request.get("/api/identity/me", {
        headers: { Authorization: `Bearer ${token}` },
    });
}
async function current(tab, token) {
    return tab.context.request.get("/api/salesday/current", {
        headers: { Authorization: `Bearer ${token}` },
    });
}
async function cookie(tab) {
    const rt = (
        await tab.context.cookies(`${baseURL}/api/identity/refresh`)
    ).find((c) => c.name === "rt");
    check(rt?.value, "Real HTTP-only refresh cookie must exist");
    secrets.push(rt.value);
    return rt.value;
}
async function revoked(tab, token, rt) {
    check(
        (await me(tab, token)).status() === 401,
        "Old access token must be rejected server-side",
    );
    const replay = await tab.context.request.post("/api/identity/refresh", {
        headers: { Cookie: `rt=${rt}` },
    });
    check(
        replay.status() === 401,
        "Old refresh cookie must be rejected server-side",
    );
}
let stage = "browser-startup";
const pass = (id, evidence) => {
    report.tests.push({
        id,
        status: "PASS",
        evidence,
        at: new Date().toISOString(),
    });
    console.log(`${id}: PASS`);
};
try {
    browser = await chromium.launch();
    stage = "W1-E01";
    const a = await page();
    let loginA = await login(a, session.accounts.manager);
    const account = await me(a, loginA.accessToken);
    check(
        account.status() === 200 &&
            (await account.json()).permissions.includes("salesday.manage"),
        "Manager permission missing",
    );
    await cookie(a);
    pass(
        stage,
        "Browser login=200; real me=200 with salesday.manage; refresh cookie present",
    );

    stage = "W1-E02";
    const before = await current(a, loginA.accessToken);
    const beforeBody = await before.text();
    // Results.Ok(null) may return an empty 200 body; the production client accepts both forms.
    check(
        before.status() === 200 &&
            (!beforeBody || JSON.parse(beforeBody) === null),
        "BLOCKED: an active day exists; do not reset/delete it",
    );
    const b = await page();
    let loginB = await login(b, session.accounts.manager);
    await b.page.getByLabel("Tiền mặt đầu ngày").waitFor();
    await a.page.getByLabel("Tiền mặt đầu ngày").fill("1000");
    const opened = a.page.waitForResponse(
        (r) => new URL(r.url()).pathname === "/api/salesday/open",
    );
    await a.page
        .getByRole("button", { name: "Mở ngày bán", exact: true })
        .click();
    const first = await opened;
    check(first.status() === 201, "First open must return 201");
    const day = await first.json();
    const firstKey = first.request().headers()["idempotency-key"];
    check(day.status === "DANG_BAN" && firstKey, "Open response/key invalid");
    pass(stage, "Empty current day -> browser open=201, DANG_BAN");

    stage = "W1-E03";
    await b.page.getByLabel("Tiền mặt đầu ngày").fill("1000");
    const duplicated = b.page.waitForResponse(
        (r) => new URL(r.url()).pathname === "/api/salesday/open",
    );
    await b.page
        .getByRole("button", { name: "Mở ngày bán", exact: true })
        .click();
    const second = await duplicated;
    check(
        second.request().headers()["idempotency-key"] !== firstKey,
        "Duplicate test needs a new key, not replay",
    );
    check(
        second.status() === 409 &&
            (await second.json()).code === "salesday.previous_not_closed",
        "Second independent open must conflict",
    );
    check(
        (await (await current(a, loginA.accessToken)).json()).id === day.id,
        "Current day ID changed",
    );
    pass(
        stage,
        "Independent browser context/new key=409; current day unchanged",
    );

    stage = "W1-E04";
    const deadline = Date.parse(loginA.expiresAt) + 35_000;
    check(
        Number.isFinite(deadline) && deadline - Date.now() < 17 * 60_000,
        "Unexpected JWT lifetime",
    );
    console.log(
        "W1-E04: waiting for real JWT expiration and clock skew; no clock or response mocks",
    );
    while (Date.now() < deadline) {
        await sleep(Math.min(30_000, deadline - Date.now()));
        console.log(
            `W1-E04: ${Math.max(0, Math.ceil((deadline - Date.now()) / 1000))} seconds until expiry check`,
        );
    }
    await a.page.bringToFront();
    await a.page.getByRole("button", { name: "Tải lại ngày bán" }).click();
    await a.page.getByText("ĐANG BÁN", { exact: true }).waitFor();
    const until = Date.now() + 15_000;
    let sequence;
    do {
        const expired = a.network.findIndex(
            (r) => r.status === 401 && r.at >= Date.parse(loginA.expiresAt),
        );
        const refresh = a.network.findIndex(
            (r, i) =>
                i > expired &&
                r.path === "/api/identity/refresh" &&
                r.status === 200,
        );
        const retry = a.network.findIndex(
            (r, i) =>
                i > refresh &&
                expired >= 0 &&
                r.path === a.network[expired].path &&
                r.status === 200,
        );
        sequence = expired >= 0 && refresh > expired && retry > refresh;
        if (!sequence) await sleep(250);
    } while (!sequence && Date.now() < until);
    check(
        sequence,
        "Missing real expiry -> 401 -> refresh 200 -> retry 200 sequence",
    );
    check(
        !a.page.url().endsWith("/login"),
        "Browser lost session after refresh",
    );
    pass(
        stage,
        "Real 15-minute JWT expiry plus skew; observed 401 -> refresh 200 -> retry 200; UI retains session",
    );

    stage = "W1-E05";
    // Obtain the current token via a real refresh in this same browser; keep it only in memory.
    const fresh = await a.context.request.post("/api/identity/refresh");
    check(fresh.status() === 200, "Refresh before logout failed");
    const saved = await fresh.json();
    secrets.push(saved.accessToken);
    // A page reload restores the newest cookie/token before interacting with logout.
    await a.page.reload();
    await a.page
        .getByRole("button", { name: "Đăng xuất phiên làm việc" })
        .waitFor();
    const savedCookie = await cookie(a);
    const logout = a.page.waitForResponse(
        (r) => new URL(r.url()).pathname === "/api/identity/logout",
    );
    await a.page
        .getByRole("button", { name: "Đăng xuất phiên làm việc" })
        .click();
    check((await logout).status() === 204, "Logout must return 204");
    await a.page.waitForURL(/\/login$/);
    await revoked(a, saved.accessToken, savedCookie);
    pass(
        stage,
        "Browser logout=204/login; saved access and latest refresh rejected=401",
    );

    stage = "W1-E06";
    loginA = await login(a, session.accounts.manager);
    await b.context.close();
    const other = await page();
    loginB = await login(other, session.accounts.manager);
    const otherCookie = await cookie(other);
    const oldPassword = session.accounts.manager.password;
    const newPassword = randomBytes(24).toString("base64") + "Aa1!";
    secrets.push(newPassword);
    await a.page.goto("/profile/password");
    await a.page
        .getByLabel("Mật khẩu hiện tại", { exact: true })
        .fill(session.accounts.manager.password);
    await a.page.getByLabel("Mật khẩu mới", { exact: true }).fill(newPassword);
    await a.page.getByLabel("Nhập lại mật khẩu mới").fill(newPassword);
    const changed = a.page.waitForResponse(
        (r) => new URL(r.url()).pathname === "/api/identity/change-password",
    );
    await a.page
        .getByRole("button", { name: "Đổi mật khẩu", exact: true })
        .click();
    check((await changed).status() === 204, "Password change must return 204");
    session.accounts.manager.password = newPassword;
    // Preserve the current test password privately so post-cleanup checks cannot use an obsolete password.
    await writeFile(process.env.WEEK1_SESSION_FILE, JSON.stringify(session));
    await a.page.waitForURL(/\/login$/);
    await revoked(other, loginB.accessToken, otherCookie);
    await other.page.goto("/sales-day");
    await other.page.waitForURL(/\/login$/);
    const oldLogin = await a.context.request.post("/api/identity/login", {
        data: {
            userName: session.accounts.manager.userName,
            password: oldPassword,
        },
    });
    check(oldLogin.status() === 401, "Old password must fail");
    await login(a, { ...session.accounts.manager, password: newPassword });
    pass(
        stage,
        "Password change=204; other device access/refresh=401 and UI login; old password rejected/new accepted",
    );

    stage = "INF10-lockout";
    console.log(
        "INF10-lockout: waiting 65 seconds to isolate from the login rate-limit window",
    );
    await sleep(65_000);
    const lock = session.accounts.lockout;
    check(
        (
            await a.context.request.post("/api/identity/login", {
                data: loginData(lock),
            })
        ).status() === 200,
        "Lockout account must initially login",
    );
    for (let i = 0; i < 5; i++) {
        check(
            (
                await a.context.request.post("/api/identity/login", {
                    data: {
                        userName: lock.userName,
                        password: "WrongTestPassword123!",
                    },
                })
            ).status() === 401,
            "Each wrong password must return 401, not 429",
        );
    }
    check(
        (
            await a.context.request.post("/api/identity/login", {
                data: loginData(lock),
            })
        ).status() === 401,
        "Correct password must fail after five wrong attempts",
    );
    pass(
        stage,
        "Dedicated account: correct=200; five wrong=401; correct after lockout=401 (not rate-limit 429)",
    );
} catch (error) {
    report.tests.push({
        id: stage,
        status: error.message.startsWith("BLOCKED:") ? "BLOCKED" : "FAIL",
        error: redact(String(error.message).split("\n")[0]),
    });
    process.exitCode = 1;
} finally {
    if (browser) await browser.close();
    report.finishedAt = new Date().toISOString();
    await writeFile(output, JSON.stringify(report, null, 2));
    console.log(
        `Sanitized report written. ${report.tests.filter((t) => t.status === "PASS").length}/7 PASS.`,
    );
}
