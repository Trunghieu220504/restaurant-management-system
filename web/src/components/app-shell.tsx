"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
    Boxes,
    LayoutDashboard,
    Menu,
    ReceiptText,
    Settings,
    Users,
    Store,
    BarChart2,
    Tag,
    UserCheck,
    UserCircle,
    LogOut,
    LayoutGrid,
    ChefHat,
    CalendarDays,
    Package,
    MoreHorizontal,
    QrCode,
    type LucideIcon,
} from "lucide-react";
import { useState, type ReactNode } from "react";
import { usePermissions } from "@/components/permission-provider";
import { useAuth } from "@/components/auth/auth-provider";
import { useRestaurant } from "@/lib/store/restaurant-context";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

// 5 TABS CHÍNH THEO ĐẶC TẢ SƠ ĐỒ ĐIỀU HƯỚNG
export const primaryTabs = [
    { label: "Bàn", href: "/tables", icon: LayoutGrid, badge: "UC03" },
    { label: "Đơn hàng", href: "/orders", icon: ReceiptText, badge: "UC07" },
    { label: "Thực đơn", href: "/menu", icon: Boxes, badge: "UC05" },
    { label: "Kho", href: "/inventory", icon: Package, badge: "UC22" },
    { label: "Thêm", href: "/more", icon: MoreHorizontal, badge: "Quản trị" },
];

export function AppShell({ children }: { children: ReactNode }) {
    const pathname = usePathname();
    const { can } = usePermissions();
    const { user, logout } = useAuth();
    const { salesDay } = useRestaurant();
    const [sidebarOpen, setSidebarOpen] = useState(false);

    const displayName = user?.userName || "nguyen the vinh";
    const userInitial = displayName.charAt(0).toUpperCase();

    return (
        <div className="min-h-screen bg-muted/50 pb-16 lg:pb-0">
            {sidebarOpen && (
                <button
                    aria-label="Đóng menu"
                    className="fixed inset-0 z-30 bg-black/40 lg:hidden"
                    onClick={() => setSidebarOpen(false)}
                />
            )}
            <aside
                className={cn(
                    "fixed inset-y-0 left-0 z-40 flex w-64 flex-col border-r bg-card transition-transform lg:translate-x-0",
                    sidebarOpen ? "translate-x-0" : "-translate-x-full",
                )}
            >
                <div className="flex h-16 items-center justify-between border-b px-4 font-semibold">
                    <Link href="/" className="flex items-center gap-2.5">
                        <span className="grid size-8 place-items-center rounded-lg bg-red-500 text-white font-bold">
                            R
                        </span>
                        <div>
                            <span className="text-sm font-bold block leading-none">
                                Restaurant OS
                            </span>
                            <span className="text-[10px] text-muted-foreground">
                                Seoul Kitchen Quán
                            </span>
                        </div>
                    </Link>
                    <Link
                        href="/stores"
                        title="Đổi cửa hàng"
                        className="p-1.5 text-xs text-muted-foreground hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors"
                    >
                        <Store size={16} />
                    </Link>
                </div>

                <nav
                    aria-label="Điều hướng chính"
                    className="flex-1 space-y-4 p-3 overflow-y-auto"
                >
                    {/* Section 1: 5 Tab chính */}
                    <div className="space-y-1">
                        <p className="px-3 text-[10px] font-bold text-gray-400 tracking-wider">
                            5 TAB VẬN HÀNH CHÍNH
                        </p>
                        {primaryTabs.map(
                            ({ label, href, icon: Icon, badge }) => {
                                const active =
                                    pathname === href ||
                                    (href !== "/tables" &&
                                        pathname.startsWith(href));
                                return (
                                    <Link
                                        key={href}
                                        href={href}
                                        onClick={() => setSidebarOpen(false)}
                                        className={cn(
                                            "flex items-center justify-between rounded-xl px-3 py-2.5 text-sm font-semibold transition-all",
                                            active
                                                ? "bg-red-50 text-red-600 shadow-xs"
                                                : "text-muted-foreground hover:bg-muted hover:text-foreground",
                                        )}
                                    >
                                        <div className="flex items-center gap-3">
                                            <Icon
                                                className={cn(
                                                    "size-4",
                                                    active && "text-red-600",
                                                )}
                                            />
                                            <span>{label}</span>
                                        </div>
                                        {badge && (
                                            <span
                                                className={cn(
                                                    "text-[10px] px-2 py-0.5 rounded-full font-bold",
                                                    active
                                                        ? "bg-red-200 text-red-800"
                                                        : "bg-gray-100 text-gray-600",
                                                )}
                                            >
                                                {badge}
                                            </span>
                                        )}
                                    </Link>
                                );
                            },
                        )}
                    </div>

                    {/* Section 2: Mở rộng / Chức năng thêm */}
                    <div className="space-y-1 pt-2 border-t border-gray-100">
                        <p className="px-3 text-[10px] font-bold text-gray-400 tracking-wider">
                            BẾP & ĐIỀU PHỐI (KDS)
                        </p>
                        <Link
                            href="/kitchen"
                            onClick={() => setSidebarOpen(false)}
                            className={cn(
                                "flex items-center justify-between rounded-xl px-3 py-2 text-sm font-medium transition-colors",
                                pathname === "/kitchen"
                                    ? "bg-orange-50 text-orange-600 font-semibold"
                                    : "text-muted-foreground hover:bg-muted hover:text-foreground",
                            )}
                        >
                            <div className="flex items-center gap-3">
                                <ChefHat className="size-4 text-orange-500" />
                                <span>Bếp & Bar (KDS)</span>
                            </div>
                            <span className="text-[10px] bg-orange-100 text-orange-700 px-1.5 py-0.5 rounded-full font-bold">
                                Chuông
                            </span>
                        </Link>
                        <Link
                            href="/guest?table=T04"
                            onClick={() => setSidebarOpen(false)}
                            className="flex items-center justify-between rounded-xl px-3 py-2 text-sm font-medium text-muted-foreground hover:bg-rose-50 hover:text-rose-600 transition-colors"
                        >
                            <div className="flex items-center gap-3">
                                <QrCode className="size-4 text-rose-500" />
                                <span>Menu QR Khách (T04)</span>
                            </div>
                            <span className="text-[10px] bg-rose-100 text-rose-700 px-1.5 py-0.5 rounded-full font-bold">
                                Khách
                            </span>
                        </Link>
                    </div>
                </nav>

                <div className="border-t p-3 space-y-2">
                    <button
                        onClick={() => logout()}
                        className="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-semibold text-red-600 hover:bg-red-50 transition-colors"
                    >
                        <LogOut size={16} />
                        <span>Đăng xuất phiên làm việc</span>
                    </button>
                    <div className="text-[11px] text-muted-foreground px-3">
                        Restaurant OS • Clean Modular Monolith
                    </div>
                </div>
            </aside>

            <div className="lg:pl-64">
                {/* Top Header */}
                <header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b bg-card/95 px-4 backdrop-blur sm:px-6">
                    <div className="flex items-center gap-3">
                        <Button
                            aria-label="Mở menu"
                            className="lg:hidden"
                            size="icon"
                            variant="ghost"
                            onClick={() => setSidebarOpen(true)}
                        >
                            <Menu aria-hidden="true" className="size-5" />
                        </Button>

                        {/* Current branch selector */}
                        <Link
                            href="/stores"
                            className="text-xs font-semibold text-gray-800 bg-gray-100 hover:bg-gray-200 px-2.5 py-1.5 rounded-lg flex items-center gap-1.5 transition-colors"
                        >
                            <Store size={14} className="text-red-500" />
                            <span className="truncate max-w-[120px] sm:max-w-none">
                                Seoul Kitchen Quán
                            </span>
                        </Link>

                        {/* SalesDay Status pill */}
                        <Link
                            href="/sales-day"
                            className={cn(
                                "hidden sm:flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full border transition-colors",
                                salesDay.status === "DANG_BAN"
                                    ? "bg-emerald-50 text-emerald-700 border-emerald-200 hover:bg-emerald-100"
                                    : "bg-amber-50 text-amber-700 border-amber-200 hover:bg-amber-100",
                            )}
                        >
                            <span
                                className={cn(
                                    "size-2 rounded-full",
                                    salesDay.status === "DANG_BAN"
                                        ? "bg-emerald-500 animate-pulse"
                                        : "bg-amber-500",
                                )}
                            />
                            <span>
                                {salesDay.status === "DANG_BAN"
                                    ? `Đang bán (${salesDay.businessDate})`
                                    : "Chưa mở ngày bán"}
                            </span>
                        </Link>
                    </div>

                    <div className="flex items-center gap-3">
                        {/* Quick Guest QR link */}
                        <Link
                            href="/guest?table=T04"
                            className="hidden md:flex items-center gap-1.5 text-xs bg-rose-50 text-rose-700 hover:bg-rose-100 px-2.5 py-1.5 rounded-lg font-bold border border-rose-200 transition-colors"
                        >
                            <QrCode size={14} />
                            <span>Giao diện Khách QR</span>
                        </Link>

                        <Link
                            href="/profile"
                            className="flex items-center gap-2.5 text-sm hover:opacity-80 transition-opacity"
                        >
                            <span className="hidden text-muted-foreground sm:inline text-xs">
                                Xin chào,
                            </span>
                            <span className="font-semibold text-gray-900 text-xs sm:text-sm">
                                {displayName}
                            </span>
                            <span className="grid size-8 place-items-center rounded-full bg-red-100 text-red-700 font-bold text-xs">
                                {userInitial}
                            </span>
                        </Link>
                    </div>
                </header>

                <main className="mx-auto w-full max-w-7xl p-3 sm:p-5 lg:p-6">
                    {children}
                </main>
            </div>

            {/* ── MOBILE FIXED BOTTOM NAVIGATION (THANH 5 TAB CHÍNH) ────────── */}
            <nav
                aria-label="Thanh điều hướng đáy màn hình di động"
                className="fixed bottom-0 left-0 right-0 z-40 bg-white/95 backdrop-blur border-t border-gray-200 flex items-center justify-around py-1.5 px-1 lg:hidden shadow-lg"
            >
                {primaryTabs.map(({ label, href, icon: Icon }) => {
                    const active =
                        pathname === href ||
                        (href !== "/tables" && pathname.startsWith(href));
                    return (
                        <Link
                            key={href}
                            href={href}
                            className={cn(
                                "flex flex-col items-center justify-center flex-1 py-1 rounded-xl transition-all",
                                active
                                    ? "text-red-600 font-bold"
                                    : "text-gray-500 hover:text-gray-800 font-medium",
                            )}
                        >
                            <div className="relative">
                                <Icon
                                    size={20}
                                    className={cn(
                                        "transition-transform",
                                        active && "scale-110",
                                    )}
                                />
                                {active && (
                                    <span className="absolute -bottom-1 left-1/2 -translate-x-1/2 size-1 rounded-full bg-red-600" />
                                )}
                            </div>
                            <span className="text-[10px] mt-0.5 tracking-tight">
                                {label}
                            </span>
                        </Link>
                    );
                })}
            </nav>
        </div>
    );
}
