import { ModulePlaceholder } from "@/components/module-placeholder";

export default function OrdersPage() {
  return <ModulePlaceholder permissions={["sales.orders.manage"]} title="Đơn hàng" />;
}
