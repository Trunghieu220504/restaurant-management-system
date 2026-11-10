"use client";

import type { ColumnDef } from "@tanstack/react-table";
import { ArrowUpRight, CircleDollarSign, ClipboardList, Users } from "lucide-react";
import { RestaurantForm } from "@/components/restaurant-form";
import { Button } from "@/components/ui/button";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { DataTable } from "@/components/ui/data-table";
import { toast } from "sonner";

type Order = {
  code: string;
  table: string;
  total: string;
  status: string;
};

const orders: Order[] = [
  { code: "DH-1024", table: "Bàn 04", total: "420.000 ₫", status: "Đang phục vụ" },
  { code: "DH-1023", table: "Bàn 08", total: "185.000 ₫", status: "Chờ xác nhận" },
  { code: "DH-1022", table: "Mang đi", total: "96.000 ₫", status: "Hoàn tất" },
];

const columns: ColumnDef<Order>[] = [
  { accessorKey: "code", header: "Mã đơn" },
  { accessorKey: "table", header: "Khu vực" },
  { accessorKey: "total", header: "Tổng tiền" },
  { accessorKey: "status", header: "Trạng thái" },
];

const stats = [
  { label: "Đơn hàng hôm nay", value: "128", icon: ClipboardList, change: "+12,5%" },
  { label: "Doanh thu hôm nay", value: "18,4 triệu", icon: CircleDollarSign, change: "+8,2%" },
  { label: "Nhân viên đang làm", value: "12", icon: Users, change: "Ca sáng" },
];

export default function DashboardPage() {
  return (
    <div className="space-y-8">
      <section className="flex flex-wrap items-end justify-between gap-4">
        <div className="space-y-1">
          <p className="text-sm text-muted-foreground">Thứ Hai, 10 tháng 11, 2026</p>
          <h1 className="text-2xl font-semibold tracking-tight">Tổng quan</h1>
          <p className="text-sm text-muted-foreground">
            Theo dõi hoạt động nhà hàng trong ngày.
          </p>
        </div>
        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button>
              <ArrowUpRight aria-hidden="true" className="size-4" />
              Kết thúc ca
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Kết thúc ca làm việc?</AlertDialogTitle>
              <AlertDialogDescription>
                Hãy xác nhận sau khi đã đối soát đơn hàng và doanh thu trong ca.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Hủy</AlertDialogCancel>
              <AlertDialogAction
                onClick={() => toast.info("Hành động mẫu; chưa kết nối API kết thúc ca.")}
              >
                Xác nhận
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </section>

      <section aria-label="Chỉ số hôm nay" className="grid gap-4 md:grid-cols-3">
        {stats.map(({ label, value, icon: Icon, change }) => (
          <article key={label} className="rounded-lg border bg-card p-5">
            <div className="flex items-center justify-between">
              <p className="text-sm text-muted-foreground">{label}</p>
              <Icon aria-hidden="true" className="size-4 text-muted-foreground" />
            </div>
            <p className="mt-4 text-2xl font-semibold">{value}</p>
            <p className="mt-1 text-xs text-muted-foreground">{change}</p>
          </article>
        ))}
      </section>

      <section className="space-y-4">
        <div>
          <h2 className="text-lg font-semibold">Đơn hàng gần đây</h2>
          <p className="text-sm text-muted-foreground">
            Dữ liệu minh họa để thay bằng API của module.
          </p>
        </div>
        <DataTable columns={columns} data={orders} searchPlaceholder="Tìm mã đơn..." />
      </section>

      <section className="max-w-2xl space-y-4 rounded-lg border bg-card p-5 sm:p-6">
        <div>
          <h2 className="text-lg font-semibold">Ví dụ biểu mẫu có xác thực</h2>
          <p className="text-sm text-muted-foreground">
            React Hook Form + Zod; thay thao tác mẫu bằng API khi triển khai.
          </p>
        </div>
        <RestaurantForm />
      </section>
    </div>
  );
}
