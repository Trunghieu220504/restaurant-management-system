import { authFetch } from "@/lib/auth/auth-client";
import type { components } from "@/lib/api/generated";

type Schema = components["schemas"];
export type InventoryCatalog = Schema["StockCatalog"];
export type StockCount = Schema["StockCountView"];

async function send<T>(
    request: typeof authFetch,
    path: string,
    init?: RequestInit,
): Promise<T> {
    const response = await request(path, { ...init, cache: "no-store" });
    if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        throw new Error(
            problem.code ?? problem.title ?? "inventory.unavailable",
        );
    }
    return response.json();
}

export function createInventoryApi(request = authFetch) {
    return {
        catalog: () =>
            send<InventoryCatalog>(request, "/api/inventory/catalog"),
        counts: (day: string) =>
            send<StockCount[]>(
                request,
                "/api/inventory/days/" + day + "/counts",
            ),
        saveCount: (
            day: string,
            ingredient: number | string,
            quantity: number,
            version: string | null,
        ) =>
            send<Schema["StockResult"]>(
                request,
                "/api/inventory/days/" + day + "/counts/" + ingredient,
                {
                    method: "PUT",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ quantity, version }),
                },
            ),
        approveCount: (
            day: string,
            ingredient: number | string,
            version: string,
        ) =>
            send<Schema["StockResult"]>(
                request,
                "/api/inventory/days/" +
                    day +
                    "/counts/" +
                    ingredient +
                    "/approve",
                {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ version }),
                },
            ),
    };
}
