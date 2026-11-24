import { describe, expect, it, vi } from "vitest";
import { createFulfillmentApi, type FulfillmentTicket } from "./api";

const ticket: FulfillmentTicket = {
    id: 42,
    portionId: 11,
    station: "BEP",
    status: "MOI",
    createdAt: "2026-11-25T00:00:00Z",
    startedBy: null,
    startedAt: null,
    finishedBy: null,
    finishedAt: null,
    deliveredBy: null,
    deliveredAt: null,
    version: "AQ==",
};

describe("fulfillment api", () => {
    it("loads a station queue", async () => {
        const request = vi.fn().mockResolvedValue(
            new Response(JSON.stringify([ticket]), { status: 200 }),
        );
        const api = createFulfillmentApi(request);

        await expect(api.queue("BEP")).resolves.toEqual([ticket]);
        expect(request).toHaveBeenCalledWith(
            "/api/fulfillment/queue/BEP",
            expect.objectContaining({ cache: "no-store" }),
        );
    });

    it("sends the ticket version and station for transitions", async () => {
        const request = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({ ...ticket, status: "DANG_LAM" }), {
                status: 200,
            }),
        );
        const api = createFulfillmentApi(request);

        await api.transition(ticket, "start");
        const [, init] = request.mock.calls[0] as [string, RequestInit];
        expect(JSON.parse(String(init.body))).toEqual({
            station: "BEP",
            version: "AQ==",
            requestKey: expect.any(String),
        });
    });

    it("exposes backend problem codes", async () => {
        const request = vi.fn().mockResolvedValue(
            new Response(
                JSON.stringify({
                    title: "fulfillment.ticket.invalid_transition",
                    code: "fulfillment.ticket.invalid_transition",
                }),
                { status: 409 },
            ),
        );
        const api = createFulfillmentApi(request);

        await expect(api.transition(ticket, "start")).rejects.toMatchObject({
            status: 409,
            code: "fulfillment.ticket.invalid_transition",
        });
    });
});
