"use client";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { AuthProvider } from "./auth/auth-provider";
export function AppProviders({ children }: { children: ReactNode }) {
    const path = usePathname();
    if (path === "/guest" || path.startsWith("/guest/")) return children;
    return <AuthProvider>{children}</AuthProvider>;
}
