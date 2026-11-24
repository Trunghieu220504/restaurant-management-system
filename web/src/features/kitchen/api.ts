import { authFetch } from "@/lib/auth/auth-client";

export type FulfillmentStation = "BEP" | "BAR";
export type FulfillmentStatus = "MOI" | "DANG_LAM" | "SAN_SANG" | "DA_HUY";

export type FulfillmentTicket = {
    id: number | string;
    portionId: number | string;
    station: FulfillmentStation;
    status: FulfillmentStatus;
    createdAt: string;
    startedBy: string | null;
    startedAt: string | null;
    finishedBy: string | null;
    finishedAt: string | null;
    deliveredBy: string | null;
    deliveredAt: string | null;
    version: string;
};

export class FulfillmentApiError extends Error {
    constructor(readonly status: number, readonly code: string) {
        super(code);
    }
}

function numberId(value: number | string) {
    const result = Number(value);
    if (!Number.isSafeInteger(result) || result <= 0)
        throw new Error("Invalid fulfillment ticket id");
    return result;
}

export function createFulfillmentApi(request: typeof authFetch = authFetch) {
    async function response<T>(path: string, init?: RequestInit): Promise<T> {
        const result = await request(path, { ...init, cache: "no-store" });
        if (!result.ok) {
            const problem = await result.json().catch(() => ({}));
            throw new FulfillmentApiError(
                result.status,
                typeof problem?.code === "string"
                    ? problem.code
                    : typeof problem?.title === "string"
                      ? problem.title
                      : "fulfillment.unavailable",
            );
        }
        return result.json();
    }

    return {
        queue(station: FulfillmentStation, signal?: AbortSignal) {
            return response<FulfillmentTicket[]>(
                "/api/fulfillment/queue/" + station,
                { signal },
            );
        },
        transition(
            ticket: FulfillmentTicket,
            action: "start" | "ready" | "deliver" | "cancel",
        ) {
            return response<FulfillmentTicket>(
                "/api/fulfillment/tickets/" + numberId(ticket.id) + "/" + action,
                {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        station: ticket.station,
                        version: ticket.version,
                        requestKey: crypto.randomUUID(),
                    }),
                },
            );
        },
    };
}
