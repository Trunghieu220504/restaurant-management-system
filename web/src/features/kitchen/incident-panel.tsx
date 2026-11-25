"use client";

import { useId, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { usePermissions } from "@/components/permission-provider";
import { useAuth } from "@/components/auth/auth-provider";
import {
    OperationError,
    readOperation,
    useOperationCommand,
} from "@/features/operations/operation-request";
import type { components } from "@/lib/api/generated";

type Snapshot = components["schemas"]["OperationsSnapshot"];
// HIE-6.1 incident response fields used by the pending queue.
type PendingIncident = {
    id: number | string;
    ticketId: number | string;
    reason: string;
    version: string;
};

const decisions = [
    { code: "LAM_LAI", label: "Làm lại món" },
    { code: "HUY_VAN_THU", label: "Hủy món, giữ nguyên tiền phải thu" },
    { code: "HUY_GIAM_NGHIA_VU", label: "Hủy món, giảm tiền phải thu" },
    { code: "reject", label: "Từ chối báo cáo" },
] as const;
type Decision = (typeof decisions)[number]["code"];
const ticketStatuses: Record<string, string> = {
    MOI: "Mới",
    DANG_LAM: "Đang làm",
    SAN_SANG: "Sẵn sàng",
    DA_GIAO: "Đã giao",
};
const field = "min-h-11 min-w-0 rounded-md border bg-background px-3 py-2";

export function IncidentPanel({ orderId }: { orderId?: string }) {
    const { can } = usePermissions();
    const { user } = useAuth();
    const title = useId();
    const approve =
        can("fulfillment.incidents.approve") && user?.roles.includes("Manager");
    const [ticketId, selectTicket] = useState("");
    const [incident, selectIncident] = useState<PendingIncident | null>(null);
    const [decision, setDecision] = useState<Decision | "">("");
    const [reason, setReason] = useState("");
    const [notice, setNotice] = useState("");
    const incidents = useQuery({
        queryKey: ["fulfillment", "pending-incidents"],
        queryFn: ({ signal }) =>
            readOperation<PendingIncident[]>(
                "/api/fulfillment/incidents",
                signal,
            ),
    });
    const snapshot = useQuery({
        queryKey: ["fulfillment", "incident-tickets", orderId],
        queryFn: ({ signal }) =>
            readOperation<Snapshot>(
                "/api/fulfillment/snapshot" +
                    (orderId ? "?orderId=" + encodeURIComponent(orderId) : ""),
                signal,
            ),
    });
    const refresh = () =>
        Promise.all([snapshot.refetch(), incidents.refetch()]);
    const command = useOperationCommand(async () => {
        setNotice(incident ? "Đã xử lý sự cố." : "Đã gửi báo cáo sự cố.");
        selectIncident(null);
        selectTicket("");
        setReason("");
        setDecision("");
        await refresh();
    }, refresh);
    const pendingTickets = new Set(
        incidents.data?.map((row) => String(row.ticketId)),
    );
    const tickets =
        snapshot.data?.tickets.filter(
            (row) =>
                ticketStatuses[row.status] &&
                !pendingTickets.has(String(row.id)),
        ) ?? [];
    const ticket = tickets.find((row) => String(row.id) === ticketId);
    const currentIncident = incidents.data?.find(
        (row) => String(row.id) === String(incident?.id),
    );
    const changed = incident && currentIncident?.version !== incident.version;
    const error = command.error ?? incidents.error ?? snapshot.error;
    const loading = incidents.isPending || snapshot.isPending;
    const disabled = loading || command.isPending || incidents.isError;
    return (
        <section
            className="grid max-w-3xl gap-4 border-t pt-5"
            aria-labelledby={title}
        >
            <div className="flex flex-wrap items-center justify-between gap-2">
                <h2 id={title} className="text-xl font-semibold">
                    Sự cố món
                </h2>
                <Button
                    variant="outline"
                    className="min-h-11"
                    disabled={
                        command.isPending ||
                        incidents.isFetching ||
                        snapshot.isFetching
                    }
                    onClick={() => {
                        command.reset();
                        void refresh();
                    }}
                >
                    Tải lại
                </Button>
            </div>
            {loading && <p role="status">Đang tải phiếu và sự cố…</p>}
            {notice && <p role="status">{notice}</p>}
            {error && (
                <p role="alert" className="text-sm text-red-700">
                    {incidents.error instanceof OperationError &&
                    incidents.error.status === 404
                        ? "Máy chủ hiện tại chưa cung cấp API sự cố món."
                        : error.message}
                </p>
            )}
            {incidents.data && (
                <section className="grid gap-2">
                    <h3 className="font-semibold">Sự cố chờ duyệt</h3>
                    {incidents.data.length === 0 && (
                        <p>Chưa có sự cố chờ duyệt.</p>
                    )}
                    {incidents.data.map((row) => (
                        <article
                            key={row.id}
                            className="flex flex-wrap items-center justify-between gap-2 border-b py-2"
                        >
                            <div className="min-w-0">
                                <strong>
                                    Sự cố #{row.id} · Phiếu #{row.ticketId}
                                </strong>
                                <p className="break-words text-sm">
                                    {row.reason}
                                </p>
                            </div>
                            {approve && (
                                <Button
                                    variant="outline"
                                    className="min-h-11"
                                    disabled={disabled}
                                    onClick={() => {
                                        command.reset();
                                        setNotice("");
                                        selectIncident(row);
                                        setReason("");
                                        setDecision("");
                                    }}
                                >
                                    Xử lý
                                </Button>
                            )}
                        </article>
                    ))}
                </section>
            )}
            {incident ? (
                <form
                    className="grid max-w-md gap-2 border-t pt-3"
                    onSubmit={(event) => {
                        event.preventDefault();
                        if (
                            disabled ||
                            !approve ||
                            changed ||
                            !decision ||
                            !reason.trim()
                        )
                            return;
                        command.mutate({
                            path: `/api/fulfillment/incidents/${incident.id}/decision`,
                            keyInBody: true,
                            body: {
                                version: incident.version,
                                decision:
                                    decision === "reject" ? null : decision,
                                reason: reason.trim(),
                            },
                        });
                    }}
                >
                    <h3 className="font-semibold">
                        Xử lý sự cố #{incident.id}
                    </h3>
                    {changed && (
                        <p role="alert">
                            Sự cố đã thay đổi. Hãy chọn lại từ danh sách.
                        </p>
                    )}
                    <label className="grid gap-1">
                        Quyết định
                        <select
                            className={field}
                            required
                            disabled={disabled}
                            value={decision}
                            onChange={(event) =>
                                setDecision(event.target.value as Decision | "")
                            }
                        >
                            <option value="">Chọn quyết định</option>
                            {decisions.map((item) => (
                                <option key={item.code} value={item.code}>
                                    {item.label}
                                </option>
                            ))}
                        </select>
                    </label>
                    <label className="grid gap-1">
                        Lý do xử lý
                        <textarea
                            className={field}
                            required
                            maxLength={1000}
                            disabled={disabled}
                            value={reason}
                            onChange={(event) => setReason(event.target.value)}
                        />
                    </label>
                    <div className="flex flex-wrap gap-2">
                        <Button
                            className="min-h-11"
                            disabled={
                                disabled ||
                                !decision ||
                                !reason.trim() ||
                                !!changed
                            }
                        >
                            {command.isPending
                                ? "Đang lưu…"
                                : "Xác nhận quyết định"}
                        </Button>
                        <Button
                            variant="outline"
                            type="button"
                            className="min-h-11"
                            disabled={command.isPending}
                            onClick={() => {
                                selectIncident(null);
                                setReason("");
                            }}
                        >
                            Quay lại
                        </Button>
                    </div>
                </form>
            ) : (
                incidents.data &&
                snapshot.data &&
                !snapshot.isError && (
                    <form
                        className="grid max-w-md gap-2 border-t pt-3"
                        onSubmit={(event) => {
                            event.preventDefault();
                            if (disabled || !ticket || !reason.trim()) return;
                            setNotice("");
                            command.mutate({
                                path: `/api/fulfillment/tickets/${ticket.id}/incidents`,
                                keyInBody: true,
                                body: {
                                    version: ticket.version,
                                    station: ticket.station,
                                    reason: reason.trim(),
                                },
                            });
                        }}
                    >
                        <h3 className="font-semibold">Báo sự cố</h3>
                        {tickets.length ? (
                            <>
                                <label className="grid gap-1">
                                    Phiếu chế biến
                                    <select
                                        className={field}
                                        required
                                        disabled={disabled}
                                        value={ticketId}
                                        onChange={(event) =>
                                            selectTicket(event.target.value)
                                        }
                                    >
                                        <option value="">Chọn phiếu</option>
                                        {tickets.map((row) => (
                                            <option key={row.id} value={row.id}>
                                                #{row.id} · Phần món #
                                                {row.portionId} ·{" "}
                                                {row.station === "BAR"
                                                    ? "Bar"
                                                    : "Bếp"}{" "}
                                                · {ticketStatuses[row.status]}
                                            </option>
                                        ))}
                                    </select>
                                </label>
                                <label className="grid gap-1">
                                    Lý do báo sự cố
                                    <textarea
                                        className={field}
                                        required
                                        maxLength={1000}
                                        disabled={disabled}
                                        value={reason}
                                        onChange={(event) =>
                                            setReason(event.target.value)
                                        }
                                    />
                                </label>
                                <Button
                                    className="min-h-11 justify-self-start"
                                    disabled={
                                        disabled || !ticket || !reason.trim()
                                    }
                                >
                                    {command.isPending
                                        ? "Đang gửi…"
                                        : "Gửi báo cáo"}
                                </Button>
                            </>
                        ) : (
                            <p>Không có phiếu đủ điều kiện báo sự cố.</p>
                        )}
                    </form>
                )
            )}
        </section>
    );
}
