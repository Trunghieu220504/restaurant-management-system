"use client";

import { createContext, useContext, type ReactNode } from "react";

type PermissionContextValue = {
    permissions: ReadonlySet<string>;
    can: (permission: string) => boolean;
};

const PermissionContext = createContext<PermissionContextValue | null>(null);

export function PermissionProvider({
    permissions,
    children,
}: {
    permissions: readonly string[];
    children: ReactNode;
}) {
    const granted = new Set(permissions);

    return (
        <PermissionContext.Provider
            value={{
                permissions: granted,
                can: (permission) => granted.has(permission),
            }}
        >
            {children}
        </PermissionContext.Provider>
    );
}

export function usePermissions() {
    const context = useContext(PermissionContext);
    if (!context) {
        throw new Error(
            "usePermissions must be used inside PermissionProvider",
        );
    }
    return context;
}
