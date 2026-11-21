"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Plus, Minus, ArrowRight, User } from "lucide-react";
import type { MenuItem } from "@/lib/store/restaurant-context";
import { publicMenu as menuItems } from "./public-menu";
import { cn } from "@/lib/utils";
import { toast } from "sonner";

export function GuestMenu({ tableCode }: { tableCode: string }) {
    useEffect(() => {
        if (location.hash)
            history.replaceState(
                history.state,
                "",
                location.pathname + location.search,
            );
    }, []);
    const currentTable = { name: tableCode };
    // Cart state: map dishId -> { quantity, note }
    const [cart, setCart] = useState<
        Record<number, { quantity: number; note: string }>
    >({});
    const [activeCategory, setActiveCategory] = useState<string>("ALL");

    const categories = [
        { key: "ALL", label: "Tất cả" },
        { key: "KHAI_VI", label: "Khai vị" },
        { key: "MON_CHINH", label: "Món chính" },
        { key: "DO_UONG", label: "Đồ uống" },
    ];

    const filteredDishes = menuItems.filter((dish) => {
        if (activeCategory === "ALL") return true;
        return dish.category === activeCategory;
    });

    const cartItemsCount = Object.values(cart).reduce(
        (s, it) => s + it.quantity,
        0,
    );
    const cartTotalPrice = Object.entries(cart).reduce(
        (sum, [dishIdStr, item]) => {
            const dish = menuItems.find((d) => d.id === Number(dishIdStr));
            return sum + (dish ? dish.price * item.quantity : 0);
        },
        0,
    );

    const handleAddToCart = (dish: MenuItem) => {
        if (dish.outOfStock) {
            toast.error(`Món "${dish.name}" đã hết ở bếp, không thể gọi!`);
            return;
        }
        setCart((prev) => {
            const current = prev[dish.id];
            const newQty = (current?.quantity || 0) + 1;
            return {
                ...prev,
                [dish.id]: { quantity: newQty, note: current?.note || "" },
            };
        });
    };

    const handleRemoveFromCart = (dishId: number) => {
        setCart((prev) => {
            const current = prev[dishId];
            if (!current) return prev;
            if (current.quantity <= 1) {
                const next = { ...prev };
                delete next[dishId];
                return next;
            }
            return {
                ...prev,
                [dishId]: { ...current, quantity: current.quantity - 1 },
            };
        });
    };

    return (
        <div className="relative mx-auto min-h-dvh w-full max-w-7xl bg-slate-50 pb-[calc(7rem+env(safe-area-inset-bottom))] text-slate-800">
            {/* Top Banner: QR Table Identity */}
            <div className="sticky top-0 z-30 bg-white/95 backdrop-blur border-b border-slate-200 px-4 py-3 flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                    <div className="size-9 rounded-xl bg-red-500 text-white flex items-center justify-center font-black text-sm shadow-md shadow-red-200">
                        R
                    </div>
                    <div>
                        <div className="flex items-center gap-1.5">
                            <span className="font-bold text-slate-900 text-sm">
                                {currentTable.name}
                            </span>
                        </div>
                        <p className="text-[11px] text-slate-400">
                            Khách tự gọi món qua mã QR
                        </p>
                    </div>
                </div>

                <Link
                    href="/login"
                    className="text-xs bg-slate-100 hover:bg-slate-200 text-slate-700 font-semibold px-2.5 py-1.5 rounded-xl transition-colors flex items-center gap-1"
                >
                    <User size={13} />
                    <span>Đăng nhập</span>
                </Link>
            </div>

            {/* Category Pills */}
            <div className="sticky top-[61px] z-20 flex items-center gap-2 overflow-x-auto border-b bg-card px-4 py-2.5 [&>button]:min-h-11 [&>button]:shrink-0">
                {categories.map((cat) => (
                    <button
                        key={cat.key}
                        onClick={() => setActiveCategory(cat.key)}
                        className={cn(
                            "px-3 py-1.5 rounded-full text-xs font-semibold whitespace-nowrap transition-colors",
                            activeCategory === cat.key
                                ? "bg-red-500 text-white shadow-sm"
                                : "bg-slate-100 text-slate-600 hover:bg-slate-200",
                        )}
                    >
                        {cat.label}
                    </button>
                ))}
            </div>

            {/* Menu List */}
            <section
                aria-label="Thực đơn"
                className="grid gap-3 p-4 sm:grid-cols-2 lg:grid-cols-3 lg:gap-5"
            >
                {filteredDishes.map((dish) => {
                    const cartItem = cart[dish.id];
                    const isOutOfStock = dish.outOfStock;

                    return (
                        <article
                            key={dish.id}
                            className={cn(
                                "p-3 rounded-2xl border transition-all flex items-center justify-between gap-3 shadow-xs",
                                isOutOfStock
                                    ? "bg-slate-100/80 border-slate-200 opacity-60"
                                    : "bg-white border-slate-200 hover:border-red-200",
                            )}
                        >
                            {/* Dish info */}
                            <div className="min-w-0 flex-1 space-y-1">
                                <div className="flex items-center gap-1.5">
                                    <h3 className="font-bold text-sm text-slate-900 leading-tight">
                                        {dish.name}
                                    </h3>
                                    {isOutOfStock && (
                                        <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-red-500 text-white">
                                            Đã hết
                                        </span>
                                    )}
                                </div>
                                <p className="text-xs font-semibold text-red-600">
                                    {dish.price.toLocaleString("vi-VN")} đ
                                </p>
                                <span className="text-[10px] text-slate-400 uppercase">
                                    {dish.station === "BEP"
                                        ? "🍳 Trạm Bếp"
                                        : "🍹 Trạm Bar"}
                                </span>

                                {/* Dish Note input if in cart */}
                                {cartItem && (
                                    <div className="pt-1.5">
                                        <input
                                            type="text"
                                            placeholder="Ghi chú (ít cay, không hành...)"
                                            value={cartItem.note}
                                            onChange={(e) =>
                                                setCart((prev) => ({
                                                    ...prev,
                                                    [dish.id]: {
                                                        ...cartItem,
                                                        note: e.target.value,
                                                    },
                                                }))
                                            }
                                            className="w-full text-xs px-2.5 py-1 bg-slate-50 border rounded-lg focus:outline-none focus:ring-1 focus:ring-red-500"
                                        />
                                    </div>
                                )}
                            </div>

                            {/* Add / Quantity Controls */}
                            <div className="shrink-0">
                                {isOutOfStock ? (
                                    <button
                                        disabled
                                        className="px-3 py-1.5 rounded-xl bg-slate-200 text-slate-400 text-xs font-bold cursor-not-allowed"
                                    >
                                        Hết món
                                    </button>
                                ) : cartItem ? (
                                    <div className="flex items-center gap-2 bg-red-50 p-1 rounded-xl border border-red-200">
                                        <button
                                            onClick={() =>
                                                handleRemoveFromCart(dish.id)
                                            }
                                            className="size-7 rounded-lg bg-white text-red-600 font-bold flex items-center justify-center shadow-xs hover:bg-red-100 transition-colors"
                                        >
                                            <Minus size={14} />
                                        </button>
                                        <span className="w-5 text-center font-bold text-sm text-red-700">
                                            {cartItem.quantity}
                                        </span>
                                        <button
                                            onClick={() =>
                                                handleAddToCart(dish)
                                            }
                                            className="size-7 rounded-lg bg-red-500 text-white font-bold flex items-center justify-center shadow-xs hover:bg-red-600 transition-colors"
                                        >
                                            <Plus size={14} />
                                        </button>
                                    </div>
                                ) : (
                                    <button
                                        onClick={() => handleAddToCart(dish)}
                                        className="flex items-center gap-1 bg-red-500 hover:bg-red-600 text-white text-xs font-bold px-3 py-2 rounded-xl shadow-sm transition-colors"
                                    >
                                        <Plus size={14} /> Chọn
                                    </button>
                                )}
                            </div>
                        </article>
                    );
                })}
            </section>

            {/* Floating Bottom Cart Bar */}
            {cartItemsCount > 0 && (
                <div className="pointer-events-none fixed inset-x-0 bottom-0 z-40 mx-auto max-w-7xl px-4 pt-4 pb-[calc(1rem+env(safe-area-inset-bottom))]">
                    <div className="pointer-events-auto bg-slate-900 text-white rounded-2xl p-3.5 shadow-2xl flex items-center justify-between gap-3 border border-slate-700 sm:ml-auto sm:max-w-md animate-in slide-in-from-bottom-3">
                        <div>
                            <div className="flex items-center gap-2">
                                <span className="size-6 rounded-full bg-red-500 text-white text-xs font-bold flex items-center justify-center">
                                    {cartItemsCount}
                                </span>
                                <span className="font-bold text-sm">
                                    {cartTotalPrice.toLocaleString("vi-VN")} đ
                                </span>
                            </div>
                            <p className="text-[11px] text-slate-400">
                                Giỏ tạm · Chưa gửi món
                            </p>
                        </div>

                        <button
                            disabled
                            className="bg-red-500 hover:bg-red-600 text-white text-xs font-bold px-4 py-2.5 rounded-xl shadow-md transition-all flex items-center gap-1.5 active:scale-95"
                        >
                            <span>Gửi đợt gọi món</span>
                            <ArrowRight size={14} />
                        </button>
                    </div>
                </div>
            )}
        </div>
    );
}
