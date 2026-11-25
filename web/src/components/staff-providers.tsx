"use client";

import type { ReactNode } from "react";
import { AuthProvider } from "./auth/auth-provider";
import { Toaster } from "./ui/sonner";

export default function StaffProviders({ children }: { children: ReactNode }) {
    return (
        <>
            <AuthProvider>{children}</AuthProvider>
            <Toaster richColors position="top-right" />
        </>
    );
}
