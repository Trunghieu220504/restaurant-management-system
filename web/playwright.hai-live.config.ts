import { defineConfig } from "@playwright/test";
import fs from "node:fs";
const file = process.env.HAI_DEMO_CREDENTIAL_FILE;
if (!file || process.env.HAI_DEMO_ALLOW_WRITES !== "true")
    throw new Error(
        "Explicit isolated demo credentials and write consent are required.",
    );
const credentials = JSON.parse(fs.readFileSync(file, "utf8"));
if (!credentials.localOnly || credentials.origin !== "http://localhost:3187")
    throw new Error(
        "This acceptance suite only writes to the isolated localhost demo.",
    );
export default defineConfig({
    testDir: "./e2e-hai-live",
    workers: 1,
    retries: 0,
    timeout: 120000,
    reporter: "list",
    outputDir: "../artifacts/hai-live-demo/playwright",
    use: {
        baseURL: credentials.origin,
        trace: "off",
        screenshot: "off",
        video: "off",
        viewport: { width: 1440, height: 1000 },
    },
});
