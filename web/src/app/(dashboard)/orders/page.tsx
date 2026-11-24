"use client";
import { Suspense } from "react";
import dynamic from "next/dynamic";
import { useSearchParams } from "next/navigation";
import { usePermissions } from "@/components/permission-provider";
import { StaffOrders } from "@/features/orders/staff-orders";
const ServiceView = dynamic(() => import("@/features/orders/service-view"), {
    loading: () => <p role="status">Đang tải màn phục vụ…</p>,
});
function OrderRoute() {
    const params = useSearchParams();
    const { can } = usePermissions();
    return can("sales.orders.manage") &&
        (params.has("orderId") || params.get("view") === "service") ? (
        <ServiceView />
    ) : (
        <StaffOrders />
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
