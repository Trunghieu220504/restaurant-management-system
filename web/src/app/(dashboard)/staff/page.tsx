import { ModulePlaceholder } from "@/components/module-placeholder";

export default function StaffPage() {
  return (
    <ModulePlaceholder
      permissions={["identity.accounts.manage", "identity.subordinates.manage"]}
      title="Nhân viên"
    />
  );
}
