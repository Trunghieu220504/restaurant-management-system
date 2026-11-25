import {
    SalesDayError,
    type BusinessDay,
    type CloseChecklist,
    type SalesDayApi,
} from "./api";
export function createDemoSalesDayApi(
    initial: BusinessDay | null,
    scenario: string,
): SalesDayApi {
    let current = initial;
    const checklist = (): CloseChecklist => ({
        dayId: current?.id ?? crypto.randomUUID(),
        status: current?.status ?? "DANG_BAN",
        blockers: [],
    });
    return {
        async current() {
            if (scenario === "error") throw new Error("offline");
            return current;
        },
        async checklist() {
            return checklist();
        },
        async beginClose() {
            if (!current)
                throw new SalesDayError(404, "salesday.day.not_found");
            current = { ...current, status: "CHO_CHOT" };
            return checklist();
        },
        async cancelClose() {
            if (!current)
                throw new SalesDayError(404, "salesday.day.not_found");
            current = { ...current, status: "DANG_BAN" };
            return checklist();
        },
        async settlementPreview() {
            return {
                openingCash: current?.openingCash ?? 0,
                expectedCash: current?.openingCash ?? 0,
                drawerPurchases: 0,
                netRevenue: 0,
                cost: 0,
                roundingDifference: 0,
                grossProfit: 0,
                ingredients: [],
                blockers: [],
            };
        },
        async finalize() {
            throw new SalesDayError(409, "salesday.close.blocked");
        },
        async snapshot() {
            return null;
        },
        async history() {
            return [];
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
