"use client";

import { useMemo } from "react";
import { CheckCircle2, RefreshCw } from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { authFetch } from "@/lib/auth/auth-client";
import { liveInterval } from "@/lib/realtime/live-query";

type Portion = {
    id: string;
    orderCode: string;
    dish: string;
    variant: string;
    note: string | null;
    status: string;
    version: string;
};

type ReadyTicket = {
    id: number | string;
    portionId: number | string;
    station: "BEP" | "BAR";
    status: "SAN_SANG";
    createdAt: string;
    version: string;
};

async function readJson<T>(path: string, signal?: AbortSignal): Promise<T> {
    const response = await authFetch(path, { signal, cache: "no-store" });
    if (!response.ok) throw new Error("Không tải được dữ liệu phục vụ.");
    return response.json();
}

async function readyTickets(signal?: AbortSignal) {
    const [sales, kitchen, bar] = await Promise.all([
        readJson<Portion[]>("/api/sales/portions", signal),
        readJson<ReadyTicket[]>("/api/fulfillment/queue/BEP", signal),
        readJson<ReadyTicket[]>("/api/fulfillment/queue/BAR", signal),
    ]);
    const portions = new Map(sales.map((portion) => [portion.id, portion]));
    return [...kitchen, ...bar]
        .filter((ticket) => ticket.status === "SAN_SANG")
        .map((ticket) => ({
            ticket,
            portion: portions.get(String(ticket.portionId)),
        }))
        .filter(
            (
                row,
            ): row is {
                ticket: ReadyTicket;
                portion: Portion;
            } => row.portion !== undefined,
        );
}

export function ServiceLive() {
    const client = useQueryClient();
    const ready = useQuery({
        queryKey: ["fulfillment", "ready-portions"],
        queryFn: ({ signal }) => readyTickets(signal),
        refetchInterval: liveInterval,
        retry: false,
    });
    const deliver = useMutation({
        mutationFn: async (ticket: ReadyTicket) => {
            const response = await authFetch(
                "/api/fulfillment/tickets/" + ticket.id + "/deliver",
                {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        station: ticket.station,
                        version: ticket.version,
                        requestKey: crypto.randomUUID(),
                    }),
                },
            );
            if (!response.ok) {
                const problem = await response.json().catch(() => ({}));
                throw new Error(problem.code ?? "Không thể xác nhận giao món.");
            }
        },
        onSuccess: () =>
            client.invalidateQueries({
                queryKey: ["fulfillment", "ready-portions"],
            }),
    });
    const rows = useMemo(() => ready.data ?? [], [ready.data]);

    return (
        <section
            aria-label="Món sẵn sàng phục vụ"
            className="space-y-3 rounded border bg-white p-4"
        >
            <header className="flex items-center justify-between gap-3">
                <div>
                    <h2 className="font-semibold">Món sẵn sàng phục vụ</h2>
                    <p className="text-sm text-muted-foreground">
                        Dữ liệu bếp/bar thật, tự cập nhật
                    </p>
                </div>
                <Button
                    size="icon"
                    variant="outline"
                    aria-label="Tải lại món sẵn sàng"
                    title="Tải lại món sẵn sàng"
                    onClick={() => void ready.refetch()}
                >
                    <RefreshCw size={16} />
                </Button>
            </header>
            {ready.isError && (
                <p role="alert" className="text-sm text-red-700">
                    {ready.error.message}
                </p>
            )}
            {rows.length === 0 && !ready.isLoading && (
                <p className="text-sm text-muted-foreground">
                    Chưa có món sẵn sàng.
                </p>
            )}
            <ul className="grid gap-3 md:grid-cols-2">
                {rows.map(({ ticket, portion }) => (
                    <li
                        key={String(ticket.id)}
                        className="flex items-center justify-between gap-3 rounded border p-3"
                    >
                        <div className="min-w-0">
                            <p className="font-semibold">{portion.dish}</p>
                            <p className="text-sm text-muted-foreground">
                                {portion.orderCode} ·{" "}
                                {ticket.station === "BEP" ? "Bếp" : "Bar"}
                            </p>
                            {portion.note && (
                                <p className="text-sm">{portion.note}</p>
                            )}
                        </div>
                        <Button
                            disabled={deliver.isPending}
                            onClick={() => deliver.mutate(ticket)}
                        >
                            <CheckCircle2 size={16} /> Đã giao
                        </Button>
                    </li>
                ))}
            </ul>
            {deliver.isError && (
                <p role="alert" className="text-sm text-red-700">
                    {deliver.error.message}
                </p>
            )}
        </section>
    );
}
