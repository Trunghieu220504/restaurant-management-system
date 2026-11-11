import { expect, it } from "vitest";
import { elapsedSeconds, money, parseVnd } from "./controls";
it("accepts only safe nonnegative integer money", () => {
    expect(parseVnd("1000000")).toBe(1000000);
    expect(parseVnd("0")).toBe(0);
    for (const raw of ["", "-1", "1.5", "1,5", "1e3", "9007199254740992"])
        expect(parseVnd(raw)).toBeNull();
    expect(money(1000000)).toBe("1.000.000");
});
it("derives waiting time from timestamps, not accumulated ticks", () => {
    const since = "2026-11-10T00:00:00Z";
    expect(elapsedSeconds(since, Date.parse(since) + 600000)).toBe(600);
    expect(elapsedSeconds(since, Date.parse(since) - 1000)).toBe(0);
    expect(elapsedSeconds("invalid", Date.now())).toBeNull();
});
