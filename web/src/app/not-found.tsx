import Link from "next/link";
import { Button } from "@/components/ui/button";

export default function NotFound() {
  return (
    <main className="grid min-h-screen place-items-center p-6">
      <section className="max-w-md space-y-4 text-center">
        <p className="text-sm font-semibold text-muted-foreground">404</p>
        <h1 className="text-3xl font-semibold">Không tìm thấy trang</h1>
        <p className="text-muted-foreground">
          Đường dẫn có thể đã thay đổi hoặc không tồn tại.
        </p>
        <Button asChild>
          <Link href="/">Về trang tổng quan</Link>
        </Button>
      </section>
    </main>
  );
}
