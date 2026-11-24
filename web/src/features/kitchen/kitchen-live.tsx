"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { WaitingTimer } from "@/components/ops/controls";
import {
    createFulfillmentApi,
    FulfillmentApiError,
    type FulfillmentStation,
    type FulfillmentStatus,
    type FulfillmentTicket,
} from "./api";

const columns: readonly [FulfillmentStatus, string][] = [
    ["MOI", "Mới"],
    ["DANG_LAM", "Đang làm"],
    ["SAN_SANG", "Sẵn sàng"],
];

const errorMessages: Record<string, string> = {
    "fulfillment.ticket.invalid_transition":
        "Phiếu đã thay đổi. Đang tải lại trạng thái.",
    "fulfillment.ticket.already_processed": "Phiếu này đã được xử lý.",
    "fulfillment.ticket.concurrency_conflict":
        "Phiếu vừa được xử lý ở nơi khác.",
    "fulfillment.ticket.station_forbidden":
        "Bạn không có quyền xử lý trạm này.",
};

function actionFor(status: FulfillmentStatus) {
    if (status === "MOI") return { name: "start" as const, label: "Bắt đầu" };
    if (status === "DANG_LAM")
        return { name: "ready" as const, label: "Hoàn thành" };
    if (status === "SAN_SANG")
        return { name: "deliver" as const, label: "Giao món" };
    return null;
}

export function KitchenLive({
    permissions,
}: {
    permissions: readonly string[];
}) {
    const [station, setStation] = useState<"all" | FulfillmentStation>("all");
    const [tickets, setTickets] = useState<FulfillmentTicket[]>([]);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState<number | null>(null);
    const [error, setError] = useState("");
    const api = useMemo(() => createFulfillmentApi(), []);
    const stations = useMemo(
        () =>
            [
                ...(permissions.includes("fulfillment.kitchen.process")
                    ? (["BEP"] as const)
                    : []),
                ...(permissions.includes("fulfillment.bar.process")
                    ? (["BAR"] as const)
                    : []),
            ] as FulfillmentStation[],
        [permissions],
    );

    const load = useCallback(
        async (signal?: AbortSignal) => {
            if (stations.length === 0) {
                setTickets([]);
                setLoading(false);
                return;
            }
            setError("");
            try {
                const selected = station === "all" ? stations : [station];
                const result = await Promise.all(
                    selected.map((value) => api.queue(value, signal)),
                );
                setTickets(
                    result
                        .flat()
                        .sort((a, b) =>
                            a.createdAt.localeCompare(b.createdAt),
                        ),
                );
            } catch (cause) {
                if (
                    cause instanceof DOMException &&
                    cause.name === "AbortError"
                )
                    return;
                setError(
                    cause instanceof FulfillmentApiError
                        ? (errorMessages[cause.code] ??
                          "Không tải được hàng đợi bếp/bar.")
                        : "Không kết nối được máy chủ.",
                );
            } finally {
                if (!signal?.aborted) setLoading(false);
            }
        },
        [api, station, stations],
    );

    useEffect(() => {
        const controller = new AbortController();
        const initialLoad = window.setTimeout(
            () => void load(controller.signal),
            0,
        );
        const timer = window.setInterval(() => void load(), 5000);
        return () => {
            controller.abort();
            window.clearTimeout(initialLoad);
            window.clearInterval(timer);
        };
    }, [load]);

    async function transition(ticket: FulfillmentTicket) {
        const action = actionFor(ticket.status);
        if (!action) return;
        const id = Number(ticket.id);
        setBusy(id);
        setError("");
        try {
            await api.transition(ticket, action.name);
            await load();
        } catch (cause) {
            setError(
                cause instanceof FulfillmentApiError
                    ? (errorMessages[cause.code] ?? "Không thể cập nhật phiếu.")
                    : "Không thể cập nhật phiếu.",
            );
            await load();
        } finally {
            setBusy(null);
        }
    }

    return (
        <section className="space-y-5" aria-label="Hàng đợi bếp và bar">
            <header className="flex flex-wrap items-center justify-between gap-3">
                <div>
                    <h2 className="text-xl font-semibold">Bếp / Bar</h2>
                    <p className="text-sm text-muted-foreground">
                        Dữ liệu phiếu chế biến từ máy chủ
                    </p>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                    <label htmlFor="live-station">Trạm</label>
                    <select
                        id="live-station"
                        value={station}
                        className="min-h-11 rounded border bg-background p-2"
                        onChange={(event) =>
                            setStation(event.target.value as typeof station)
                        }
                    >
                        <option value="all">Tất cả</option>
                        {stations.includes("BEP") && (
                            <option value="BEP">Bếp</option>
                        )}
                        {stations.includes("BAR") && (
                            <option value="BAR">Bar</option>
                        )}
                    </select>
                    <Button
                        variant="outline"
                        size="icon"
                        aria-label="Tải lại hàng đợi"
                        title="Tải lại hàng đợi"
                        onClick={() => void load()}
                    >
                        <RefreshCw size={16} />
                    </Button>
                </div>
            </header>
            {error && (
                <p
                    role="alert"
                    className="border-l-4 border-amber-500 bg-amber-50 p-3 text-sm"
                >
                    {error}
                </p>
            )}
            <div aria-busy={loading} className="grid gap-5 lg:grid-cols-3">
                {columns.map(([status, label]) => {
                    const visible = tickets.filter(
                        (ticket) => ticket.status === status,
                    );
                    return (
                        <section
                            key={status}
                            aria-label={label}
                            className="min-w-0 border-t-2 border-emerald-600 pt-3"
                        >
                            <h3 className="mb-3 font-semibold">
                                {label} ({visible.length})
                            </h3>
                            {loading && visible.length === 0 ? (
                                <p className="rounded border bg-white p-4 text-sm text-muted-foreground">
                                    Đang tải phiếu…
                                </p>
                            ) : (
                                <ul className="space-y-3">
                                    {visible.map((ticket) => {
                                        const action = actionFor(ticket.status);
                                        const id = Number(ticket.id);
                                        return (
                                            <li
                                                key={id}
                                                className="space-y-3 rounded border bg-white p-4"
                                            >
                                                <div className="flex items-center justify-between gap-3">
                                                    <p className="font-semibold">
                                                        Phiếu #{id}
                                                    </p>
                                                    <span className="text-xs text-muted-foreground">
                                                        {ticket.station === "BEP"
                                                            ? "Bếp"
                                                            : "Bar"}
                                                    </span>
                                                </div>
                                                <p className="text-sm text-muted-foreground">
                                                    Phần món #{ticket.portionId}
                                                </p>
                                                <WaitingTimer
                                                    since={ticket.createdAt}
                                                />
                                                {action && (
                                                    <Button
                                                        disabled={busy === id}
                                                        onClick={() =>
                                                            void transition(
                                                                ticket,
                                                            )
                                                        }
                                                    >
                                                        {busy === id
                                                            ? "Đang cập nhật…"
                                                            : action.label}
                                                    </Button>
                                                )}
                                            </li>
                                        );
                                    })}
                                </ul>
                            )}
                        </section>
                    );
                })}
            </div>
        </section>
    );
}
