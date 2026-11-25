import fs from "node:fs";
import path from "node:path";
import {
    HubConnectionBuilder,
    HubConnectionState,
    LogLevel,
} from "@microsoft/signalr";
const credentials = JSON.parse(
    fs.readFileSync(process.env.HAI_DEMO_CREDENTIAL_FILE, "utf8"),
);
if (!credentials.localOnly || credentials.origin !== "http://localhost:3187")
    throw new Error("Only the isolated local demo is allowed");
const account = credentials.accounts.find((x) => x.role === "Staff");
const base = credentials.origin;
let token = "",
    cookie = "",
    refreshing,
    refreshes = 0;
async function authenticate(refresh = false) {
    const response = await fetch(
        base + "/api/identity/" + (refresh ? "refresh" : "login"),
        {
            method: "POST",
            headers: refresh
                ? { Cookie: cookie }
                : { "Content-Type": "application/json" },
            body: refresh
                ? undefined
                : JSON.stringify({
                      userName: account.userName,
                      password: account.password,
                  }),
            redirect: "error",
        },
    );
    if (!response.ok) throw new Error("Authentication unavailable");
    cookie = response.headers.get("set-cookie")?.split(";")[0] ?? cookie;
    token = (await response.json()).accessToken;
    if (refresh) refreshes++;
    return token;
}
async function accessToken() {
    const exp = token
        ? JSON.parse(Buffer.from(token.split(".")[1], "base64url").toString())
              .exp * 1000
        : 0;
    if (exp > Date.now() + 30000) return token;
    refreshing ??= authenticate(true).finally(() => {
        refreshing = undefined;
    });
    return refreshing;
}
await authenticate();
const connections = [];
let reconnects = 0,
    failures = 0,
    finishing = false;
async function joined(hub) {
    await hub.invoke("JoinServing");
    const response = await fetch(base + "/api/fulfillment/snapshot", {
        headers: { Authorization: "Bearer " + (await accessToken()) },
    });
    if (!response.ok) throw new Error("Snapshot unavailable");
    const snapshot = await response.json();
    if (!Array.isArray(snapshot.tickets) || !Array.isArray(snapshot.tombstones))
        throw new Error("Snapshot contract mismatch");
}
try {
    await Promise.all(
        Array.from({ length: 50 }, async () => {
            const hub = new HubConnectionBuilder()
                .withUrl(base + "/hubs/operations", {
                    accessTokenFactory: accessToken,
                })
                .withAutomaticReconnect([0, 1000, 3000, 5000])
                .configureLogging(LogLevel.None)
                .build();
            hub.on("TicketChanged", () => {});
            hub.onreconnecting(() => reconnects++);
            hub.onreconnected(() => joined(hub).catch(() => failures++));
            hub.onclose(() => {
                if (!finishing) failures++;
            });
            connections.push(hub);
            await hub.start();
            await joined(hub);
        }),
    );
    const started = Date.now();
    console.log(
        "50 SignalR connections joined; 30-minute local proxy/session renewal test started.",
    );
    const progress = setInterval(
        () =>
            console.log(
                `Connected ${connections.filter((h) => h.state === HubConnectionState.Connected).length}/50; reconnects ${reconnects}; failures ${failures}`,
            ),
        60000,
    );
    await new Promise((resolve) => setTimeout(resolve, 1800000));
    clearInterval(progress);
    for (
        let i = 0;
        i < 10 &&
        connections.some((h) => h.state !== HubConnectionState.Connected);
        i++
    )
        await new Promise((resolve) => setTimeout(resolve, 1000));
    const active = connections.filter(
        (h) => h.state === HubConnectionState.Connected,
    ).length;
    const result = {
        status: failures === 0 && active === 50 ? "PASS" : "FAIL",
        environment: "isolated localhost demo via Nginx",
        connections: 50,
        activeAtEnd: active,
        durationMs: Date.now() - started,
        reconnects,
        refreshes,
        failures,
        note: "Expected JWT expiry reconnects are included; this is not a staging/Cloudflare TLS acceptance.",
    };
    fs.writeFileSync(
        path.resolve("../artifacts/hai-live-demo/signalr-30min.json"),
        JSON.stringify(result, null, 2),
    );
    console.log(JSON.stringify(result));
    process.exitCode = result.status === "PASS" ? 0 : 1;
} finally {
    finishing = true;
    await Promise.all(connections.map((hub) => hub.stop()));
    await fetch(base + "/api/identity/logout", {
        method: "POST",
        headers: { Authorization: "Bearer " + token, Cookie: cookie },
    }).catch(() => {});
}
