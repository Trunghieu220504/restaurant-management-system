"use client";

import Link from "next/link";
import {
    BarChart2,
    Users,
    UserCircle,
    CalendarDays,
    LogOut,
    ChevronRight,
    ChefHat,
    QrCode,
} from "lucide-react";
import { useAuth } from "@/components/auth/auth-provider";
import { usePermissions } from "@/components/permission-provider";
import {
    canAccess,
    guestQrPreviewPermissions,
    hasAnyPermission,
} from "@/lib/auth/route-permissions";

export default function MorePage() {
    const { user, logout } = useAuth();
    const { can } = usePermissions();
    const displayName = user?.userName || "Tài khoản";

    const menuSections = [
        {
            title: "Báo cáo & Vận hành",
            items: [
                {
                    label: "Báo cáo kinh doanh (KD)",
                    desc: "Doanh thu và dòng tiền theo ngày đã chốt",
                    href: "/reports",
                    icon: BarChart2,
                    color: "bg-rose-50 text-rose-600",
                },
                {
                    label: "Ngày bán & Chốt ca",
                    desc: "Tiền két đầu ngày, đối soát doanh thu & chốt ca",
                    href: "/sales-day",
                    icon: CalendarDays,
                    color: "bg-emerald-50 text-emerald-600",
                },
                {
                    label: "Màn hình Bếp & Bar KDS",
                    desc: "Màn hình chế biến món theo trạm & chuông báo",
                    href: "/kitchen",
                    icon: ChefHat,
                    color: "bg-orange-50 text-orange-600",
                },
            ],
        },
        {
            title: "Tài khoản & Phân quyền",
            items: [
                {
                    label: "Quản lý tài khoản nhân viên",
                    desc: "Quản lý tài khoản và vai trò của nhân viên",
                    href: "/staff",
                    icon: Users,
                    color: "bg-amber-50 text-amber-600",
                },
                {
                    label: "Hồ sơ tài khoản",
                    desc: "Thông tin tài khoản và đổi mật khẩu",
                    href: "/profile",
                    icon: UserCircle,
                    color: "bg-gray-50 text-gray-700",
                },
            ],
        },
    ]
        .map((section) => ({
            ...section,
            items: section.items.filter((item) => canAccess(item.href, can)),
        }))
        .filter((section) => section.items.length > 0);

    return (
        <div className="space-y-4">
            {/* Header */}
            <div className="bg-white p-4 rounded-xl shadow-sm border border-gray-100 flex items-center justify-between">
                <div className="flex items-center gap-3">
                    <div className="size-11 rounded-full bg-red-100 text-red-700 font-bold flex items-center justify-center text-sm shadow-sm">
                        {displayName.charAt(0).toUpperCase()}
                    </div>
                    <div>
                        <h1 className="text-base font-bold text-gray-900">
                            {displayName}
                        </h1>
                        <p className="text-xs text-gray-500">
                            {user?.roles.join(", ") || "Chưa có vai trò"}
                        </p>
                    </div>
                </div>

                {hasAnyPermission(guestQrPreviewPermissions, can) && (
                    <Link
                        href="/guest?table=T04"
                        className="text-xs bg-red-50 text-red-600 font-bold px-3 py-1.5 rounded-lg border border-red-200 hover:bg-red-100 transition-colors flex items-center gap-1.5"
                    >
                        <QrCode size={14} />
                        <span>Thử QR Khách</span>
                    </Link>
                )}
            </div>

            {/* Navigation Sections */}
            <div className="grid items-start gap-4 lg:grid-cols-2">
                {menuSections.map((sec) => (
                    <div
                        key={sec.title}
                        className="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden"
                    >
                        <div className="px-4 py-2.5 bg-gray-50/70 border-b border-gray-100">
                            <h2 className="text-xs font-bold uppercase tracking-wider text-gray-500">
                                {sec.title}
                            </h2>
                        </div>
                        <div className="divide-y divide-gray-100 text-xs">
                            {sec.items.map((item) => {
                                const Icon = item.icon;
                                return (
                                    <Link
                                        key={item.href}
                                        href={item.href}
                                        className="p-3.5 flex items-center justify-between hover:bg-gray-50 transition-colors group"
                                    >
                                        <div className="flex items-center gap-3">
                                            <div
                                                className={`p-2 rounded-xl ${item.color} shrink-0`}
                                            >
                                                <Icon size={18} />
                                            </div>
                                            <div>
                                                <span className="font-bold text-gray-900 text-sm group-hover:text-red-600 transition-colors">
                                                    {item.label}
                                                </span>
                                                <p className="text-gray-500 text-xs mt-0.5 line-clamp-1">
                                                    {item.desc}
                                                </p>
                                            </div>
                                        </div>
                                        <ChevronRight
                                            size={18}
                                            className="text-gray-300 group-hover:text-red-500 transition-colors shrink-0"
                                        />
                                    </Link>
                                );
                            })}
                        </div>
                    </div>
                ))}
            </div>

            {/* Logout button */}
            <div className="pt-2">
                <button
                    onClick={() => logout()}
                    className="w-full py-3 md:w-auto md:px-5 bg-white hover:bg-red-50 text-red-600 font-bold text-sm rounded-xl border border-red-200 shadow-sm flex items-center justify-center gap-2 transition-colors"
                >
                    <LogOut size={16} />
                    <span>Đăng xuất tài khoản</span>
                </button>
            </div>
        </div>
    );
}
