"use client";

import { usePermissions } from "@/components/permission-provider";
import { KitchenLive } from "@/features/kitchen/kitchen-live";

export default function KitchenPage() {
    const { can } = usePermissions();
    const permissions = [
        "fulfillment.kitchen.process",
        "fulfillment.bar.process",
    ].filter(can);
    return (
        <section className="min-w-0 space-y-4">
            <h1 className="text-2xl font-semibold">Phiếu chế biến</h1>
            <KitchenLive permissions={permissions} />
        </section>
    );
}
