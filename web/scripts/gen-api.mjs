import { mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { loadEnvConfig } from "@next/env";
import { astToString, default as openapiTS } from "openapi-typescript";

loadEnvConfig(process.cwd());

const apiUrl = (process.env.API_URL || "http://localhost:5080").replace(/\/+$/, "");
const specUrl = `${apiUrl}/openapi/v1.json`;
const outputPath = resolve("src/lib/api/schema.d.ts");

const schema = await openapiTS(specUrl);
await mkdir(dirname(outputPath), { recursive: true });
await writeFile(outputPath, astToString(schema), "utf8");
console.info(`Generated API types from ${specUrl} at ${outputPath}`);
