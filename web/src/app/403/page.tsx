import Link from "next/link";
import { Button } from "@/components/ui/button";

export default function ForbiddenPage() {
  return (
    <main className="grid min-h-screen place-items-center p-6">
      <section className="max-w-md space-y-4 text-center">
        <p className="text-sm font-semibold text-muted-foreground">403</p>
        <h1 className="text-3xl font-semibold">Bạn không có quyền truy cập</h1>
        <p className="text-muted-foreground">
          Hãy liên hệ quản trị viên nếu bạn cần quyền sử dụng chức năng này.
        </p>
        <Button asChild>
          <Link href="/">Về trang tổng quan</Link>
        </Button>
      </section>
    </main>
  );
}
