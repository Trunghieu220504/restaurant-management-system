import { defineConfig, devices } from "@playwright/test";

// An external URL tests an already-running image; otherwise use the local build.
const externalURL = process.env.PLAYWRIGHT_BASE_URL;
const baseURL = externalURL || "http://127.0.0.1:3100";

export default defineConfig({
    testDir: "./e2e-production",
    reporter: "list",
    use: { baseURL },
    projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
    webServer: externalURL
        ? undefined
        : {
              command:
                  "node node_modules/next/dist/bin/next start --hostname 127.0.0.1 --port 3100",
              url: `${baseURL}/login`,
              reuseExistingServer: false,
              timeout: 60_000,
          },
});
