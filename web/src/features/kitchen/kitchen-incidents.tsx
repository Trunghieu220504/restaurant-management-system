"use client";

import { useAuth } from "@/components/auth/auth-provider";
import { usePermissions } from "@/components/permission-provider";
import { LiveQueryScope } from "@/lib/realtime/live-query";
import { IncidentPanel } from "./incident-panel";

export function KitchenIncidents() {
    const { user } = useAuth();
    const { can } = usePermissions();
    if (!user || !can("fulfillment.incidents.report")) return null;
    return (
        <LiveQueryScope key={user.id}>
            <IncidentPanel />
        </LiveQueryScope>
    );
}
