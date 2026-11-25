"use client";
import dynamic from "next/dynamic";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

const StaffProviders = dynamic(() => import("./staff-providers"));

export function AppProviders({ children }: { children: ReactNode }) {
    const path = usePathname();
    if (path === "/guest" || path.startsWith("/guest/")) return children;
    return <StaffProviders>{children}</StaffProviders>;
}
