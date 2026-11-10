"use client";

import { useEffect } from "react";
import { Button } from "@/components/ui/button";

export default function DashboardError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("Dashboard route error:", error);
  }, [error]);

  return (
    <main className="mx-auto grid min-h-[50vh] max-w-lg content-center justify-items-start gap-4">
      <p className="text-sm font-medium text-red-600">Đã xảy ra lỗi</p>
      <h1 className="text-2xl font-semibold">Không thể tải nội dung</h1>
      <p className="text-muted-foreground">
        Vui lòng thử lại. Nếu lỗi tiếp tục, hãy liên hệ quản trị viên.
      </p>
      <Button onClick={reset}>Thử lại</Button>
    </main>
  );
}
