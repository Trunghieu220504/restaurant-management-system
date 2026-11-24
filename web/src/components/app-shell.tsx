"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
    Boxes,
    Menu,
    ReceiptText,
    LogOut,
    LayoutGrid,
    ChefHat,
    Package,
    MoreHorizontal,
    QrCode,
    type LucideIcon,
} from "lucide-react";
import { useEffect, useState, type ReactNode } from "react";
import { usePermissions } from "@/components/permission-provider";
import { useAuth } from "@/components/auth/auth-provider";
import { Forbidden } from "@/components/forbidden";
import {
    canAccess,
    guestQrPreviewPermissions,
    hasAnyPermission,
} from "@/lib/auth/route-permissions";
import { authFetch } from "@/lib/auth/auth-client";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type NavTab = {
    label: string;
    href: string;
    icon: LucideIcon;
    badge?: string;
};

// 5 TABS CHÍNH THEO ĐẶC TẢ SƠ ĐỒ ĐIỀU HƯỚNG
// Tab nào hiện là do quyền của đường dẫn trong lib/auth/route-permissions.ts.
export const primaryTabs: readonly NavTab[] = [
    { label: "Bàn", href: "/tables", icon: LayoutGrid },
    { label: "Đơn hàng", href: "/orders", icon: ReceiptText },
    { label: "Thực đơn", href: "/menu", icon: Boxes },
    { label: "Kho", href: "/inventory", icon: Package },
    { label: "Thêm", href: "/more", icon: MoreHorizontal, badge: "Quản trị" },
];

export function AppShell({ children }: { children: ReactNode }) {
    const pathname = usePathname();
    const { can } = usePermissions();
    const { user, logout } = useAuth();

    const [sidebarOpen, setSidebarOpen] = useState(false);
    const [liveSalesDay, setLiveSalesDay] = useState<{
        status: "DANG_BAN" | "CHO_CHOT" | "DA_CHOT";
        businessDate: string;
    } | null>(null);

    useEffect(() => {
        let active = true;
        authFetch("/api/salesday/current", {
            credentials: "include",
            cache: "no-store",
        })
            .then(async (response) => {
                if (!response.ok) return null;
                return (await response.json()) as {
                    status?: string;
                    businessDate?: string;
                } | null;
            })
            .then((day) => {
                if (
                    active &&
                    day &&
                    typeof day.businessDate === "string" &&
                    (day.status === "DANG_BAN" ||
                        day.status === "CHO_CHOT" ||
                        day.status === "DA_CHOT")
                ) {
                    setLiveSalesDay({
                        status: day.status,
                        businessDate: day.businessDate,
                    });
                }
            })
            .catch(() => {});
        return () => {
            active = false;
        };
    }, []);

    const headerSalesDay = liveSalesDay;

    const visibleTabs = primaryTabs.filter((tab) => canAccess(tab.href, can));
    const showKitchen = canAccess("/kitchen", can);
    const showGuestQr = hasAnyPermission(guestQrPreviewPermissions, can);

    const displayName = user?.userName || "Tài khoản";
    const userInitial = displayName.charAt(0).toUpperCase();

    return (
        <div className="min-h-screen bg-muted/50 pb-[calc(4.5rem+env(safe-area-inset-bottom))] md:grid md:grid-cols-[12rem_minmax(0,1fr)] md:items-start md:pb-0 xl:grid-cols-[16rem_minmax(0,1fr)]">
            {sidebarOpen && (
                <button
                    aria-label="Đóng menu"
                    className="fixed inset-0 z-30 bg-black/40 md:hidden"
                    onClick={() => setSidebarOpen(false)}
                />
            )}
            <aside
                className={cn(
                    "fixed inset-y-0 left-0 z-50 flex w-64 max-w-[85vw] flex-col border-r bg-card md:sticky md:top-0 md:visible md:h-dvh md:w-full md:max-w-none md:translate-x-0",
                    sidebarOpen
                        ? "visible translate-x-0"
                        : "invisible -translate-x-full",
                )}
            >
                <div className="flex h-16 items-center border-b px-4 font-semibold">
                    <Link href="/" className="flex items-center gap-2.5">
                        <span className="grid size-8 place-items-center rounded-lg bg-red-500 text-white font-bold">
                            R
                        </span>
                        <span className="text-sm font-bold">Restaurant OS</span>
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
                        {visibleTabs.map(
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
                    {(showKitchen || showGuestQr) && (
                        <div className="space-y-1 pt-2 border-t border-gray-100">
                            <p className="px-3 text-[10px] font-bold text-gray-400 tracking-wider">
                                BẾP & ĐIỀU PHỐI (KDS)
                            </p>
                            {showKitchen && (
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
                            )}
                            {showGuestQr && (
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
                            )}
                        </div>
                    )}
                </nav>

                <div className="border-t p-3 space-y-2">
                    <button
                        onClick={() => logout()}
                        className="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-semibold text-red-600 hover:bg-red-50 transition-colors"
                    >
                        <LogOut size={16} />
                        <span>Đăng xuất phiên làm việc</span>
                    </button>
                </div>
            </aside>

            <div className="min-w-0">
                {/* Top Header */}
                <header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b bg-card/95 px-4 backdrop-blur sm:px-6">
                    <div className="flex shrink-0 items-center gap-3">
                        <Button
                            aria-label="Mở menu"
                            className="md:hidden"
                            size="icon"
                            variant="ghost"
                            onClick={() => setSidebarOpen(true)}
                        >
                            <Menu aria-hidden="true" className="size-5" />
                        </Button>

                        {/* SalesDay Status pill */}
                        <Link
                            href="/sales-day"
                            className={cn(
                                "hidden sm:flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full border transition-colors",
                                headerSalesDay?.status === "DANG_BAN"
                                    ? "bg-emerald-50 text-emerald-700 border-emerald-200 hover:bg-emerald-100"
                                    : "bg-amber-50 text-amber-700 border-amber-200 hover:bg-amber-100",
                            )}
                        >
                            <span
                                className={cn(
                                    "size-2 rounded-full",
                                    headerSalesDay?.status === "DANG_BAN"
                                        ? "bg-emerald-500 animate-pulse"
                                        : "bg-amber-500",
                                )}
                            />
                            <span>
                                {headerSalesDay?.status === "DANG_BAN"
                                    ? `Đang bán (${headerSalesDay.businessDate})`
                                    : headerSalesDay?.status === "DA_CHOT"
                                      ? "Đã chốt ngày"
                                      : headerSalesDay?.status === "CHO_CHOT"
                                        ? "Chờ chốt ngày"
                                        : "Ngày bán"}
                            </span>
                        </Link>
                    </div>

                    <div className="flex min-w-0 items-center gap-3">
                        {/* Quick Guest QR link */}
                        {showGuestQr && (
                            <Link
                                href="/guest?table=T04"
                                className="hidden lg:flex shrink-0 items-center gap-1.5 text-xs bg-rose-50 text-rose-700 hover:bg-rose-100 px-2.5 py-1.5 rounded-lg font-bold border border-rose-200 transition-colors"
                            >
                                <QrCode size={14} />
                                <span>Giao diện Khách QR</span>
                            </Link>
                        )}

                        <Link
                            href="/profile"
                            className="flex min-w-0 items-center gap-2.5 text-sm hover:opacity-80 transition-opacity"
                        >
                            <span className="hidden text-muted-foreground sm:inline text-xs">
                                Xin chào,
                            </span>
                            <span className="truncate font-semibold text-gray-900 text-xs sm:text-sm">
                                {displayName}
                            </span>
                            <span className="grid size-8 shrink-0 place-items-center rounded-full bg-red-100 text-red-700 font-bold text-xs">
                                {userInitial}
                            </span>
                        </Link>
                    </div>
                </header>

                <main className="mx-auto w-full max-w-7xl p-3 sm:p-5 lg:p-6">
                    {canAccess(pathname, can) ? children : <Forbidden />}
                </main>
            </div>

            {/* ── MOBILE FIXED BOTTOM NAVIGATION (THANH 5 TAB CHÍNH) ────────── */}
            <nav
                aria-label="Thanh điều hướng đáy màn hình di động"
                className="fixed bottom-0 left-0 right-0 z-40 flex items-stretch justify-around gap-1 border-t border-border bg-card px-1 pt-2 pb-[calc(0.5rem+env(safe-area-inset-bottom))] md:hidden"
            >
                {visibleTabs.map(({ label, href, icon: Icon }) => {
                    const active =
                        pathname === href ||
                        (href !== "/tables" && pathname.startsWith(href));
                    return (
                        <Link
                            key={href}
                            href={href}
                            className={cn(
                                "flex min-h-12 min-w-0 flex-1 flex-col items-center justify-center gap-1 rounded-md px-0.5 py-1 text-center transition-colors",
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
                            <span className="text-[11px] leading-tight">
                                {label}
                            </span>
                        </Link>
                    );
                })}
            </nav>
        </div>
    );
}
