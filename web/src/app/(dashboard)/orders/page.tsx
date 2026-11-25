"use client";
import { Suspense, useState } from "react";
import dynamic from "next/dynamic";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { usePermissions } from "@/components/permission-provider";
import { useAuth } from "@/components/auth/auth-provider";
import { StaffOrders } from "@/features/orders/staff-orders";
import { RefundPanel } from "@/features/billing/refund-panel";
import { IncidentPanel } from "@/features/kitchen/incident-panel";
import { LiveQueryScope } from "@/lib/realtime/live-query";
import { Button } from "@/components/ui/button";
const ServiceView = dynamic(() => import("@/features/orders/service-view"), {
    loading: () => <p role="status">Đang tải màn phục vụ…</p>,
});
function OrderRoute() {
    const params = useSearchParams();
    const { can } = usePermissions();
    const { user } = useAuth();
    const view = params.get("view");
    const orderId = params.get("orderId") ?? undefined;
    const refunds =
        view === "refunds" &&
        ["request", "approve", "pay"].some((action) =>
            can("billing.refunds." + action),
        );
    const incidents =
        view === "incidents" && can("fulfillment.incidents.report");
    if (user && (refunds || incidents)) {
        return (
            <LiveQueryScope key={user.id}>
                <div className="grid gap-5">
                    <Link
                        className="justify-self-start text-sm underline"
                        href="/orders"
                    >
                        Về đơn hàng
                    </Link>
                    {refunds ? (
                        <RefundPanel key={orderId} orderId={orderId} />
                    ) : (
                        <OrderIncidents key={orderId} orderId={orderId} />
                    )}
                </div>
            </LiveQueryScope>
        );
    }
    return can("sales.orders.manage") &&
        (params.has("orderId") || params.get("view") === "service") ? (
        <ServiceView />
    ) : (
        <StaffOrders />
    );
}
function OrderIncidents({ orderId }: { orderId?: string }) {
    const [input, setInput] = useState(orderId ?? "");
    const [lookup, setLookup] = useState(
        orderId && /^[1-9][0-9]{0,18}$/.test(orderId) ? orderId : undefined,
    );
    return (
        <>
            <form
                className="flex flex-wrap items-end gap-2"
                onSubmit={(event) => {
                    event.preventDefault();
                    setLookup(input || undefined);
                }}
            >
                <label className="grid gap-1">
                    Mã đơn hàng
                    <input
                        className="min-h-11 rounded-md border bg-background px-3 py-2"
                        inputMode="numeric"
                        pattern="[1-9][0-9]*"
                        maxLength={19}
                        value={input}
                        onChange={(event) => setInput(event.target.value)}
                    />
                </label>
                <Button className="min-h-11" type="submit">
                    {input ? "Tìm phiếu theo đơn" : "Hiện phiếu đang mở"}
                </Button>
            </form>
            <IncidentPanel key={lookup} orderId={lookup} />
        </>
    );
}
export default function OrdersPage() {
    return (
        <Suspense
            fallback={
                <div className="p-8 text-center text-sm text-gray-500">
                    Đang tải đơn hàng...
                </div>
            }
        >
            <OrderRoute />
        </Suspense>
    );
}
