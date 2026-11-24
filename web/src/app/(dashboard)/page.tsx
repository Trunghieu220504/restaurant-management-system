"use client";
import Link from "next/link";
import {
    Boxes,
    Users,
    UserCircle,
    BarChart2,
    ArrowUpRight,
    LayoutGrid,
    ReceiptText,
    ChefHat,
    CalendarDays,
} from "lucide-react";
import { usePermissions } from "@/components/permission-provider";
import { canAccess } from "@/lib/auth/route-permissions";
const moduleShortcuts = [
    {
        title: "Bàn",
        href: "/tables",
        desc: "Sơ đồ và phiên bàn",
        icon: LayoutGrid,
        color: "bg-emerald-50 text-emerald-800 border-emerald-200",
    },
    {
        title: "Đơn hàng",
        href: "/orders",
        desc: "Gọi món và theo dõi các đợt",
        icon: ReceiptText,
        color: "bg-emerald-50 text-emerald-800 border-emerald-200",
    },
    {
        title: "Bếp & Bar",
        href: "/kitchen",
        desc: "Theo dõi phiếu chế biến",
        icon: ChefHat,
        color: "bg-emerald-50 text-emerald-800 border-emerald-200",
    },
    {
        title: "Ngày bán",
        href: "/sales-day",
        desc: "Mở và chốt ngày bán",
        icon: CalendarDays,
        color: "bg-emerald-50 text-emerald-800 border-emerald-200",
    },
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
    const { can } = usePermissions();
    const visibleShortcuts = moduleShortcuts.filter(({ href }) =>
        canAccess(href, can),
    );

    return (
        <div className="space-y-5">
            <h1 className="text-2xl font-bold">Hệ thống Quản lý Nhà hàng</h1>
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
        </div>
    );
}
