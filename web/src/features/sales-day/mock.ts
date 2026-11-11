import { http, HttpResponse } from "msw";
import type { BusinessDay, OpenDayInput } from "./api";
export function salesDayHandlers(
    initial: BusinessDay | null = null,
    allowed = true,
) {
    let current = initial;
    const requests = new Map<string, { input: string; day: BusinessDay }>();
    const problem = (code: string, status: number) =>
        HttpResponse.json({ title: code, status, code }, { status });
    return [
        http.get("*/api/salesday/current", ({ request }) =>
            request.headers.has("Authorization")
                ? HttpResponse.json(current)
                : problem("unauthorized", 401),
        ),
        http.post("*/api/salesday/open", async ({ request }) => {
            if (!request.headers.has("Authorization"))
                return problem("unauthorized", 401);
            if (!allowed) return problem("forbidden", 403);
            const key = request.headers.get("Idempotency-Key");
            if (
                !key ||
                !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
                    key,
                ) ||
                key === "00000000-0000-0000-0000-000000000000"
            )
                return problem("salesday.idempotency.required", 400);
            const input = (await request.json()) as OpenDayInput;
            if (
                !Number.isSafeInteger(input.openingCash) ||
                input.openingCash < 0 ||
                !/^\d{4}-\d{2}-\d{2}$/.test(input.businessDate)
            )
                return problem("salesday.input.invalid", 400);
            const previous = requests.get(key);
            if (previous)
                return previous.input === JSON.stringify(input)
                    ? HttpResponse.json(previous.day)
                    : problem("salesday.idempotency.conflict", 409);
            if (current) return problem("salesday.previous_not_closed", 409);
            current = {
                ...input,
                id: crypto.randomUUID(),
                openedAt: new Date().toISOString(),
                status: "DANG_BAN",
            };
            requests.set(key, { input: JSON.stringify(input), day: current });
            return HttpResponse.json(current, { status: 201 });
        }),
    ];
}
