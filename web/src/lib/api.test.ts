// @vitest-environment node

import { http, HttpResponse } from "msw";
import { expect, test } from "vitest";
import { server } from "@/test/msw/server";

test("MSW intercepts backend API requests", async () => {
  const modules = ["catalog", "sales"];

  server.use(
    http.get("http://localhost:5080/api/system/modules", () =>
      HttpResponse.json(modules),
    ),
  );

  const response = await fetch("http://localhost:5080/api/system/modules");

  expect(response.status).toBe(200);
  await expect(response.json()).resolves.toEqual(modules);
});
