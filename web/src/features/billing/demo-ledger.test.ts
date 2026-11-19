import { expect, it } from "vitest";
import { createDemoLedger, remaining } from "./demo-ledger";

it("supports partial cash payments, change and idempotent replay", async () => {
    const api = createDemoLedger();
    const input = { amount: 50000, method: "cash" as const, tendered: 100000 };
    const first = await api.pay(input, "key-1");
    const replay = await api.pay(input, "key-1");
    expect(first).toEqual(replay);
    expect(first.payments[0].change).toBe(50000);
    expect(remaining(first)).toBe(100000);
    const paid = await api.pay(
        { amount: 100000, method: "cash", tendered: 100000 },
        "key-2",
    );
    expect(remaining(paid)).toBe(0);
    expect(paid.payments).toHaveLength(2);
});

it("rejects overpayment, invalid money and conflicting idempotency keys", async () => {
    const api = createDemoLedger();
    await expect(
        api.pay({ amount: 150001, method: "cash", tendered: 150001 }, "a"),
    ).rejects.toThrow();
    await expect(
        api.pay({ amount: 0.5, method: "cash", tendered: 1 }, "b"),
    ).rejects.toThrow();
    await expect(
        api.pay({ amount: 100, method: "cash", tendered: 50 }, "c"),
    ).rejects.toThrow();
    await api.pay({ amount: 100, method: "cash", tendered: 100 }, "d");
    await expect(
        api.pay({ amount: 101, method: "cash", tendered: 101 }, "d"),
    ).rejects.toThrow();
});

it("reserves pending transfers, confirms once and allows cancellation", async () => {
    const api = createDemoLedger();
    const pending = await api.pay(
        { amount: 150000, method: "transfer", reference: "BANK-1" },
        "a",
    );
    expect(remaining(pending)).toBe(150000);
    await expect(
        api.pay({ amount: 1, method: "cash", tendered: 1 }, "b"),
    ).rejects.toThrow();
    const paid = await api.confirm(pending.payments[0].id, "c");
    expect(remaining(paid)).toBe(0);
    expect(await api.confirm(pending.payments[0].id, "c")).toEqual(paid);
    await expect(api.confirm(pending.payments[0].id, "d")).rejects.toThrow();
    const other = createDemoLedger();
    const transfer = await other.pay(
        { amount: 1000, method: "transfer", reference: "BANK-2" },
        "e",
    );
    const cancelled = await other.cancel(transfer.payments[0].id, "f");
    expect(cancelled.payments[0].status).toBe("cancelled");
});

it("waives only unpaid, unreserved amounts and requires permission and reason", async () => {
    const api = createDemoLedger();
    await api.pay({ amount: 50000, method: "cash", tendered: 50000 }, "a");
    await expect(
        api.waive(100001, "approved reason", "b", true),
    ).rejects.toThrow();
    await expect(api.waive(1000, "", "c", true)).rejects.toThrow();
    await expect(api.waive(1000, "reason", "d", false)).rejects.toThrow();
    const waived = await api.waive(100000, "Manager approval", "e", true);
    expect(remaining(waived)).toBe(0);
    expect(waived.waivers).toHaveLength(1);
});

it("serializes competing commands and does not expose mutable state", async () => {
    const api = createDemoLedger();
    const result = await Promise.allSettled([
        api.pay({ amount: 150000, method: "cash", tendered: 150000 }, "a"),
        api.pay({ amount: 150000, method: "cash", tendered: 150000 }, "b"),
    ]);
    expect(result.filter((item) => item.status === "fulfilled")).toHaveLength(
        1,
    );
    const copy = await api.current();
    copy.payments.length = 0;
    expect((await api.current()).payments).toHaveLength(1);
});
