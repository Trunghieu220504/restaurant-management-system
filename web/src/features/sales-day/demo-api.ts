import { SalesDayError, type BusinessDay, type SalesDayApi } from "./api";
export function createDemoSalesDayApi(
    initial: BusinessDay | null,
    scenario: string,
): SalesDayApi {
    let current = initial;
    return {
        async current() {
            if (scenario === "error") throw new Error("offline");
            return current;
        },
        async open(input) {
            if (scenario === "forbidden")
                throw new SalesDayError(403, "forbidden");
            if (current)
                throw new SalesDayError(409, "salesday.previous_not_closed");
            current = {
                ...input,
                id: crypto.randomUUID(),
                status: "DANG_BAN",
                openedAt: new Date().toISOString(),
            };
            return current;
        },
    };
}
