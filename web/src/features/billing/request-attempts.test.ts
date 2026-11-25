import { describe, expect, it } from "vitest";
import { RequestAttempts } from "./request-attempts";
describe("money request attempts", () => {
    it("reuses the same key after an unknown outcome and changes it only after a confirmed result", () => {
        const attempts = new RequestAttempts();
        const first = attempts.begin("collect", { amount: 20000 });
        expect(attempts.begin("collect", { amount: 20000 })).toBe(first);
        attempts.complete("collect", first);
        expect(attempts.begin("collect", { amount: 20000 })).not.toBe(first);
    });
    it("isolates changed payloads and never lets an old response clear a newer attempt", () => {
        const attempts = new RequestAttempts();
        const old = attempts.begin("collect", { amount: 10000 });
        const current = attempts.begin("collect", { amount: 20000 });
        attempts.complete("collect", old);
        expect(attempts.begin("collect", { amount: 20000 })).toBe(current);
        expect(attempts.begin("waive", { amount: 20000 })).not.toBe(current);
    });
});
