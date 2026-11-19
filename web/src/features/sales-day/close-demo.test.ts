import { expect, it } from "vitest";
import { closeDemo, initialCloseDay } from "./close-demo";
it("requires checklist clearance, counted cash and explicit discrepancy reason", () => {
    const day = initialCloseDay();
    expect(() => closeDemo(day, "close")).toThrow();
    const waiting = closeDemo(day, "request");
    expect(waiting.status).toBe("CHO_CHOT");
    expect(closeDemo(waiting, "cancel").status).toBe("DANG_BAN");
    expect(() => closeDemo(waiting, "close")).toThrow();
    const clear = {
        ...waiting,
        openOrders: 0,
        unapprovedCounts: 0,
        countedCash: waiting.expectedCash,
    };
    expect(closeDemo(clear, "close").status).toBe("DA_CHOT");
    expect(() => closeDemo({ ...clear, countedCash: 1 }, "close")).toThrow();
    expect(
        closeDemo(
            { ...clear, countedCash: 1, reason: "Count checked" },
            "close",
        ).status,
    ).toBe("DA_CHOT");
    expect(() => closeDemo(closeDemo(clear, "close"), "cancel")).toThrow();
});
