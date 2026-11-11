"use client";
import { useEffect, useMemo, useRef, useState } from "react";
import { Bell, RefreshCw, Volume2, VolumeX, WifiOff } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
    MoneyInput,
    QuantityStepper,
    Receipt,
    StatusBadge,
    WaitingTimer,
} from "@/components/ops/controls";
import { MockOperationsTransport } from "@/lib/realtime/mock";
import {
    OperationsFeed,
    type ConnectionState,
} from "@/lib/realtime/operations";
import {
    NotificationSound,
    createBrowserAudio,
    type SoundState,
} from "@/lib/sound/notification-sound";
import { SalesDayScreen } from "@/features/sales-day/sales-day-screen";
import { type BusinessDay, type SalesDayApi } from "@/features/sales-day/api";
import { createDemoSalesDayApi } from "@/features/sales-day/demo-api";

const initialDay: BusinessDay = {
    id: "demo",
    businessDate: "2026-11-10",
    openingCash: 1000000,
    status: "DANG_BAN",
    openedAt: "2026-11-10T01:00:00Z",
};
export function WeekOneDemo({ startedAt }: { startedAt: string }) {
    const [amount, setAmount] = useState<number | null>(1000000);
    const [quantity, setQuantity] = useState(1);
    const [connection, setConnection] = useState<ConnectionState>("offline");
    const [soundState, setSoundState] = useState<SoundState>({
        muted: false,
        blocked: false,
        unlocked: false,
    });
    const [items, setItems] = useState<string[]>([]);
    const [lastNotice, setLastNotice] = useState("");
    const [scenario, setScenario] = useState("empty");
    const [token, setToken] = useState("");
    const [live, setLive] = useState(false);
    const transport = useRef<MockOperationsTransport<string> | null>(null);
    const sound = useRef<NotificationSound | null>(null);
    const [previousEvent, setPreviousEvent] = useState<{
        eventId: string;
        occurredAt: string;
        payload: string;
    } | null>(null);
    useEffect(() => {
        let storage: Storage | undefined;
        try {
            storage = window.localStorage;
        } catch {
            /* Browser privacy mode. */
        }
        const audio = new NotificationSound(
            createBrowserAudio(),
            storage,
            setSoundState,
        );
        sound.current = audio;
        const mock = new MockOperationsTransport<string>();
        transport.current = mock;
        const feed = new OperationsFeed(
            mock,
            async () => ["Bàn 01 · món đã có trong snapshot"],
            {
                snapshot: setItems,
                event: (event, audible) => {
                    setItems((old) => [...old, event.payload]);
                    if (audible) {
                        setLastNotice(event.payload);
                        void audio.notify();
                    }
                },
                state: setConnection,
                error: () =>
                    setLastNotice("Không tải được trạng thái vận hành"),
            },
        );
        queueMicrotask(() => setSoundState(audio.current));
        void feed.start();
        const unlock = () => {
            void audio.unlock();
        };
        window.addEventListener("pointerdown", unlock, { once: true });
        window.addEventListener("keydown", unlock, { once: true });
        return () => {
            window.removeEventListener("pointerdown", unlock);
            window.removeEventListener("keydown", unlock);
            void feed.stop();
            void audio.dispose();
        };
    }, []);
    const mockApi = useMemo<SalesDayApi>(() => {
        const current: BusinessDay | null =
            scenario === "empty" ||
            scenario === "forbidden" ||
            scenario === "error"
                ? null
                : { ...initialDay, status: scenario as BusinessDay["status"] };
        return createDemoSalesDayApi(current, scenario);
    }, [scenario]);
    const liveApi = useMemo<SalesDayApi>(
        () => ({
            async current(signal) {
                const { createSalesDayApi } =
                    await import("@/features/sales-day/api");
                return createSalesDayApi(() => token).current(signal);
            },
            async open(input, key) {
                const { createSalesDayApi } =
                    await import("@/features/sales-day/api");
                return createSalesDayApi(() => token).open(input, key);
            },
        }),
        [token],
    );
    return (
        <main className="mx-auto max-w-5xl space-y-10 p-4 sm:p-8">
            <header className="flex flex-wrap items-center justify-between gap-3 border-b pb-5">
                <h1 className="text-2xl font-semibold">Vận hành nhà hàng</h1>
                <a href="/sales-day" className="text-sm underline">
                    Ngày bán
                </a>
            </header>
            <section className="space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                    <h2 className="text-lg font-semibold">
                        Thông báo vận hành
                    </h2>
                    <StatusBadge
                        tone={connection === "live" ? "success" : "warning"}
                    >
                        {connection === "live"
                            ? "Đã kết nối"
                            : connection === "syncing"
                              ? "Đang đồng bộ"
                              : "Mất kết nối"}
                    </StatusBadge>
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button
                        aria-label="Bật âm thanh"
                        title="Bật âm thanh"
                        variant="outline"
                        onClick={() => void sound.current?.unlock()}
                    >
                        <Volume2 size={17} />
                    </Button>
                    <Button
                        aria-label={
                            soundState.muted ? "Bỏ tắt tiếng" : "Tắt tiếng"
                        }
                        title={soundState.muted ? "Bỏ tắt tiếng" : "Tắt tiếng"}
                        variant="outline"
                        onClick={() => sound.current?.toggleMute()}
                    >
                        {soundState.muted ? (
                            <VolumeX size={17} />
                        ) : (
                            <Volume2 size={17} />
                        )}
                    </Button>
                    <Button
                        disabled={connection !== "live"}
                        onClick={() => {
                            const event = {
                                eventId: crypto.randomUUID(),
                                occurredAt: new Date(
                                    Date.now() + 1,
                                ).toISOString(),
                                payload: `Bàn 02 · món mới ${items.length}`,
                            };
                            setPreviousEvent(event);
                            transport.current?.emit(event);
                        }}
                    >
                        <Bell size={16} />
                        Món mới
                    </Button>
                    <Button
                        variant="outline"
                        disabled={!previousEvent}
                        onClick={() => {
                            if (previousEvent)
                                transport.current?.emit(previousEvent);
                        }}
                    >
                        Gửi lại sự kiện
                    </Button>
                    <Button
                        variant="outline"
                        onClick={() => transport.current?.disconnect()}
                    >
                        <WifiOff size={16} />
                        Ngắt kết nối
                    </Button>
                    <Button
                        variant="outline"
                        onClick={() => transport.current?.reconnect()}
                    >
                        <RefreshCw size={16} />
                        Kết nối lại
                    </Button>
                </div>
                {soundState.blocked && (
                    <p
                        role="alert"
                        className="border-l-4 border-amber-500 bg-amber-50 p-3 text-sm"
                    >
                        Âm thanh bị chặn. Có thông báo mới trên màn hình.
                    </p>
                )}
                <p
                    role="status"
                    aria-live="polite"
                    className="min-h-6 text-sm font-medium text-emerald-800"
                >
                    {lastNotice}
                </p>
                <ul className="divide-y text-sm">
                    {items.map((item, i) => (
                        <li className="py-3" key={i}>
                            {item}
                        </li>
                    ))}
                </ul>
            </section>
            <section className="grid gap-6 border-t pt-6 sm:grid-cols-2">
                <div className="space-y-4">
                    <h2 className="text-lg font-semibold">
                        Trạng thái & số lượng
                    </h2>
                    <div className="flex flex-wrap gap-2">
                        <StatusBadge>Mới</StatusBadge>
                        <StatusBadge tone="warning">Đang làm</StatusBadge>
                        <StatusBadge tone="success">Sẵn sàng</StatusBadge>
                    </div>
                    <WaitingTimer since={startedAt} />
                    <QuantityStepper value={quantity} onChange={setQuantity} />
                    <label htmlFor="demo-money" className="block text-sm">
                        Số tiền
                    </label>
                    <MoneyInput
                        id="demo-money"
                        value={amount}
                        onChange={setAmount}
                    />
                </div>
                <Receipt
                    number="DEMO-001"
                    lines={[{ name: "Món mẫu", amount: 65000 }]}
                    total={65000}
                />
            </section>
            <section className="space-y-4 border-t pt-6">
                <div className="flex flex-wrap items-center gap-3">
                    <label htmlFor="scenario" className="text-sm">
                        Tình huống
                    </label>
                    <select
                        id="scenario"
                        className="rounded border p-2 text-sm"
                        value={scenario}
                        onChange={(e) => {
                            setScenario(e.target.value);
                            setLive(false);
                        }}
                    >
                        <option value="empty">Chưa mở ngày</option>
                        <option value="DANG_BAN">Đang bán</option>
                        <option value="CHO_CHOT">Chờ chốt</option>
                        <option value="DA_CHOT">Đã chốt</option>
                        <option value="forbidden">Thiếu quyền</option>
                        <option value="error">Lỗi kết nối</option>
                    </select>
                </div>
                <details className="text-sm">
                    <summary className="cursor-pointer">
                        Kết nối API thật
                    </summary>
                    <div className="mt-3 flex flex-wrap gap-2">
                        <input
                            className="min-w-0 flex-1 rounded border p-2"
                            aria-label="Access token tạm thời"
                            type="password"
                            value={token}
                            autoComplete="off"
                            onChange={(e) => {
                                setToken(e.target.value);
                                setLive(false);
                            }}
                        />
                        <Button disabled={!token} onClick={() => setLive(true)}>
                            Kết nối
                        </Button>
                        <Button
                            variant="outline"
                            onClick={() => {
                                setToken("");
                                setLive(false);
                            }}
                        >
                            Ngắt
                        </Button>
                    </div>
                </details>
                <SalesDayScreen
                    key={`${scenario}-${live}`}
                    api={live ? liveApi : mockApi}
                    canManage={scenario !== "forbidden" || live}
                />
            </section>
        </main>
    );
}
