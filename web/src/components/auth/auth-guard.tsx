"use client";

import { useEffect, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "./auth-provider";

export function AuthGuard({ children }: { children: ReactNode }) {
    const { isAuthenticated, isLoading } = useAuth();
    const router = useRouter();

    useEffect(() => {
        if (!isLoading && !isAuthenticated) {
            router.replace("/login");
        }
    }, [isLoading, isAuthenticated, router]);

    // While restoring session on initial load, show clean loading skeleton to prevent screen flickering
    if (isLoading) {
        return (
            <div className="min-h-screen bg-slate-50 flex flex-col items-center justify-center p-4">
                <div className="space-y-4 text-center max-w-sm w-full bg-white p-8 rounded-2xl shadow-sm border border-slate-100">
                    <div className="size-12 rounded-xl bg-red-500 text-white font-bold flex items-center justify-center text-xl mx-auto animate-pulse">
                        R
                    </div>
                    <div className="space-y-1">
                        <h3 className="font-bold text-slate-900 text-sm">
                            Đang khôi phục phiên đăng nhập...
                        </h3>
                        <p className="text-xs text-slate-500">
                            Đang kiểm tra refresh token và phân quyền hệ thống
                        </p>
                    </div>
                    <div className="w-full bg-slate-100 h-1.5 rounded-full overflow-hidden">
                        <div className="bg-red-500 h-full w-2/3 animate-[pulse_1s_infinite]"></div>
                    </div>
                </div>
            </div>
        );
    }

    if (!isAuthenticated) {
        return null;
    }

    return <>{children}</>;
}
