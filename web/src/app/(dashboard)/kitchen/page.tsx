"use client";

import { useState } from "react";
import {
    ChefHat,
    Coffee,
    Utensils,
    Clock,
    CheckCircle2,
    Bell,
    Play,
    Check,
    AlertTriangle,
    RotateCcw,
    Volume2,
} from "lucide-react";
import {
    useRestaurant,
    type DishStation,
    type OrderItemDetail,
    type RestaurantOrder,
} from "@/lib/store/restaurant-context";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

interface KdsItem extends OrderItemDetail {
    orderId: string;
    tableName: string;
    orderCreatedAt: string;
    isTakeaway: boolean;
}

export default function KitchenPage() {
    const {
        orders,
        startCookingItem,
        finishCookingItem,
        serveItem,
        toggleDishStock,
        menuItems,
    } = useRestaurant();

    const [stationFilter, setStationFilter] = useState<"ALL" | DishStation>(
        "ALL",
    );
    const [audioEnabled, setAudioEnabled] = useState(true);

    // Flatten all items from orders that are confirmed and active
    const activeOrders = orders.filter(
        (o) => o.status === "DANG_PHUC_VU" || o.status === "CHO_THANH_TOAN",
    );

    const allKdsItems: KdsItem[] = [];
    activeOrders.forEach((order) => {
        order.items.forEach((item) => {
            allKdsItems.push({
                ...item,
                orderId: order.id,
                tableName: order.tableName,
                orderCreatedAt: order.createdAt,
                isTakeaway: order.isTakeaway,
            });
        });
    });

    const filteredItems = allKdsItems.filter((item) => {
        if (stationFilter === "ALL") return true;
        return item.station === stationFilter;
    });

    const pendingItems = filteredItems.filter((i) => i.status === "CHO_LAM");
    const cookingItems = filteredItems.filter((i) => i.status === "DANG_LAM");
    const readyItems = filteredItems.filter((i) => i.status === "SAN_SANG");
    const servedCount = filteredItems.filter(
        (i) => i.status === "DA_GIAO",
    ).length;

    const playChime = () => {
        if (!audioEnabled) return;
        try {
            const ctx = new (
                window.AudioContext ||
                (
                    window as unknown as {
                        webkitAudioContext: typeof AudioContext;
                    }
                ).webkitAudioContext
            )();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = "sine";
            osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
            osc.frequency.exponentialRampToValueAtTime(
                880,
                ctx.currentTime + 0.3,
            ); // A5
            gain.gain.setValueAtTime(0.3, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.5);
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start();
            osc.stop(ctx.currentTime + 0.5);
        } catch {
            // AudioContext fallback
        }
    };

    const handleFinishCooking = (orderId: string, itemId: string) => {
        finishCookingItem(orderId, itemId);
        playChime();
    };

    return (
        <div className="min-h-screen bg-gray-900 text-gray-100 flex flex-col space-y-4 p-3 sm:p-5 rounded-2xl">
            {/* KDS Header Bar */}
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-gray-800/90 p-4 rounded-xl border border-gray-700 shadow-md">
                <div className="flex items-center gap-3">
                    <div className="size-10 rounded-xl bg-orange-600 flex items-center justify-center text-white shadow-lg shadow-orange-600/30">
                        <ChefHat size={22} />
                    </div>
                    <div>
                        <h1 className="text-lg font-bold text-white flex items-center gap-2">
                            KDS - Điều phối Bếp & Bar (UC11, UC12, UC15)
                        </h1>
                        <p className="text-xs text-gray-400">
                            Màn hình hiển thị thời gian thực theo trạm chế biến
                            & phát tín hiệu phục vụ
                        </p>
                    </div>
                </div>

                {/* Controls */}
                <div className="flex items-center gap-2">
                    {/* Station filters */}
                    <div className="flex items-center bg-gray-900 p-1 rounded-lg border border-gray-700">
                        <button
                            onClick={() => setStationFilter("ALL")}
                            className={cn(
                                "px-3 py-1.5 text-xs font-semibold rounded-md transition-colors",
                                stationFilter === "ALL"
                                    ? "bg-red-600 text-white shadow"
                                    : "text-gray-400 hover:text-white",
                            )}
                        >
                            Tất cả trạm
                        </button>
                        <button
                            onClick={() => setStationFilter("BEP")}
                            className={cn(
                                "flex items-center gap-1 px-3 py-1.5 text-xs font-semibold rounded-md transition-colors",
                                stationFilter === "BEP"
                                    ? "bg-orange-600 text-white shadow"
                                    : "text-gray-400 hover:text-white",
                            )}
                        >
                            <Utensils size={13} />
                            Trạm Bếp
                        </button>
                        <button
                            onClick={() => setStationFilter("BAR")}
                            className={cn(
                                "flex items-center gap-1 px-3 py-1.5 text-xs font-semibold rounded-md transition-colors",
                                stationFilter === "BAR"
                                    ? "bg-blue-600 text-white shadow"
                                    : "text-gray-400 hover:text-white",
                            )}
                        >
                            <Coffee size={13} />
                            Trạm Bar
                        </button>
                    </div>

                    {/* Sound toggle button (UC32) */}
                    <button
                        onClick={() => {
                            setAudioEnabled(!audioEnabled);
                            playChime();
                            toast.info(
                                audioEnabled
                                    ? "Đã tắt âm báo chuông"
                                    : "Đã bật âm báo chuông KDS",
                            );
                        }}
                        title="Bật/tắt âm báo món xong (UC32)"
                        className={cn(
                            "p-2.5 rounded-lg border text-xs font-medium transition-colors flex items-center gap-1.5",
                            audioEnabled
                                ? "bg-amber-500/20 text-amber-300 border-amber-500/40"
                                : "bg-gray-800 text-gray-400 border-gray-700",
                        )}
                    >
                        <Volume2 size={16} />
                        <span className="hidden sm:inline">
                            {audioEnabled ? "Chuông: BẬT" : "Chuông: TẮT"}
                        </span>
                    </button>
                </div>
            </div>

            {/* 3 Kanban Columns */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 flex-1">
                {/* ── CỘT 1: CHỜ LÀM ────────────────────────────────────────── */}
                <div className="bg-gray-800/60 rounded-xl border border-gray-700/80 flex flex-col overflow-hidden">
                    <div className="bg-amber-950/60 border-b border-amber-800/60 px-4 py-3 flex items-center justify-between">
                        <div className="flex items-center gap-2">
                            <span className="size-2.5 rounded-full bg-amber-400 animate-ping" />
                            <h2 className="text-sm font-bold text-amber-200 uppercase tracking-wider">
                                1. Chờ chế biến
                            </h2>
                        </div>
                        <span className="text-xs font-bold bg-amber-500 text-gray-950 px-2.5 py-0.5 rounded-full">
                            {pendingItems.length}
                        </span>
                    </div>

                    <div className="p-3 space-y-3 overflow-y-auto flex-1 max-h-[calc(100vh-250px)]">
                        {pendingItems.length === 0 ? (
                            <div className="text-center py-12 text-gray-500 text-xs">
                                Không có món chờ chế biến
                            </div>
                        ) : (
                            pendingItems.map((item) => (
                                <div
                                    key={item.id}
                                    className="bg-gray-800 border border-gray-700 hover:border-amber-500/50 rounded-xl p-3.5 shadow transition-all space-y-3"
                                >
                                    <div className="flex items-start justify-between">
                                        <div>
                                            <span className="text-[11px] font-bold px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 border border-amber-500/30">
                                                {item.tableName}
                                            </span>
                                            <span className="text-xs text-gray-400 ml-2 font-mono">
                                                #{item.orderId}
                                            </span>
                                        </div>
                                        <span className="text-[11px] text-gray-400 flex items-center gap-1">
                                            <Clock size={12} />
                                            {item.orderCreatedAt}
                                        </span>
                                    </div>

                                    <div className="flex items-center justify-between">
                                        <span className="text-base font-bold text-white leading-tight">
                                            {item.name}
                                        </span>
                                        <span className="text-lg font-black text-amber-400 bg-amber-950/80 px-2 py-0.5 rounded-md border border-amber-800">
                                            x{item.quantity}
                                        </span>
                                    </div>

                                    {item.note && (
                                        <p className="text-xs text-yellow-300/90 bg-yellow-950/50 p-2 rounded-lg border border-yellow-800/40">
                                            📝 Ghi chú: {item.note}
                                        </p>
                                    )}

                                    <div className="pt-2 border-t border-gray-700/80 flex items-center justify-between">
                                        <span className="text-[11px] text-gray-400 uppercase">
                                            {item.station === "BEP"
                                                ? "🍳 Bếp"
                                                : "🍹 Bar"}
                                        </span>
                                        <button
                                            onClick={() =>
                                                startCookingItem(
                                                    item.orderId,
                                                    item.id,
                                                )
                                            }
                                            className="flex items-center gap-1.5 text-xs font-bold px-3 py-1.5 bg-amber-600 hover:bg-amber-500 text-gray-950 rounded-lg shadow transition-colors"
                                        >
                                            <Play size={13} />
                                            Bắt đầu nấu (UC11)
                                        </button>
                                    </div>
                                </div>
                            ))
                        )}
                    </div>
                </div>

                {/* ── CỘT 2: ĐANG LÀM ────────────────────────────────────────── */}
                <div className="bg-gray-800/60 rounded-xl border border-gray-700/80 flex flex-col overflow-hidden">
                    <div className="bg-blue-950/60 border-b border-blue-800/60 px-4 py-3 flex items-center justify-between">
                        <div className="flex items-center gap-2">
                            <span className="size-2.5 rounded-full bg-blue-400 animate-pulse" />
                            <h2 className="text-sm font-bold text-blue-200 uppercase tracking-wider">
                                2. Đang chế biến
                            </h2>
                        </div>
                        <span className="text-xs font-bold bg-blue-500 text-white px-2.5 py-0.5 rounded-full">
                            {cookingItems.length}
                        </span>
                    </div>

                    <div className="p-3 space-y-3 overflow-y-auto flex-1 max-h-[calc(100vh-250px)]">
                        {cookingItems.length === 0 ? (
                            <div className="text-center py-12 text-gray-500 text-xs">
                                Chưa có món đang chế biến
                            </div>
                        ) : (
                            cookingItems.map((item) => (
                                <div
                                    key={item.id}
                                    className="bg-gray-800 border border-blue-700/60 rounded-xl p-3.5 shadow-lg space-y-3"
                                >
                                    <div className="flex items-start justify-between">
                                        <div>
                                            <span className="text-[11px] font-bold px-2 py-0.5 rounded bg-blue-500/20 text-blue-300 border border-blue-500/30">
                                                {item.tableName}
                                            </span>
                                            <span className="text-xs text-gray-400 ml-2 font-mono">
                                                #{item.orderId}
                                            </span>
                                        </div>
                                        <span className="text-[11px] text-blue-300 flex items-center gap-1 font-medium">
                                            <Clock
                                                size={12}
                                                className="animate-spin"
                                            />
                                            Đang nấu
                                        </span>
                                    </div>

                                    <div className="flex items-center justify-between">
                                        <span className="text-base font-bold text-white leading-tight">
                                            {item.name}
                                        </span>
                                        <span className="text-lg font-black text-blue-300 bg-blue-950/80 px-2 py-0.5 rounded-md border border-blue-800">
                                            x{item.quantity}
                                        </span>
                                    </div>

                                    {item.note && (
                                        <p className="text-xs text-yellow-300/90 bg-yellow-950/50 p-2 rounded-lg border border-yellow-800/40">
                                            📝 {item.note}
                                        </p>
                                    )}

                                    <div className="pt-2 border-t border-gray-700/80 flex items-center justify-between">
                                        <span className="text-[11px] text-gray-400 uppercase">
                                            {item.station === "BEP"
                                                ? "🍳 Bếp"
                                                : "🍹 Bar"}
                                        </span>
                                        <button
                                            onClick={() =>
                                                handleFinishCooking(
                                                    item.orderId,
                                                    item.id,
                                                )
                                            }
                                            className="flex items-center gap-1.5 text-xs font-bold px-3 py-1.5 bg-blue-600 hover:bg-blue-500 text-white rounded-lg shadow-md transition-colors"
                                        >
                                            <Bell size={13} />
                                            Xong món (UC12)
                                        </button>
                                    </div>
                                </div>
                            ))
                        )}
                    </div>
                </div>

                {/* ── CỘT 3: SẴN SÀNG PHỤC VỤ ─────────────────────────────────── */}
                <div className="bg-gray-800/60 rounded-xl border border-gray-700/80 flex flex-col overflow-hidden">
                    <div className="bg-emerald-950/60 border-b border-emerald-800/60 px-4 py-3 flex items-center justify-between">
                        <div className="flex items-center gap-2">
                            <span className="size-2.5 rounded-full bg-emerald-400" />
                            <h2 className="text-sm font-bold text-emerald-200 uppercase tracking-wider">
                                3. Sẵn sàng bưng (Chuông rung)
                            </h2>
                        </div>
                        <span className="text-xs font-bold bg-emerald-500 text-gray-950 px-2.5 py-0.5 rounded-full">
                            {readyItems.length}
                        </span>
                    </div>

                    <div className="p-3 space-y-3 overflow-y-auto flex-1 max-h-[calc(100vh-250px)]">
                        {readyItems.length === 0 ? (
                            <div className="text-center py-12 text-gray-500 text-xs">
                                Tất cả món xong đã được phục vụ
                            </div>
                        ) : (
                            readyItems.map((item) => (
                                <div
                                    key={item.id}
                                    className="bg-gray-800 border border-emerald-500/70 rounded-xl p-3.5 shadow-lg space-y-3 ring-1 ring-emerald-500/30"
                                >
                                    <div className="flex items-start justify-between">
                                        <div>
                                            <span className="text-[11px] font-bold px-2 py-0.5 rounded bg-emerald-500 text-gray-950">
                                                {item.tableName}
                                            </span>
                                            <span className="text-xs text-gray-400 ml-2 font-mono">
                                                #{item.orderId}
                                            </span>
                                        </div>
                                        <span className="text-[11px] text-emerald-300 font-semibold flex items-center gap-1">
                                            <CheckCircle2 size={13} />
                                            Chờ bưng bàn
                                        </span>
                                    </div>

                                    <div className="flex items-center justify-between">
                                        <span className="text-base font-bold text-white leading-tight">
                                            {item.name}
                                        </span>
                                        <span className="text-lg font-black text-emerald-300 bg-emerald-950/80 px-2 py-0.5 rounded-md border border-emerald-800">
                                            x{item.quantity}
                                        </span>
                                    </div>

                                    <div className="pt-2 border-t border-gray-700/80 flex items-center justify-between">
                                        <span className="text-[11px] text-gray-400">
                                            Nhân viên bưng tới bàn
                                        </span>
                                        <button
                                            onClick={() =>
                                                serveItem(item.orderId, item.id)
                                            }
                                            className="flex items-center gap-1.5 text-xs font-bold px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg shadow-md transition-colors"
                                        >
                                            <Check size={14} />
                                            Đã phục vụ (UC15)
                                        </button>
                                    </div>
                                </div>
                            ))
                        )}
                    </div>
                </div>
            </div>
        </div>
    );
}
