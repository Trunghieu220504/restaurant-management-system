"use client";

import Link from "next/link";
import type { ColumnDef } from "@tanstack/react-table";
import {
    ArrowUpRight,
    CircleDollarSign,
    ClipboardList,
    Users,
    Boxes,
    Store,
    UserCircle,
    Settings,
    BarChart2,
    Tag,
    UserCheck,
    ReceiptText,
    Smartphone,
    LayoutGrid,
    ChefHat,
    CalendarDays,
} from "lucide-react";
import { RestaurantForm } from "@/components/restaurant-form";
import { Button } from "@/components/ui/button";
import { DataTable } from "@/components/ui/data-table";
import {
    useRestaurant,
    type RestaurantOrder,
} from "@/lib/store/restaurant-context";

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
        title: "1. QL Món ăn",
        href: "/menu",
        desc: "Lưới món ăn, cờ 'Đã hết', toggle bật/tắt bán, tabs khai vị / món chính",
        icon: Boxes,
        badge: "Ảnh 1",
        color: "bg-orange-50 text-orange-600 border-orange-200",
    },
    {
        title: "2. Danh sách cửa hàng",
        href: "/stores",
        desc: "Doanh thu chuỗi 188.320đ, chi nhánh Seoul / Sumibozu / Cơm Thố",
        icon: Store,
        badge: "Ảnh 2",
        color: "bg-red-50 text-red-600 border-red-200",
    },
    {
        title: "3. Hồ sơ tài khoản",
        href: "/profile",
        desc: "nguyen the vinh, số điện thoại, mật khẩu, đổi ngôn ngữ, đăng xuất",
        icon: UserCircle,
        badge: "Ảnh 3",
        color: "bg-gray-50 text-gray-700 border-gray-200",
    },
    {
        title: "4. Cài đặt quán",
        href: "/settings",
        desc: "Loại hình quán, tự động hoàn thành, thanh toán trước, nhắc nhở 15s",
        icon: Settings,
        badge: "Ảnh 4",
        color: "bg-blue-50 text-blue-600 border-blue-200",
    },
    {
        title: "5. Báo cáo KD",
        href: "/reports",
        desc: "Biểu đồ cột đỏ 188.320đ ngày 19/02, tổng thu, bảng chi tiết ngày",
        icon: BarChart2,
        badge: "Ảnh 5",
        color: "bg-rose-50 text-rose-600 border-rose-200",
    },
    {
        title: "6. QL Khuyến mại",
        href: "/promotions",
        desc: "Tìm kiếm CTKM, lọc theo loại & trạng thái, card giảm giá sản phẩm",
        icon: Tag,
        badge: "Ảnh 6",
        color: "bg-indigo-50 text-indigo-600 border-indigo-200",
    },
    {
        title: "7. QL Nhân viên",
        href: "/staff",
        desc: "2 tab QL Nhân viên / QL Bộ phận, tìm kiếm, avatar, vai trò chủ quán",
        icon: Users,
        badge: "Ảnh 7",
        color: "bg-amber-50 text-amber-600 border-amber-200",
    },
    {
        title: "8. QL Khách hàng",
        href: "/customers",
        desc: "2 tab Khách hàng / Xếp hạng, thống kê ăn & chi tiêu, nút + Tạo đơn",
        icon: UserCheck,
        badge: "Ảnh 8",
        color: "bg-emerald-50 text-emerald-600 border-emerald-200",
    },
];

export default function DashboardPage() {
    const { orders, tables, salesDay } = useRestaurant();

    // Dynamic calculations
    const closedOrders = orders.filter((o) => o.status === "DA_DONG");
    const todayRevenue = closedOrders.reduce((sum, o) => {
        return sum + o.payments.reduce((s, p) => s + p.amount, 0);
    }, 0);
    const totalRevenueDisplay =
        (188320 + todayRevenue).toLocaleString("vi-VN") + " ₫";

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
            label: "Doanh thu toàn chuỗi",
            value: totalRevenueDisplay,
            icon: CircleDollarSign,
            change: "Bao gồm 188.320đ baseline + đơn hôm nay",
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
                    <p className="text-sm text-muted-foreground">
                        Báo cáo đề tài: Clean Architecture kết hợp Modular
                        Monolith & Giao diện di động đồng bộ.
                    </p>
                </div>
                <div className="flex items-center gap-2">
                    <Button variant="outline" asChild>
                        <Link href="/sales-day">
                            <CalendarDays
                                aria-hidden="true"
                                className="size-4"
                            />
                            Chốt ca / Ngày bán (UC26)
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

            {/* Primary Operational Flow Quick Action Banner */}
            <section className="bg-gradient-to-r from-red-600 via-rose-600 to-orange-500 rounded-2xl p-5 text-white shadow-lg">
                <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                    <div className="space-y-1">
                        <span className="text-xs font-bold uppercase tracking-wider bg-white/20 px-2.5 py-1 rounded-full">
                            Quy trình nghiệp vụ cốt lõi (Core Workflow)
                        </span>
                        <h2 className="text-lg font-bold">
                            Luồng khép kín theo tài liệu đề tài
                        </h2>
                        <p className="text-xs text-white/90 max-w-2xl">
                            1. Mở ngày bán (UC25) → 2. Mở phiên bàn (UC03) → 3.
                            Gọi món (UC06) → 4. Bếp/Bar KDS chế biến (UC11,
                            UC12) → 5. Bưng bàn (UC15) → 6. Thu tiền & Đóng đơn
                            (UC17, UC20) → 7. Báo cáo & Chốt ngày (UC26, UC28)
                        </p>
                    </div>
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

            {/* 8 Module Shortcut Cards matching the 8 user images */}
            <section className="space-y-4">
                <div className="flex items-center justify-between">
                    <div>
                        <h2 className="text-lg font-bold text-gray-900">
                            Các phân hệ đã xây dựng theo hình ảnh
                        </h2>
                        <p className="text-sm text-muted-foreground">
                            Bấm vào từng màn hình để xem và tương tác trực tiếp
                            với giao diện mẫu.
                        </p>
                    </div>
                    <div className="flex items-center gap-1.5 text-xs text-gray-500 bg-gray-100 px-3 py-1.5 rounded-lg">
                        <Smartphone size={14} />
                        <span>Tối ưu hiển thị responsive</span>
                    </div>
                </div>

                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                    {moduleShortcuts.map((item) => {
                        const Icon = item.icon;
                        return (
                            <Link
                                key={item.href}
                                href={item.href}
                                className="group rounded-xl border bg-card p-4 shadow-sm hover:shadow-md transition-all hover:border-red-300 flex flex-col justify-between"
                            >
                                <div>
                                    <div className="flex items-center justify-between mb-2">
                                        <span
                                            className={`text-[11px] font-semibold px-2 py-0.5 rounded-full border ${item.color}`}
                                        >
                                            {item.badge}
                                        </span>
                                        <Icon className="size-5 text-gray-400 group-hover:text-red-500 transition-colors" />
                                    </div>
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

            {/* Validation Form example */}
            <section className="max-w-2xl space-y-4 rounded-xl border bg-card p-5 sm:p-6 shadow-sm">
                <div>
                    <h2 className="text-lg font-bold text-gray-900">
                        Cấu hình thông tin nhà hàng
                    </h2>
                    <p className="text-sm text-muted-foreground">
                        Biểu mẫu xác thực React Hook Form + Zod.
                    </p>
                </div>
                <RestaurantForm />
            </section>
        </div>
    );
}
