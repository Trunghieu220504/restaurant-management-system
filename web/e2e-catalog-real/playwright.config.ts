import { defineConfig, devices } from "@playwright/test";
import path from "node:path";

const baseURL = process.env.PLAYWRIGHT_BASE_URL || "http://127.0.0.1:3322";
const webRoot = path.resolve(__dirname, "..");
const external = process.env.CATALOG_EXTERNAL_WEB === "1";

export default defineConfig({
    testDir: "..",
    testMatch: ["e2e/catalog.spec.ts", "e2e-catalog-real/catalog-real.spec.ts"],
    fullyParallel: false,
    workers: 1,
    reporter: "list",
    timeout: 60_000,
    use: { baseURL, trace: "retain-on-failure" },
    projects: [
        {
            name: "catalog-chromium",
            use: {
                ...devices["Desktop Chrome"],
                ...(process.env.CATALOG_BROWSER_CHANNEL
                    ? { channel: process.env.CATALOG_BROWSER_CHANNEL }
                    : {}),
            },
        },
    ],
    webServer: external
        ? undefined
        : {
              command: `"${process.execPath}" node_modules/next/dist/bin/next dev --hostname 127.0.0.1 --port ${new URL(baseURL).port || "3322"}`,
              cwd: webRoot,
              url: `${baseURL}/login`,
              reuseExistingServer: false,
              timeout: 120_000,
          },
});
