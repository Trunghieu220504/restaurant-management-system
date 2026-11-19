"use client";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { AuthProvider } from "./auth/auth-provider";
import { RestaurantProvider } from "@/lib/store/restaurant-context";
export function AppProviders({ children }: { children: ReactNode }) {
    const path = usePathname();
    if (path === "/guest" || path.startsWith("/guest/")) return children;
    return (
        <AuthProvider>
            <RestaurantProvider>{children}</RestaurantProvider>
        </AuthProvider>
    );
}
