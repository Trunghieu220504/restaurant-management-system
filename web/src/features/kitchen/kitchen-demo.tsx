"use client";
import { useEffect, useRef, useState } from "react";
import { Bell, Volume2, VolumeX, WifiOff, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { WaitingTimer } from "@/components/ops/controls";
import { MockOperationsTransport } from "@/lib/realtime/mock";
import {
    OperationsFeed,
    type ConnectionState,
    type OperationEvent,
} from "@/lib/realtime/operations";
import {
    NotificationSound,
    createBrowserAudio,
    type SoundState,
} from "@/lib/sound/notification-sound";

type Ticket = {
    id: string;
    name: string;
    table: string;
    station: "kitchen" | "bar";
    status: "new" | "working" | "ready";
    createdAt: string;
};
export function KitchenDemo() {
    const [items, setItems] = useState<Ticket[]>([]);
    const [station, setStation] = useState("all");
    const [connection, setConnection] = useState<ConnectionState>("offline");
    const [notice, setNotice] = useState("");
    const [audioState, setAudioState] = useState<SoundState>({
        muted: false,
        blocked: false,
        unlocked: false,
    });
    const transport = useRef<MockOperationsTransport<Ticket> | null>(null);
    const audio = useRef<NotificationSound | null>(null);
    const snapshot = useRef<Ticket[]>([]);
    const last = useRef<OperationEvent<Ticket> | null>(null);
    useEffect(() => {
        const mock = new MockOperationsTransport<Ticket>();
        transport.current = mock;
        let storage: Storage | undefined;
        try {
            storage = localStorage;
        } catch {
            /* Privacy mode. */
        }
        const sound = new NotificationSound(
            createBrowserAudio(),
            storage,
            setAudioState,
        );
        audio.current = sound;
        snapshot.current = [
            {
                id: "initial",
                name: "Cơm gà",
                table: "Bàn 01",
                station: "kitchen",
                status: "new",
                createdAt: new Date().toISOString(),
            },
        ];
        const feed = new OperationsFeed(
            mock,
            async () => snapshot.current.map((item) => ({ ...item })),
            {
                snapshot: (tickets) => {
                    setItems(tickets);
                    setAudioState(sound.current);
                },
                state: setConnection,
                error: () =>
                    setNotice("Không tải được phiếu. Thử kết nối lại."),
                event: (event, audible) => {
                    setItems((previous) => [
                        ...previous.filter(
                            (item) => item.id !== event.payload.id,
                        ),
                        event.payload,
                    ]);
                    if (audible) {
                        setNotice(
                            `${event.payload.table} · ${event.payload.name}`,
                        );
                        void sound.notify();
                    }
                },
            },
        );
        void feed.start();
        return () => {
            void feed.stop();
            void sound.dispose();
        };
    }, []);
    function addTicket() {
        const id = crypto.randomUUID();
        const ticket: Ticket = {
            id,
            name: station === "bar" ? "Trà chanh" : "Cơm gà",
            table: "Bàn 02",
            station: station === "bar" ? "bar" : "kitchen",
            status: "new",
            createdAt: new Date().toISOString(),
        };
        snapshot.current = [...snapshot.current, ticket];
        last.current = {
            eventId: id,
            occurredAt: ticket.createdAt,
            payload: ticket,
        };
        transport.current?.emit(last.current);
    }
    function advance(ticket: Ticket) {
        if (connection !== "live" || ticket.status === "ready") return;
        const next: Ticket = {
            ...ticket,
            status: ticket.status === "new" ? "working" : "ready",
        };
        snapshot.current = snapshot.current.map((item) =>
            item.id === next.id ? next : item,
        );
        setItems((previous) =>
            previous.map((item) => (item.id === next.id ? next : item)),
        );
    }
    return (
        <section className="space-y-5">
            <header className="flex flex-wrap items-center justify-between gap-3">
                <h2 className="text-xl font-semibold">Bếp / Bar</h2>
                <p role="status">
                    {connection === "live"
                        ? "Đã kết nối"
                        : connection === "syncing"
                          ? "Đang đồng bộ"
                          : "Mất kết nối"}
                </p>
            </header>
            <div className="flex flex-wrap items-center gap-2">
                <label htmlFor="station">Trạm</label>
                <select
                    id="station"
                    value={station}
                    className="min-h-11 max-w-full rounded border p-2"
                    onChange={(e) => setStation(e.target.value)}
                >
                    <option value="all">Tất cả</option>
                    <option value="kitchen">Bếp</option>
                    <option value="bar">Bar</option>
                </select>
                <Button
                    variant="outline"
                    onClick={() => void audio.current?.unlock()}
                >
                    <Volume2 size={16} /> Bật âm thanh
                </Button>
                <Button
                    size="icon"
                    variant="outline"
                    aria-label={audioState.muted ? "Bỏ tắt tiếng" : "Tắt tiếng"}
                    title={audioState.muted ? "Bỏ tắt tiếng" : "Tắt tiếng"}
                    onClick={() => audio.current?.toggleMute()}
                >
                    {audioState.muted ? (
                        <VolumeX size={16} />
                    ) : (
                        <Volume2 size={16} />
                    )}
                </Button>
                <Button disabled={connection !== "live"} onClick={addTicket}>
                    <Bell size={16} /> Phiếu mới
                </Button>
                <Button
                    variant="outline"
                    onClick={() => {
                        if (last.current) transport.current?.emit(last.current);
                    }}
                >
                    Gửi lại sự kiện
                </Button>
                <Button
                    variant="outline"
                    onClick={() => transport.current?.disconnect()}
                >
                    <WifiOff size={16} /> Ngắt kết nối
                </Button>
                <Button
                    variant="outline"
                    onClick={() => transport.current?.reconnect()}
                >
                    <RefreshCw size={16} /> Kết nối lại
                </Button>
            </div>
            {audioState.blocked && (
                <p
                    role="alert"
                    className="border-l-4 border-amber-500 bg-amber-50 p-3"
                >
                    Âm thanh chưa được cho phép. Vẫn có thông báo trên màn hình.
                </p>
            )}
            <p
                aria-live="polite"
                className="min-h-6 font-medium text-emerald-800"
            >
                {notice}
            </p>
            <div className="grid gap-5 lg:grid-cols-3">
                {(
                    [
                        ["new", "Mới"],
                        ["working", "Đang làm"],
                        ["ready", "Sẵn sàng"],
                    ] as const
                ).map(([status, label]) => (
                    <section
                        key={status}
                        aria-label={label}
                        className="min-w-0 border-t-2 border-emerald-600 pt-3"
                    >
                        <h3 className="mb-3 font-semibold">
                            {label} (
                            {
                                items.filter(
                                    (item) =>
                                        item.status === status &&
                                        (station === "all" ||
                                            item.station === station),
                                ).length
                            }
                            )
                        </h3>
                        <ul className="space-y-3">
                            {items
                                .filter(
                                    (item) =>
                                        item.status === status &&
                                        (station === "all" ||
                                            item.station === station),
                                )
                                .map((ticket) => (
                                    <li
                                        key={ticket.id}
                                        className="space-y-3 rounded border bg-white p-4"
                                    >
                                        <p className="font-semibold">
                                            {ticket.name} · {ticket.table}
                                        </p>
                                        <p className="text-sm">
                                            {ticket.station === "bar"
                                                ? "Bar"
                                                : "Bếp"}
                                        </p>
                                        <WaitingTimer
                                            since={ticket.createdAt}
                                        />
                                        {status !== "ready" && (
                                            <Button
                                                disabled={connection !== "live"}
                                                onClick={() => advance(ticket)}
                                            >
                                                {status === "new"
                                                    ? "Bắt đầu"
                                                    : "Hoàn thành"}
                                            </Button>
                                        )}
                                    </li>
                                ))}
                        </ul>
                    </section>
                ))}
            </div>
        </section>
    );
}
