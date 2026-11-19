"use client";

import Link from "next/link";
import type { ColumnDef } from "@tanstack/react-table";
import {
    ArrowUpRight,
    CircleDollarSign,
    ClipboardList,
    Users,
    Boxes,
    UserCircle,
    BarChart2,
    ReceiptText,
    LayoutGrid,
    ChefHat,
    CalendarDays,
} from "lucide-react";
import { usePermissions } from "@/components/permission-provider";
import { Button } from "@/components/ui/button";
import { DataTable } from "@/components/ui/data-table";
import { canAccess } from "@/lib/auth/route-permissions";
import { useRestaurant } from "@/lib/store/restaurant-context";

type OrderRow = {
    code: string;
    table: string;
    customer: string;
    total: string;
    status: string;
};

const columns: ColumnDef<OrderRow>[] = [
    { accessorKey: "code", header: "Mã đơn" },
    { accessorKey: "table", header: "Bàn / Khu vực" },
    { accessorKey: "customer", header: "Khách hàng" },
    { accessorKey: "total", header: "Tổng tiền" },
    { accessorKey: "status", header: "Trạng thái" },
];

const moduleShortcuts = [
    {
        title: "Thực đơn",
        href: "/menu",
        desc: "Quản lý món ăn và trạng thái còn bán",
        icon: Boxes,
        color: "bg-orange-50 text-orange-600 border-orange-200",
    },
    {
        title: "Hồ sơ tài khoản",
        href: "/profile",
        desc: "Xem thông tin tài khoản và đổi mật khẩu",
        icon: UserCircle,
        color: "bg-gray-50 text-gray-700 border-gray-200",
    },
    {
        title: "Báo cáo kinh doanh",
        href: "/reports",
        desc: "Xem doanh thu và tổng tiền thu trong ngày",
        icon: BarChart2,
        color: "bg-rose-50 text-rose-600 border-rose-200",
    },
    {
        title: "Tài khoản nhân viên",
        href: "/staff",
        desc: "Quản lý tài khoản và vai trò của nhân viên",
        icon: Users,
        color: "bg-amber-50 text-amber-600 border-amber-200",
    },
];

export default function DashboardPage() {
    const { orders, tables } = useRestaurant();
    const { can } = usePermissions();
    const visibleShortcuts = moduleShortcuts.filter(({ href }) =>
        canAccess(href, can),
    );

    // Dynamic calculations
    const closedOrders = orders.filter((o) => o.status === "DA_DONG");
    const todayRevenue = closedOrders.reduce((sum, o) => {
        return sum + o.payments.reduce((s, p) => s + p.amount, 0);
    }, 0);

    const occupiedTables = tables.filter(
        (t) => t.status === "DANG_DUNG",
    ).length;

    const orderRows: OrderRow[] = orders.map((o) => {
        const totalCost = o.items.reduce(
            (s, it) => s + it.price * it.quantity,
            0,
        );
        let statusLabel = "Chờ xác nhận";
        if (o.status === "DANG_PHUC_VU") statusLabel = "Đang phục vụ";
        if (o.status === "CHO_THANH_TOAN") statusLabel = "Chờ thanh toán";
        if (o.status === "DA_DONG") statusLabel = "Hoàn tất";
        if (o.status === "DA_HUY") statusLabel = "Đã hủy";

        return {
            code: o.id,
            table: o.tableName,
            customer: o.customerName,
            total: totalCost.toLocaleString("vi-VN") + " ₫",
            status: statusLabel,
        };
    });

    const dynamicStats = [
        {
            label: "Tiền thu trong ngày",
            value: `${todayRevenue.toLocaleString("vi-VN")} ₫`,
            icon: CircleDollarSign,
            change: "Từ các đơn đã hoàn tất",
        },
        {
            label: "Đơn hàng hôm nay",
            value: `${orders.length} đơn`,
            icon: ClipboardList,
            change: `${closedOrders.length} hoàn tất, ${orders.length - closedOrders.length} đang xử lý`,
        },
        {
            label: "Bàn đang phục vụ",
            value: `${occupiedTables} / ${tables.length} bàn`,
            icon: Users,
            change: `${tables.length - occupiedTables} bàn trống sẵn sàng đón khách`,
        },
    ];

    return (
        <div className="space-y-8 pb-12">
            {/* Header section */}
            <section className="flex flex-wrap items-end justify-between gap-4">
                <div className="space-y-1">
                    <p className="text-sm text-muted-foreground">
                        Thứ Tư, 11 tháng 11, 2026
                    </p>
                    <h1 className="text-2xl font-bold tracking-tight">
                        Hệ thống Quản lý Nhà hàng
                    </h1>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                    <Button variant="outline" asChild>
                        <Link href="/sales-day">
                            <CalendarDays
                                aria-hidden="true"
                                className="size-4"
                            />
                            Chốt ca / Ngày bán
                        </Link>
                    </Button>
                    <Button
                        className="bg-red-600 hover:bg-red-700 text-white"
                        asChild
                    >
                        <Link href="/tables">
                            <LayoutGrid aria-hidden="true" className="size-4" />
                            Sơ đồ Bàn & Đặt món
                        </Link>
                    </Button>
                </div>
            </section>

            {/* Operational shortcuts */}
            <section className="bg-gradient-to-r from-red-600 via-rose-600 to-orange-500 rounded-2xl p-5 text-white shadow-lg">
                <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                    <div className="flex flex-wrap items-center gap-2">
                        <Link
                            href="/tables"
                            className="bg-white text-red-600 text-xs font-bold px-3 py-2 rounded-xl shadow hover:bg-red-50 transition-colors flex items-center gap-1.5"
                        >
                            <LayoutGrid size={15} /> Sơ đồ bàn
                        </Link>
                        <Link
                            href="/orders"
                            className="bg-white text-red-600 text-xs font-bold px-3 py-2 rounded-xl shadow hover:bg-red-50 transition-colors flex items-center gap-1.5"
                        >
                            <ReceiptText size={15} /> Đơn hàng
                        </Link>
                        <Link
                            href="/kitchen"
                            className="bg-white text-red-600 text-xs font-bold px-3 py-2 rounded-xl shadow hover:bg-red-50 transition-colors flex items-center gap-1.5"
                        >
                            <ChefHat size={15} /> Bếp & Bar KDS
                        </Link>
                        <Link
                            href="/sales-day"
                            className="bg-white text-red-600 text-xs font-bold px-3 py-2 rounded-xl shadow hover:bg-red-50 transition-colors flex items-center gap-1.5"
                        >
                            <CalendarDays size={15} /> Ngày bán
                        </Link>
                    </div>
                </div>
            </section>

            {/* Stats summary */}
            <section
                aria-label="Chỉ số hôm nay"
                className="grid gap-4 sm:grid-cols-3"
            >
                {dynamicStats.map(({ label, value, icon: Icon, change }) => (
                    <article
                        key={label}
                        className="rounded-xl border bg-card p-5 shadow-sm"
                    >
                        <div className="flex items-center justify-between">
                            <p className="text-sm text-muted-foreground">
                                {label}
                            </p>
                            <Icon
                                aria-hidden="true"
                                className="size-4 text-muted-foreground"
                            />
                        </div>
                        <p className="mt-3 text-2xl font-bold text-gray-900">
                            {value}
                        </p>
                        <p className="mt-1 text-xs text-muted-foreground">
                            {change}
                        </p>
                    </article>
                ))}
            </section>

            <section className="space-y-4">
                <h2 className="text-lg font-bold text-gray-900">
                    Truy cập nhanh
                </h2>

                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                    {visibleShortcuts.map((item) => {
                        const Icon = item.icon;
                        return (
                            <Link
                                key={item.href}
                                href={item.href}
                                className="group rounded-xl border bg-card p-4 shadow-sm hover:shadow-md transition-all hover:border-red-300 flex flex-col justify-between"
                            >
                                <div>
                                    <span
                                        className={`inline-flex mb-2 rounded-lg border p-2 ${item.color}`}
                                    >
                                        <Icon
                                            className="size-5"
                                            aria-hidden="true"
                                        />
                                    </span>
                                    <h3 className="font-bold text-gray-900 group-hover:text-red-600 transition-colors text-sm">
                                        {item.title}
                                    </h3>
                                    <p className="mt-1 text-xs text-gray-500 line-clamp-2">
                                        {item.desc}
                                    </p>
                                </div>
                                <div className="mt-4 pt-2 border-t border-gray-100 flex items-center justify-between text-xs text-red-600 font-semibold">
                                    <span>Mở giao diện</span>
                                    <ArrowUpRight
                                        size={14}
                                        className="group-hover:translate-x-0.5 group-hover:-translate-y-0.5 transition-transform"
                                    />
                                </div>
                            </Link>
                        );
                    })}
                </div>
            </section>

            {/* Recent Orders table */}
            <section className="space-y-4">
                <div className="flex items-center justify-between">
                    <div>
                        <h2 className="text-lg font-bold text-gray-900">
                            Đơn hàng trong ngày
                        </h2>
                        <p className="text-sm text-muted-foreground">
                            Dữ liệu thời gian thực từ cửa hàng và đợt phục vụ
                            tại bàn.
                        </p>
                    </div>
                    <Link
                        href="/orders"
                        className="text-xs font-semibold text-red-600 hover:text-red-700 flex items-center gap-1"
                    >
                        Quản lý tất cả đơn hàng <ArrowUpRight size={14} />
                    </Link>
                </div>
                <DataTable
                    columns={columns}
                    data={orderRows}
                    searchPlaceholder="Tìm mã đơn, tên bàn..."
                />
            </section>
        </div>
    );
}
