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
  type LucideIcon,
} from "lucide-react";
import { useState, type ReactNode } from "react";
import { usePermissions } from "@/components/permission-provider";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type NavigationItem = {
  label: string;
  href: string;
  icon: LucideIcon;
  permissions?: string[];
};

const navigation: NavigationItem[] = [
  { label: "Tổng quan", href: "/", icon: LayoutDashboard },
  {
    label: "Đơn hàng",
    href: "/orders",
    icon: ReceiptText,
    permissions: ["sales.orders.manage"],
  },
  {
    label: "Thực đơn",
    href: "/menu",
    icon: Boxes,
    permissions: ["catalog.menu.manage"],
  },
  {
    label: "Nhân viên",
    href: "/staff",
    icon: Users,
    permissions: ["identity.accounts.manage", "identity.subordinates.manage"],
  },
  {
    label: "Phân quyền",
    href: "/settings",
    icon: Settings,
    permissions: ["identity.roles.manage"],
  },
];

export function AppShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const { can } = usePermissions();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  return (
    <div className="min-h-screen bg-muted/50">
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
        <Link href="/" className="flex h-16 items-center gap-3 border-b px-6 font-semibold">
          <span className="grid size-8 place-items-center rounded-lg bg-primary text-primary-foreground">
            R
          </span>
          Restaurant OS
        </Link>
        <nav aria-label="Điều hướng chính" className="flex-1 space-y-1 p-3">
          {navigation
            .filter(
              (item) =>
                !item.permissions || item.permissions.some((permission) => can(permission)),
            )
            .map(({ label, href, icon: Icon }) => {
              const active = pathname === href;
              return (
                <Link
                  key={href}
                  href={href}
                  aria-current={active ? "page" : undefined}
                  onClick={() => setSidebarOpen(false)}
                  className={cn(
                    "flex items-center gap-3 rounded-md px-3 py-2.5 text-sm font-medium",
                    active
                      ? "bg-muted text-foreground"
                      : "text-muted-foreground hover:bg-muted hover:text-foreground",
                  )}
                >
                  <Icon aria-hidden="true" className="size-4" />
                  {label}
                </Link>
              );
            })}
        </nav>
        <div className="border-t p-4 text-xs text-muted-foreground">
          Restaurant Management
        </div>
      </aside>

      <div className="lg:pl-64">
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
            <span className="text-sm text-muted-foreground">Quản lý nhà hàng</span>
          </div>
          <div className="flex items-center gap-3 text-sm">
            <span className="hidden text-muted-foreground sm:inline">Xin chào,</span>
            <span className="font-medium">Tài khoản demo</span>
            <span className="grid size-9 place-items-center rounded-full bg-muted font-semibold">
              D
            </span>
          </div>
        </header>
        <main className="mx-auto w-full max-w-7xl p-4 sm:p-6 lg:p-8">{children}</main>
      </div>
    </div>
  );
}
