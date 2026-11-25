"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Package, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LiveQueryScope } from "@/lib/realtime/live-query";
import { readOperation } from "@/features/operations/operation-request";
import type { BusinessDay } from "@/features/sales-day/api";
import { createInventoryApi, type StockCount } from "@/features/inventory/api";
import { useAuth } from "@/components/auth/auth-provider";
import { usePermissions } from "@/components/permission-provider";
import { StockAdjustment } from "@/features/inventory/stock-adjustment";

export default function InventoryPage() {
    const { user } = useAuth();
    if (!user) return null;
    return (
        <LiveQueryScope key={user.id}>
            <InventoryWorkspace />
        </LiveQueryScope>
    );
}

function InventoryWorkspace() {
    const { user } = useAuth();
    const { can } = usePermissions();
    const canAdjust =
        user?.roles.includes("Manager") && can("inventory.adjustments.approve");
    const currentDay = useQuery({
        queryKey: ["stock-adjustment", "day"],
        queryFn: ({ signal }) =>
            readOperation<BusinessDay | null>("/api/salesday/current", signal),
    });
    const day = currentDay.data?.id ?? "";
    const dayOpen =
        !currentDay.isError && currentDay.data?.status === "DANG_BAN";
    const api = createInventoryApi();
    const client = useQueryClient();
    const [tab, setTab] = useState<"catalog" | "counts" | "adjust">("catalog");
    const catalog = useQuery({
        queryKey: ["inventory", "catalog"],
        queryFn: api.catalog,
        retry: false,
    });
    const counts = useQuery({
        queryKey: ["inventory", "counts", day],
        queryFn: () => api.counts(day),
        enabled: tab === "counts" && Boolean(day),
        retry: false,
    });
    const [message, setMessage] = useState("");
    const [busy, setBusy] = useState(false);

    async function update(row: StockCount, quantity: number, approve: boolean) {
        if (!dayOpen || (approve && !row.version)) return;
        setBusy(true);
        try {
            if (approve && row.version)
                await api.approveCount(day, row.ingredientId, row.version);
            else
                await api.saveCount(
                    day,
                    row.ingredientId,
                    quantity,
                    row.version,
                );
            await client.invalidateQueries({
                queryKey: ["inventory", "counts", day],
            });
            setMessage("Đã cập nhật số đếm tồn.");
        } catch (error) {
            setMessage(
                error instanceof Error
                    ? error.message
                    : "Không thể cập nhật tồn kho.",
            );
        } finally {
            setBusy(false);
        }
    }

    return (
        <section className="space-y-4">
            <header className="flex flex-wrap items-center justify-between gap-3">
                <div>
                    <h1 className="flex items-center gap-2 text-2xl font-semibold">
                        <Package size={22} /> Kho nguyên liệu
                    </h1>
                    <p className="text-sm text-muted-foreground">
                        Dữ liệu Inventory API thật
                    </p>
                </div>
                <Button
                    size="icon"
                    variant="outline"
                    aria-label="Tải lại kho"
                    title="Tải lại kho"
                    onClick={() => {
                        void catalog.refetch();
                        void currentDay.refetch();
                        if (tab === "counts" && day) void counts.refetch();
                    }}
                >
                    <RefreshCw size={16} />
                </Button>
            </header>
            <div className="flex flex-wrap gap-2 border-b">
                <Button
                    variant={tab === "catalog" ? "default" : "ghost"}
                    onClick={() => setTab("catalog")}
                >
                    Nguyên liệu
                </Button>
                <Button
                    variant={tab === "counts" ? "default" : "ghost"}
                    onClick={() => setTab("counts")}
                >
                    Số đếm cuối ngày
                </Button>
                {canAdjust && (
                    <Button
                        variant={tab === "adjust" ? "default" : "ghost"}
                        onClick={() => setTab("adjust")}
                    >
                        Điều chỉnh tồn
                    </Button>
                )}
            </div>
            {tab === "adjust" && user && canAdjust && (
                <StockAdjustment key={user.id} />
            )}
            {message && (
                <p
                    role="status"
                    className="border-l-4 border-emerald-500 bg-emerald-50 p-3 text-sm"
                >
                    {message}
                </p>
            )}
            {catalog.isError && (
                <p role="alert" className="text-red-700">
                    {catalog.error.message}
                </p>
            )}
            {tab === "catalog" && (
                <div className="overflow-x-auto rounded border bg-white">
                    <table className="w-full text-sm">
                        <thead>
                            <tr className="border-b bg-muted/40 text-left">
                                <th className="p-3">Mã</th>
                                <th className="p-3">Nguyên liệu</th>
                                <th className="p-3">Đơn vị</th>
                                <th className="p-3">Chế độ</th>
                            </tr>
                        </thead>
                        <tbody>
                            {(catalog.data?.ingredients ?? []).map((item) => (
                                <tr key={String(item.id)} className="border-b">
                                    <td className="p-3 font-mono">{item.ma}</td>
                                    <td className="p-3 font-medium">
                                        {item.ten}
                                    </td>
                                    <td className="p-3">
                                        {catalog.data?.units.find(
                                            (unit) =>
                                                unit.id === item.donViChuanId,
                                        )?.ten ?? "Chưa rõ"}
                                    </td>
                                    <td className="p-3">{item.cheDo}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
            {tab === "counts" && (
                <div className="space-y-3">
                    {currentDay.isError && (
                        <p role="alert">{currentDay.error.message}</p>
                    )}
                    {!dayOpen &&
                        !currentDay.isPending &&
                        !currentDay.isError && (
                            <p className="rounded border bg-amber-50 p-3 text-sm">
                                Cần mở ngày bán trước khi nhập số đếm.
                            </p>
                        )}
                    {counts.isError && (
                        <p role="alert" className="text-red-700">
                            {counts.error.message}
                        </p>
                    )}
                    {counts.data?.map((row) => (
                        <CountRow
                            key={String(row.ingredientId)}
                            row={row}
                            busy={busy || !dayOpen}
                            onSave={(quantity, approve) =>
                                void update(row, quantity, approve)
                            }
                        />
                    ))}
                </div>
            )}
        </section>
    );
}

function CountRow({
    row,
    busy,
    onSave,
}: {
    row: StockCount;
    busy: boolean;
    onSave: (quantity: number, approve: boolean) => void;
}) {
    const [quantity, setQuantity] = useState(
        Number(row.draft ?? row.openingQuantity),
    );
    return (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded border bg-white p-4">
            <div>
                <strong>{row.name}</strong>
                <p className="text-sm text-muted-foreground">
                    Sổ sách: {row.openingQuantity} · Đã duyệt:{" "}
                    {row.approved ?? "Chưa duyệt"}
                </p>
            </div>
            <div className="flex items-center gap-2">
                <input
                    aria-label={"Số đếm " + row.name}
                    className="w-24 rounded border p-2 text-right"
                    type="number"
                    step="0.001"
                    value={quantity}
                    onChange={(event) =>
                        setQuantity(Number(event.target.value))
                    }
                />
                <Button disabled={busy} onClick={() => onSave(quantity, false)}>
                    Lưu nháp
                </Button>
                <Button
                    disabled={busy || !row.version}
                    variant="outline"
                    onClick={() => onSave(quantity, true)}
                >
                    Duyệt
                </Button>
            </div>
        </div>
    );
}
