import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { authFetch } from "@/lib/auth/auth-client";
import { liveInterval } from "@/lib/realtime/live-query";
import type { Choice } from "@/features/orders/batches";
import type { CatalogMenu, MenuItem } from "./api";

const targetKind = z.enum(["ITEM", "VARIANT", "TOPPING", "OPTION"]);
export type TargetKind = z.infer<typeof targetKind>;
const snapshot = z.object({
    businessDayId: z.string().nullable(),
    soldOut: z.array(z.object({ kind: targetKind, targetId: z.string() })),
});
export type Availability = z.infer<typeof snapshot>;
export async function readAvailability(signal?: AbortSignal) {
    const res = await authFetch("/api/catalog/availability", {
        signal,
        cache: "no-store",
    });
    if (!res.ok) throw Error("Không thể kiểm tra trạng thái hết món.");
    return snapshot.parse(await res.json());
}
export function useAvailability(enabled = true) {
    return useQuery({
        queryKey: ["catalog", "availability"],
        queryFn: ({ signal }) => readAvailability(signal),
        enabled,
        refetchInterval: liveInterval,
    });
}
export function isSoldOut(state: Availability, kind: TargetKind, id: string) {
    return state.soldOut.some(
        (row) => row.kind === kind && row.targetId === id,
    );
}
export function unavailableChoice(state: Availability, choice: Choice) {
    return (
        !state.businessDayId ||
        isSoldOut(state, "ITEM", choice.itemId) ||
        isSoldOut(state, "VARIANT", choice.variantId) ||
        state.soldOut.some(
            (row) =>
                (row.kind === "OPTION" || row.kind === "TOPPING") &&
                choice.options.includes(row.targetId),
        )
    );
}
export function unavailableItem(
    state: Availability,
    menu: CatalogMenu,
    item: MenuItem,
) {
    if (
        isSoldOut(state, "ITEM", item.id) ||
        !item.variants.some(
            (v) => v.active && !isSoldOut(state, "VARIANT", v.id),
        )
    )
        return true;
    return item.optionGroups.some((a) => {
        const group = menu.optionGroups.find(
            (g) => g.id === a.optionGroupId && g.active,
        );
        const remaining =
            group?.options.filter(
                (o) =>
                    o.active &&
                    !isSoldOut(
                        state,
                        group.type === "TOPPING" ? "TOPPING" : "OPTION",
                        o.id,
                    ),
            ).length ?? 0;
        return remaining < a.minSelections;
    });
}
