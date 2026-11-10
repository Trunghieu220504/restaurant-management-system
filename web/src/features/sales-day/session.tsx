"use client";
import { createContext, useContext, useMemo, type ReactNode } from "react";
import { createSalesDayApi } from "./api";
import { SalesDayScreen } from "./sales-day-screen";
type Session = {
    getAccessToken: () => string | null;
    permissions: readonly string[];
};
const Context = createContext<Session | null>(null);
export function SalesDaySessionProvider({
    session,
    children,
}: {
    session: Session;
    children: ReactNode;
}) {
    return <Context.Provider value={session}>{children}</Context.Provider>;
}
export function ConnectedSalesDay() {
    const session = useContext(Context);
    const api = useMemo(
        () => (session ? createSalesDayApi(session.getAccessToken) : null),
        [session],
    );
    if (!session || !api)
        return (
            <section>
                <h1 className="text-2xl font-semibold">Ngày bán</h1>
                <p role="status" className="mt-4 text-sm text-muted-foreground">
                    Vui lòng đăng nhập bằng tài khoản có quyền quản lý ngày bán.
                </p>
            </section>
        );
    return (
        <SalesDayScreen
            api={api}
            canManage={session.permissions.includes("salesday.manage")}
        />
    );
}
