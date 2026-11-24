"use client";
import { useEffect, useMemo, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { WaitingTimer } from "@/components/ops/controls";
import {
    authFetch,
    refreshAccessTokenSingleFlight,
} from "@/lib/auth/auth-client";
import { getAccessToken } from "@/lib/auth/token-store";
import {
    OperationsFeed,
    type ConnectionState,
} from "@/lib/realtime/operations";
import { createSignalRTransport } from "@/lib/realtime/signalr";
import {
    createBrowserAudio,
    NotificationSound,
    type SoundState,
} from "@/lib/sound/notification-sound";
import {
    createFulfillmentApi,
    FulfillmentApiError,
    type FulfillmentTicket,
} from "./api";

function version(value: string): bigint {
    const bytes = atob(value);
    if (bytes.length !== 8) throw new Error("Invalid ticket version");
    return [...bytes].reduce(
        (total, byte) => total * BigInt(256) + BigInt(byte.charCodeAt(0)),
        BigInt(0),
    );
}
const terminal = (status: string) =>
    status === "DA_GIAO" || status === "DA_HUY";
export function KitchenLive({
    permissions,
}: {
    permissions: readonly string[];
}) {
    const canServe = permissions.includes("fulfillment.portions.serve");
    const stationKey = ["BEP", "BAR"]
        .filter(
            (station) =>
                canServe ||
                permissions.includes(
                    station === "BEP"
                        ? "fulfillment.kitchen.process"
                        : "fulfillment.bar.process",
                ),
        )
        .join(",");
    const [station, setStation] = useState("all");
    const [tickets, setTickets] = useState<FulfillmentTicket[]>([]);
    const [connection, setConnection] = useState<ConnectionState>("offline");
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [busy, setBusy] = useState<number | null>(null);
    const [retry, setRetry] = useState(0);
    const [soundState, setSoundState] = useState<SoundState>({
        muted: false,
        blocked: false,
        unlocked: false,
    });
    const sound = useRef<NotificationSound | null>(null);
    const appliedVersions = useRef(
        new Map<string, { value: bigint; terminal: boolean }>(),
    );
    const api = useMemo(() => createFulfillmentApi(), []);
    useEffect(() => {
        const player = new NotificationSound(
            createBrowserAudio(),
            window.localStorage,
            setSoundState,
        );
        sound.current = player;
        return () => {
            sound.current = null;
            void player.dispose();
        };
    }, []);
    useEffect(() => {
        const stations = stationKey.split(",").filter(Boolean);
        if (!stations.length) return;
        const versions = new Map<
            string,
            { value: bigint; terminal: boolean }
        >();
        appliedVersions.current = versions;
        const remember = (id: string | number, raw: string, ended: boolean) => {
            const value = version(raw),
                before = versions.get(String(id));
            if (!before || value > before.value)
                versions.set(String(id), { value, terminal: ended });
        };
        const selected =
            station === "all"
                ? stations
                : stations.filter((value) => value === station);
        const transport = createSignalRTransport<FulfillmentTicket>(
            "/hubs/operations",
            "TicketChanged",
            async () => {
                const token = getAccessToken();
                try {
                    const payload = JSON.parse(
                        atob(
                            token!
                                .split(".")[1]
                                .replace(/-/g, "+")
                                .replace(/_/g, "/"),
                        ),
                    );
                    if (Number(payload.exp) * 1000 > Date.now() + 30000)
                        return token!;
                } catch {
                    /* A missing/expired token must be refreshed before reconnect. */
                }
                return (await refreshAccessTokenSingleFlight()) ?? "";
            },
            canServe
                ? [{ method: "JoinServing" }]
                : selected.map((value) => ({
                      method: "JoinStation",
                      args: [value],
                  })),
        );
        const feed = new OperationsFeed(
            transport,
            async () => {
                const response = await authFetch(
                    "/api/fulfillment/snapshot" +
                        (station === "all" ? "" : "?station=" + station),
                    { cache: "no-store" },
                );
                if (!response.ok)
                    throw new Error("Không tải được trạng thái chế biến.");
                const snapshot: {
                    tickets: FulfillmentTicket[];
                    tombstones: { id: number; version: string }[];
                } = await response.json();
                snapshot.tombstones.forEach((row) =>
                    remember(row.id, row.version, true),
                );
                snapshot.tickets.forEach((row) =>
                    remember(row.id, row.version, terminal(row.status)),
                );
                return snapshot.tickets.filter(
                    (row) =>
                        selected.includes(row.station) &&
                        !versions.get(String(row.id))?.terminal,
                );
            },
            {
                snapshot: (rows) => {
                    setTickets((current) =>
                        rows.map((row) => {
                            const newer = current.find(
                                (item) => String(item.id) === String(row.id),
                            );
                            return newer &&
                                version(newer.version) > version(row.version)
                                ? newer
                                : row;
                        }),
                    );
                    setError("");
                },
                state: setConnection,
                error: () =>
                    setError(
                        "Mất kết nối thời gian thực. Đang thử kết nối lại; bạn có thể tải lại hàng đợi.",
                    ),
                event: (event, audible) => {
                    const row = event.payload;
                    if (!selected.includes(row.station)) return;
                    let value: bigint;
                    try {
                        value = version(row.version);
                    } catch {
                        return;
                    }
                    if (
                        value <=
                        (versions.get(String(row.id))?.value ?? BigInt(-1))
                    )
                        return;
                    remember(row.id, row.version, terminal(row.status));
                    setTickets((current) =>
                        terminal(row.status)
                            ? current.filter(
                                  (item) => String(item.id) !== String(row.id),
                              )
                            : [
                                  ...current.filter(
                                      (item) =>
                                          String(item.id) !== String(row.id),
                                  ),
                                  row,
                              ].sort((a, b) =>
                                  a.createdAt.localeCompare(b.createdAt),
                              ),
                    );
                    if (
                        audible &&
                        (canServe
                            ? row.status === "SAN_SANG"
                            : row.status === "MOI")
                    ) {
                        setNotice(
                            canServe
                                ? "Có món vừa sẵn sàng để giao."
                                : "Có phiếu chế biến mới.",
                        );
                        void sound.current?.notify();
                    }
                },
            },
        );
        void feed.start();
        return () => {
            void feed.stop();
        };
    }, [stationKey, station, canServe, retry]);
    useEffect(() => {
        if (connection !== "offline") return;
        const timer = window.setInterval(
            () => setRetry((value) => value + 1),
            10000,
        );
        return () => window.clearInterval(timer);
    }, [connection]);
    function actionFor(ticket: FulfillmentTicket) {
        if (ticket.status === "SAN_SANG")
            return canServe
                ? { name: "deliver" as const, label: "Giao món" }
                : null;
        const process = permissions.includes(
            ticket.station === "BEP"
                ? "fulfillment.kitchen.process"
                : "fulfillment.bar.process",
        );
        if (!process) return null;
        if (ticket.status === "MOI")
            return { name: "start" as const, label: "Bắt đầu" };
        if (ticket.status === "DANG_LAM")
            return { name: "ready" as const, label: "Hoàn thành" };
        return null;
    }
    async function transition(ticket: FulfillmentTicket) {
        const action = actionFor(ticket);
        if (!action) return;
        setBusy(Number(ticket.id));
        setError("");
        try {
            const response = await api.transition(ticket, action.name);
            const updated = {
                ...response,
                dishName: response.dishName ?? ticket.dishName,
                variantName: response.variantName ?? ticket.variantName,
                optionNames: response.optionNames ?? ticket.optionNames,
                note: response.note ?? ticket.note,
                orderCode: response.orderCode ?? ticket.orderCode,
            };
            appliedVersions.current.set(String(updated.id), {
                value: version(updated.version),
                terminal: terminal(updated.status),
            });
            setTickets((current) =>
                terminal(updated.status)
                    ? current.filter(
                          (row) => String(row.id) !== String(updated.id),
                      )
                    : current.map((row) =>
                          String(row.id) === String(updated.id) ? updated : row,
                      ),
            );
        } catch (cause) {
            setError(
                cause instanceof FulfillmentApiError && cause.status === 409
                    ? "Phiếu vừa thay đổi. Đang tải lại trạng thái."
                    : "Không cập nhật được phiếu. Hãy kiểm tra quyền và kết nối.",
            );
            setRetry((value) => value + 1);
        } finally {
            setBusy(null);
        }
    }
    const servingOnly =
        canServe && !permissions.some((value) => value.endsWith(".process"));
    const columns = servingOnly
        ? [["SAN_SANG", "Sẵn sàng giao"]]
        : [
              ["MOI", "Mới"],
              ["DANG_LAM", "Đang làm"],
              ["SAN_SANG", "Sẵn sàng"],
          ];
    if (!stationKey)
        return <p role="status">Bạn không có quyền xem hàng đợi này.</p>;
    return (
        <section
            className="space-y-4 rounded-xl border bg-white p-4"
            aria-label={servingOnly ? "Món chờ giao" : "Hàng đợi bếp và bar"}
        >
            <header className="flex flex-wrap items-center justify-between gap-3">
                <div>
                    <h2 className="text-xl font-semibold">
                        {servingOnly ? "Món chờ giao" : "Bếp / Bar"}
                    </h2>
                    <p role="status">
                        {connection === "live"
                            ? "Đã kết nối thời gian thực"
                            : connection === "syncing"
                              ? "Đang đồng bộ…"
                              : "Đang kết nối lại…"}
                    </p>
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button
                        variant="outline"
                        onClick={() => setRetry((value) => value + 1)}
                    >
                        Tải lại hàng đợi
                    </Button>
                    <Button
                        variant="outline"
                        onClick={() => void sound.current?.unlock()}
                    >
                        Bật âm thanh
                    </Button>
                    <Button
                        variant="outline"
                        onClick={() => sound.current?.toggleMute()}
                    >
                        {soundState.muted ? "Bỏ tắt tiếng" : "Tắt tiếng"}
                    </Button>
                </div>
            </header>
            {notice && (
                <p role="status" className="rounded bg-emerald-50 p-3">
                    {notice}
                </p>
            )}
            {soundState.blocked && (
                <p role="alert" className="rounded bg-amber-50 p-3">
                    Trình duyệt chưa cho phép âm thanh. Nhấn “Bật âm thanh”;
                    thông báo trên màn hình vẫn hoạt động.
                </p>
            )}
            {error && (
                <p role="alert" className="rounded bg-amber-50 p-3">
                    {error}
                </p>
            )}
            <label>
                Trạm
                <select
                    className="ml-2 min-h-11 rounded border p-2"
                    value={station}
                    onChange={(event) => setStation(event.target.value)}
                >
                    <option value="all">Tất cả trạm được phép</option>
                    {stationKey.includes("BEP") && (
                        <option value="BEP">Bếp</option>
                    )}
                    {stationKey.includes("BAR") && (
                        <option value="BAR">Bar</option>
                    )}
                </select>
            </label>
            <div
                className={`grid gap-4 ${servingOnly ? "" : "lg:grid-cols-3"}`}
            >
                {columns.map(([status, label]) => (
                    <section key={status} aria-label={label}>
                        <h3 className="mb-2 font-semibold">{label}</h3>
                        <ul className="space-y-2">
                            {tickets
                                .filter((ticket) => ticket.status === status)
                                .map((ticket) => {
                                    const action = actionFor(ticket);
                                    return (
                                        <li
                                            key={String(ticket.id)}
                                            className="space-y-2 rounded border p-3"
                                        >
                                            <strong>
                                                Phiếu #{ticket.id} ·{" "}
                                                {ticket.station === "BEP"
                                                    ? "Bếp"
                                                    : "Bar"}
                                            </strong>
                                            <p className="font-semibold">
                                                {ticket.dishName ??
                                                    `Phần món #${ticket.portionId}`}
                                            </p>
                                            <p>
                                                {ticket.variantName}{" "}
                                                {ticket.optionNames?.join(", ")}
                                            </p>
                                            {ticket.note && (
                                                <p>Ghi chú: {ticket.note}</p>
                                            )}
                                            <p>{ticket.orderCode}</p>
                                            <WaitingTimer
                                                since={ticket.createdAt}
                                            />
                                            {action && (
                                                <Button
                                                    disabled={
                                                        busy ===
                                                        Number(ticket.id)
                                                    }
                                                    onClick={() =>
                                                        void transition(ticket)
                                                    }
                                                >
                                                    {busy === Number(ticket.id)
                                                        ? "Đang cập nhật…"
                                                        : action.label}
                                                </Button>
                                            )}
                                        </li>
                                    );
                                })}
                        </ul>
                        {tickets.filter((ticket) => ticket.status === status)
                            .length === 0 && (
                            <p className="p-3 text-sm text-muted-foreground">
                                Chưa có món trong trạng thái này.
                            </p>
                        )}
                    </section>
                ))}
            </div>
        </section>
    );
}
