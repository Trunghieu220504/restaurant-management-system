import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.STAGING_BASE_URL;
if (!baseURL || !baseURL.startsWith("https://")) {
    throw new Error(
        "Set STAGING_BASE_URL to the approved HTTPS staging origin.",
    );
}
const url = new URL(baseURL);
if (
    url.username ||
    url.password ||
    url.pathname !== "/" ||
    url.search ||
    url.hash
) {
    throw new Error(
        "STAGING_BASE_URL must be an origin without credentials or a path.",
    );
}
export default defineConfig({
    testDir: "./e2e-staging",
    workers: 1,
    retries: 0,
    reporter: "list",
    timeout: 60000,
    use: { baseURL, trace: "off", screenshot: "off", video: "off" },
    projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
