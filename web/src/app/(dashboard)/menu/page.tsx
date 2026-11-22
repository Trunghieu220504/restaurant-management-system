"use client";
import { CatalogScreen } from "@/features/catalog/catalog-screen";
import { LiveQueryScope } from "@/lib/realtime/live-query";
import { useAuth } from "@/components/auth/auth-provider";
export default function MenuPage() {
    const { user } = useAuth();
    return (
        user && (
            <LiveQueryScope key={user.id}>
                <CatalogScreen />
            </LiveQueryScope>
        )
    );
}
