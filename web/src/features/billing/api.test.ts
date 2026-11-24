import { describe, expect, it, vi } from "vitest";
import { createBillingApi } from "./api";

describe("billing api", () => {
    it("loads an invoice", async () => {
        const request = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({ id: 7 }), { status: 200 }),
        );
        const api = createBillingApi(request);

        await expect(api.invoice(7)).resolves.toEqual({ id: 7 });
        expect(request).toHaveBeenCalledWith(
            "/api/billing/invoices/7",
            expect.objectContaining({ cache: "no-store" }),
        );
    });

    it("sends the required idempotency key and Vietnamese payment method", async () => {
        const request = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({ resourceId: 9, replayed: false }), {
                status: 200,
            }),
        );
        const api = createBillingApi(request);

        await api.collect(
            7,
            { businessDayId: "day", method: "TIEN_MAT", amount: 100000, tendered: 120000 },
            "request-key",
        );
        const [, init] = request.mock.calls[0] as [string, RequestInit];
        expect(init.headers).toMatchObject({ "Idempotency-Key": "request-key" });
        expect(JSON.parse(String(init.body))).toMatchObject({
            method: "TIEN_MAT",
            amount: 100000,
            tendered: 120000,
        });
    });

    it("surfaces backend billing errors", async () => {
        const request = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({ code: "billing.payment.exceeds_due" }), {
                status: 409,
            }),
        );
        const api = createBillingApi(request);

        await expect(
            api.collect(7, { businessDayId: "day", method: "TIEN_MAT", amount: 1 }, "key"),
        ).rejects.toMatchObject({ status: 409, code: "billing.payment.exceeds_due" });
    });
});
