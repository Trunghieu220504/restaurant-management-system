import type { CatalogMenu } from "@/features/catalog/api";

export type Choice = { itemId: string; variantId: string; options: string[] };
export type Line = Choice & {
    id: string;
    label: string;
    unitPrice: number;
    quantity: number;
    cancelled?: number;
    cancelReason?: string;
    note?: string;
    editReason?: string;
};
export type Destination =
    | { kind: "table"; session: string; name: string }
    | { kind: "takeaway"; name: string; phone: string; pickup: string };
export type Batch = {
    id: string;
    lines: Line[];
    status: "pending" | "confirmed" | "rejected" | "cancelled";
    reason?: string;
};
export type StaffOrder = {
    id: string;
    destination: Destination;
    batches: Batch[];
};

export function quote(menu: CatalogMenu, choice: Choice) {
    const item = menu.items.find(
        (i) =>
            i.active &&
            i.id === choice.itemId &&
            menu.groups.some((g) => g.active && g.id === i.groupId),
    );
    const variant = item?.variants.find(
        (v) => v.active && v.id === choice.variantId,
    );
    if (!item || !variant) throw Error("Món hoặc biến thể đã ngừng bán.");
    let unitPrice = variant.price;
    const names = [item.name, variant.name];
    const matched = new Set<string>();
    for (const assignment of item.optionGroups) {
        const group = menu.optionGroups.find(
            (g) => g.active && g.id === assignment.optionGroupId,
        );
        const options =
            group?.options.filter(
                (o) => o.active && choice.options.includes(o.id),
            ) ?? [];
        if (
            options.length < assignment.minSelections ||
            options.length > assignment.maxSelections
        )
            throw Error(
                `Chọn từ ${assignment.minSelections} đến ${assignment.maxSelections} mục trong nhóm ${group?.name ?? "tùy chọn"}.`,
            );
        for (const option of options) {
            unitPrice += option.extraPrice;
            names.push(option.name);
            matched.add(option.id);
        }
    }
    if (matched.size !== choice.options.length)
        throw Error("Tùy chọn đã thay đổi. Vui lòng chọn lại món.");
    return { unitPrice, label: names.join(" · ") };
}

// Temporary Sales adapter until HIE-3.1 publishes its command contract.
export function appendBatch(
    orders: StaffOrder[],
    destination: Destination,
    lines: Line[],
): StaffOrder[] {
    const batch: Batch = { id: crypto.randomUUID(), status: "pending", lines };
    const existing =
        destination.kind === "table" &&
        orders.find(
            (o) =>
                o.destination.kind === "table" &&
                o.destination.session === destination.session,
        );
    if (existing)
        return orders.map((o) =>
            o.id === existing.id ? { ...o, batches: [...o.batches, batch] } : o,
        );
    return [
        { id: crypto.randomUUID(), destination, batches: [batch] },
        ...orders,
    ];
}
