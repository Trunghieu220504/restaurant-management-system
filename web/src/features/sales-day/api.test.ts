// @vitest-environment node
import { beforeEach, expect, it } from "vitest";
import { server } from "@/test/msw/server";
import {
    createSalesDayApi,
    localBusinessDate,
    SalesDayError,
    parseDay,
} from "./api";
import { salesDayHandlers } from "./mock";
const request: typeof fetch = (url, init) =>
    fetch(new URL(String(url), "http://localhost:5080"), init);
const api = createSalesDayApi(() => "test-token", request);
const input = { businessDate: "2026-11-10", openingCash: 1000000 };
beforeEach(() => server.use(...salesDayHandlers()));
it("normalizes safe int64 strings, rejecting unsafe amounts and unknown statuses", () => {
    const day = {
        id: "day",
        businessDate: "2026-11-11",
        openingCash: "1000000",
        status: "DANG_BAN",
        openedAt: "2026-11-11T00:00:00Z",
    };
    expect(parseDay(day)?.openingCash).toBe(1000000);
    expect(() =>
        parseDay({ ...day, openingCash: "9007199254740993" }),
    ).toThrow();
    expect(() => parseDay({ ...day, status: "UNKNOWN" })).toThrow();
});
it("opens a day and replays the same key without creating another", async () => {
    expect(await api.current()).toBeNull();
    const key = crypto.randomUUID();
    const day = await api.open(input, key);
    expect(day.status).toBe("DANG_BAN");
    expect(await api.open(input, key)).toEqual(day);
    expect(await api.current()).toEqual(day);
    await expect(api.open(input, crypto.randomUUID())).rejects.toMatchObject({
        status: 409,
        code: "salesday.previous_not_closed",
    });
});
it("rejects reuse of an idempotency key with different content", async () => {
    const key = crypto.randomUUID();
    await api.open(input, key);
    await expect(
        api.open({ ...input, openingCash: 0 }, key),
    ).rejects.toMatchObject({ code: "salesday.idempotency.conflict" });
});
it("handles 401 and 403 without saving tokens", async () => {
    await expect(
        createSalesDayApi(() => null, request).current(),
    ).rejects.toBeInstanceOf(SalesDayError);
    server.use(...salesDayHandlers(null, false));
    await expect(api.open(input, crypto.randomUUID())).rejects.toMatchObject({
        status: 403,
    });
});
it("requires a UUID and valid money", async () => {
    await expect(api.open(input, "invalid")).rejects.toMatchObject({
        status: 400,
    });
    await expect(
        api.open({ ...input, openingCash: -1 }, crypto.randomUUID()),
    ).rejects.toMatchObject({ code: "salesday.input.invalid" });
});
it("uses the restaurant timezone rather than the host timezone", () => {
    expect(localBusinessDate(new Date("2026-11-09T18:00:00Z"))).toBe(
        "2026-11-10",
    );
});
