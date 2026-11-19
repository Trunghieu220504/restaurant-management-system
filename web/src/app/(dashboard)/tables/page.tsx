"use client";
import { useMemo } from "react";
import { useAuth } from "@/components/auth/auth-provider";
import { usePermissions } from "@/components/permission-provider";
import { createSeatingApi } from "@/features/seating/api";
import { SeatingBoardView } from "@/features/seating/seating-board";

export default function TablesPage() {
    const { user } = useAuth();
    const { can } = usePermissions();
    const catalog = can("seating.catalog.manage"),
        sessions = can("seating.sessions.manage");
    const gateway = useMemo(
        () => createSeatingApi({ catalog, sessions }),
        [catalog, sessions],
    );
    if (!user || !(catalog || sessions)) return null;
    return (
        <SeatingBoardView
            key={[user.id, catalog, sessions].join(":")}
            gateway={gateway}
            canManageCatalog={catalog}
            canManageSessions={sessions}
        />
    );
}
