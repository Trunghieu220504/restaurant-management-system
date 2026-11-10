"use client";

import Link from "next/link";
import { usePermissions } from "@/components/permission-provider";
import { Button } from "@/components/ui/button";

export function ModulePlaceholder({
  title,
  permissions,
}: {
  title: string;
  permissions: string[];
}) {
  const { can } = usePermissions();
  const allowed = permissions.some(can);

  if (!allowed) {
    return (
      <section className="mx-auto max-w-lg space-y-3 rounded-lg border bg-card p-8 text-center">
        <h1 className="text-xl font-semibold">403 · Không có quyền truy cập</h1>
        <p className="text-sm text-muted-foreground">
          Tài khoản hiện tại chưa được cấp quyền cần thiết.
        </p>
        <Button asChild variant="outline">
          <Link href="/">Về tổng quan</Link>
        </Button>
      </section>
    );
  }

  return (
    <section className="space-y-2">
      <h1 className="text-2xl font-semibold">{title}</h1>
      <p className="text-muted-foreground">
        Khu vực sẵn sàng để Duy triển khai tính năng {title.toLowerCase()}.
      </p>
    </section>
  );
}
