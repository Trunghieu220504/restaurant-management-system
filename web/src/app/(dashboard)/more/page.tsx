"use client";

import Link from "next/link";
import {
    BarChart2,
    Users,
    UserCheck,
    Tag,
    Settings,
    UserCircle,
    CalendarDays,
    Store,
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
import { useRestaurant } from "@/lib/store/restaurant-context";

export default function MorePage() {
    const { user, logout } = useAuth();
    const { can } = usePermissions();
    const { salesDay } = useRestaurant();
    const displayName = user?.userName || "Tài khoản";

    const menuSections = [
        {
            title: "Báo cáo & Vận hành",
            items: [
                {
                    label: "Báo cáo kinh doanh (KD)",
                    desc: "Biểu đồ cột doanh thu ròng, tổng thu ngày, bảng chi tiết",
                    href: "/reports",
                    icon: BarChart2,
                    color: "bg-rose-50 text-rose-600",
                    badge: "Ảnh 5",
                },
                {
                    label: "Ngày bán & Chốt ca",
                    desc: "Tiền két đầu ngày (UC25), đối soát doanh thu & chốt ca (UC26)",
                    href: "/sales-day",
                    icon: CalendarDays,
                    color: "bg-emerald-50 text-emerald-600",
                    badge:
                        salesDay.status === "DANG_BAN" ? "Đang bán" : "Chưa mở",
                },
                {
                    label: "Màn hình Bếp & Bar KDS",
                    desc: "Màn hình chế biến món theo trạm (UC11, UC12) & chuông báo",
                    href: "/kitchen",
                    icon: ChefHat,
                    color: "bg-orange-50 text-orange-600",
                    badge: "KDS",
                },
            ],
        },
        {
            title: "Quản trị phân hệ (Theo ảnh thiết kế)",
            items: [
                {
                    label: "Quản lý Nhân viên & Bộ phận",
                    desc: "Phân quyền vai trò phục vụ, bếp, bar, quản lý",
                    href: "/staff",
                    icon: Users,
                    color: "bg-amber-50 text-amber-600",
                    badge: "Ảnh 7",
                },
                {
                    label: "Quản lý Khách hàng",
                    desc: "Lịch sử dùng món, chi tiêu và xếp hạng thành viên",
                    href: "/customers",
                    icon: UserCheck,
                    color: "bg-teal-50 text-teal-600",
                    badge: "Ảnh 8",
                },
                {
                    label: "Chương trình Khuyến mại",
                    desc: "Tạo và kiểm tra điều kiện áp dụng giảm giá",
                    href: "/promotions",
                    icon: Tag,
                    color: "bg-indigo-50 text-indigo-600",
                    badge: "Ảnh 6",
                },
            ],
        },
        {
            title: "Hệ thống & Cửa hàng",
            items: [
                {
                    label: "Danh sách cửa hàng & Doanh thu",
                    desc: "Quản lý chuỗi chi nhánh và chuyển đổi cửa hàng",
                    href: "/stores",
                    icon: Store,
                    color: "bg-red-50 text-red-600",
                    badge: "Ảnh 2",
                },
                {
                    label: "Cài đặt quán",
                    desc: "Loại hình quán, quy trình order, âm thanh thông báo",
                    href: "/settings",
                    icon: Settings,
                    color: "bg-blue-50 text-blue-600",
                    badge: "Ảnh 4",
                },
                {
                    label: "Hồ sơ tài khoản",
                    desc: "Thông tin cá nhân, đổi mật khẩu, giữ sáng màn hình",
                    href: "/profile",
                    icon: UserCircle,
                    color: "bg-gray-50 text-gray-700",
                    badge: "Ảnh 3",
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
        <div className="min-h-screen bg-gray-50 flex flex-col space-y-4 pb-24">
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
            <div className="space-y-4">
                {menuSections.map((sec, idx) => (
                    <div
                        key={idx}
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
                                                <div className="flex items-center gap-2">
                                                    <span className="font-bold text-gray-900 text-sm group-hover:text-red-600 transition-colors">
                                                        {item.label}
                                                    </span>
                                                    {item.badge && (
                                                        <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-gray-100 text-gray-600">
                                                            {item.badge}
                                                        </span>
                                                    )}
                                                </div>
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
                    className="w-full py-3 bg-white hover:bg-red-50 text-red-600 font-bold text-sm rounded-xl border border-red-200 shadow-sm flex items-center justify-center gap-2 transition-colors"
                >
                    <LogOut size={16} />
                    <span>Đăng xuất tài khoản</span>
                </button>
            </div>
        </div>
    );
}
