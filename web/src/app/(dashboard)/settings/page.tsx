import { ModulePlaceholder } from "@/components/module-placeholder";

export default function SettingsPage() {
  return <ModulePlaceholder permissions={["identity.roles.manage"]} title="Phân quyền" />;
}
