"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Plus, Minus, User } from "lucide-react";
import { MenuPhoto } from "@/features/catalog/menu-photo";
import { GuestAccessStatus } from "./guest-access";
import { GuestCheckout } from "./guest-checkout";
import { cn } from "@/lib/utils";

import type { components } from "@/lib/api/generated";
type PublicMenu = components["schemas"]["PublicMenu"];
export function GuestMenu({ tableCode }: { tableCode: string }) {
    const [sending, setSending] = useState(false);
    const [token, setToken] = useState<string | null>(null);
    const [selected, setSelected] = useState<Record<string, string[]>>({});
    const [menu, setMenu] = useState<PublicMenu | null>(null),
        [error, fail] = useState(""),
        [attempt, retry] = useState(0);
    useEffect(() => {
        const c = new AbortController();
        async function load() {
            try {
                const res = await fetch("/api/public/menu", {
                    signal: c.signal,
                    credentials: "omit",
                    cache: "no-store",
                });
                if (res.status === 429)
                    throw Error(
                        "Quá nhiều yêu cầu. Vui lòng thử lại sau " +
                            (res.headers.get("Retry-After") ?? 60) +
                            " giây.",
                    );
                if (!res.ok)
                    throw Error("Không thể tải thực đơn. Vui lòng thử lại.");
                const data: PublicMenu = await res.json();
                if (!c.signal.aborted) {
                    setMenu(data);
                    fail("");
                }
            } catch (e) {
                if (!c.signal.aborted)
                    fail(
                        e instanceof Error
                            ? e.message
                            : "Không thể tải thực đơn.",
                    );
            }
        }
        void load();
        return () => c.abort();
    }, [attempt]);
    const menuItems =
        menu?.groups.flatMap((g) =>
            g.items.flatMap((i) =>
                i.variants.map((v) => ({
                    id: v.id,
                    name: i.name + " · " + v.name,
                    category: g.id,
                    price: Number(v.price),
                    imageObjectKey: i.imageObjectKey,
                    optionGroups: i.optionGroups,
                })),
            ),
        ) ?? [];
    const [cart, setCart] = useState<
        Record<string, { quantity: number; note: string }>
    >({});
    const [activeCategory, setActiveCategory] = useState("ALL");
    const categories = [
        { key: "ALL", label: "Tất cả" },
        ...(menu?.groups.map((g) => ({ key: g.id, label: g.name })) ?? []),
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
            const dish = menuItems.find((d) => d.id === dishIdStr);
            return (
                sum +
                (dish
                    ? (dish.price +
                          dish.optionGroups
                              .flatMap((g) => g.options)
                              .filter((o) => selected[dish.id]?.includes(o.id))
                              .reduce(
                                  (sum, o) => sum + Number(o.extraPrice),
                                  0,
                              )) *
                      item.quantity
                    : 0)
            );
        },
        0,
    );

    const handleAddToCart = (dish: (typeof menuItems)[number]) => {
        setCart((prev) => {
            const current = prev[dish.id];
            const newQty = (current?.quantity || 0) + 1;
            return {
                ...prev,
                [dish.id]: { quantity: newQty, note: current?.note || "" },
            };
        });
    };

    const handleRemoveFromCart = (dishId: string) => {
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
        <main className="relative mx-auto min-h-dvh w-full max-w-7xl bg-slate-50 pb-[calc(7rem+env(safe-area-inset-bottom))] text-slate-800">
            <h1 className="sr-only">Thực đơn tại bàn {tableCode}</h1>
            {/* Top Banner: QR Table Identity */}
            <div className="sticky top-0 z-30 bg-white/95 backdrop-blur border-b border-slate-200 px-4 py-3 flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                    <div className="size-9 rounded-xl bg-red-700 text-white flex items-center justify-center font-black text-sm shadow-md shadow-red-200">
                        R
                    </div>
                    <div>
                        <div className="flex items-center gap-1.5">
                            <span className="font-bold text-slate-900 text-sm">
                                {tableCode}
                            </span>
                        </div>
                        <p className="text-[11px] text-slate-600">
                            Thực đơn tại bàn
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

            <div className="px-4 pt-3">
                <GuestAccessStatus tableCode={tableCode} onToken={setToken} />
                {error && (
                    <p role="alert">
                        {error}{" "}
                        <button onClick={() => retry((n) => n + 1)}>
                            Tải lại
                        </button>
                    </p>
                )}
                {!menu && !error && <p>Đang tải thực đơn…</p>}
                {menu && menuItems.length === 0 && (
                    <p>
                        {menu.businessDayId
                            ? "Chưa có món đang bán."
                            : "Chưa mở ngày bán."}
                    </p>
                )}
            </div>
            {/* Category Pills */}
            <div className="sticky top-[61px] z-20 flex items-center gap-2 overflow-x-auto border-b bg-card px-4 py-2.5 [&>button]:min-h-11 [&>button]:shrink-0">
                {categories.map((cat) => (
                    <button
                        key={cat.key}
                        aria-pressed={activeCategory === cat.key}
                        onClick={() => setActiveCategory(cat.key)}
                        className={cn(
                            "px-3 py-1.5 rounded-full text-xs font-semibold whitespace-nowrap transition-colors",
                            activeCategory === cat.key
                                ? "bg-red-700 text-white shadow-sm"
                                : "bg-slate-100 text-slate-600 hover:bg-slate-200",
                        )}
                    >
                        {cat.label}
                    </button>
                ))}
            </div>

            {/* Menu List */}
            <section aria-label="Thực đơn">
                <fieldset
                    disabled={sending}
                    aria-label="Thực đơn"
                    className="grid gap-3 p-4 sm:grid-cols-2 lg:grid-cols-3 lg:gap-5"
                >
                    {filteredDishes.map((dish) => {
                        const cartItem = cart[dish.id];

                        return (
                            <article
                                key={dish.id}
                                className="p-3 rounded-2xl border bg-white border-slate-200 flex items-center justify-between gap-3 shadow-xs"
                            >
                                <MenuPhoto objectKey={dish.imageObjectKey} />
                                <div className="min-w-0 flex-1 space-y-1">
                                    <div className="flex items-center gap-1.5">
                                        <h2 className="font-bold text-sm text-slate-900 leading-tight">
                                            {dish.name}
                                        </h2>
                                    </div>
                                    <p className="text-xs font-semibold text-red-600">
                                        {dish.price.toLocaleString("vi-VN")} đ
                                    </p>
                                    {dish.optionGroups.map((g) => (
                                        <details key={g.id}>
                                            <summary>
                                                {g.name} (chọn {g.min}–{g.max})
                                            </summary>
                                            {g.options.map((o) => (
                                                <label
                                                    key={o.id}
                                                    className="block"
                                                >
                                                    <input
                                                        type="checkbox"
                                                        checked={
                                                            !!selected[
                                                                dish.id
                                                            ]?.includes(o.id)
                                                        }
                                                        onChange={(e) => {
                                                            const checked =
                                                                e.target
                                                                    .checked;
                                                            setSelected(
                                                                (prev) => ({
                                                                    ...prev,
                                                                    [dish.id]:
                                                                        checked
                                                                            ? [
                                                                                  ...(prev[
                                                                                      dish
                                                                                          .id
                                                                                  ] ??
                                                                                      []),
                                                                                  o.id,
                                                                              ]
                                                                            : (
                                                                                  prev[
                                                                                      dish
                                                                                          .id
                                                                                  ] ??
                                                                                  []
                                                                              ).filter(
                                                                                  (
                                                                                      id,
                                                                                  ) =>
                                                                                      id !==
                                                                                      o.id,
                                                                              ),
                                                                }),
                                                            );
                                                        }}
                                                    />
                                                    {o.name} ·{" "}
                                                    {Number(
                                                        o.extraPrice,
                                                    ).toLocaleString(
                                                        "vi-VN",
                                                    )}{" "}
                                                    đ
                                                </label>
                                            ))}
                                        </details>
                                    ))}

                                    {/* Dish Note input if in cart */}
                                    {cartItem && (
                                        <div className="pt-1.5">
                                            <input
                                                type="text"
                                                aria-label={
                                                    "Ghi chú cho " + dish.name
                                                }
                                                placeholder="Ghi chú (ít cay, không hành...)"
                                                value={cartItem.note}
                                                onChange={(e) =>
                                                    setCart((prev) => ({
                                                        ...prev,
                                                        [dish.id]: {
                                                            ...cartItem,
                                                            note: e.target
                                                                .value,
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
                                    {cartItem ? (
                                        <div className="flex items-center gap-2 bg-red-50 p-1 rounded-xl border border-red-200">
                                            <button
                                                onClick={() =>
                                                    handleRemoveFromCart(
                                                        dish.id,
                                                    )
                                                }
                                                aria-label={
                                                    "Giảm số lượng " + dish.name
                                                }
                                                className="size-10 rounded-lg bg-white text-red-600 font-bold flex items-center justify-center shadow-xs hover:bg-red-100 transition-colors"
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
                                                aria-label={
                                                    "Tăng số lượng " + dish.name
                                                }
                                                className="size-10 rounded-lg bg-red-700 text-white font-bold flex items-center justify-center shadow-xs hover:bg-red-600 transition-colors"
                                            >
                                                <Plus size={14} />
                                            </button>
                                        </div>
                                    ) : (
                                        <button
                                            onClick={() =>
                                                handleAddToCart(dish)
                                            }
                                            className="flex items-center gap-1 bg-red-700 hover:bg-red-600 text-white text-xs font-bold px-3 py-2 rounded-xl shadow-sm transition-colors"
                                        >
                                            <Plus size={14} /> Chọn
                                        </button>
                                    )}
                                </div>
                            </article>
                        );
                    })}
                </fieldset>
            </section>

            <GuestCheckout
                onBusy={setSending}
                token={token}
                count={cartItemsCount}
                total={cartTotalPrice}
                lines={Object.entries(cart).map(([variantId, line]) => ({
                    variantId,
                    ...line,
                    optionIds: selected[variantId] ?? [],
                }))}
                valid={Object.keys(cart).every((id) =>
                    menuItems.some(
                        (d) =>
                            d.id === id &&
                            d.optionGroups.every((g) => {
                                const n = g.options.filter((o) =>
                                    selected[id]?.includes(o.id),
                                ).length;
                                return n >= Number(g.min) && n <= Number(g.max);
                            }),
                    ),
                )}
                onSent={() => {
                    setCart({});
                    setSelected({});
                }}
                onSoldOut={() => retry((n) => n + 1)}
            />
            {Object.keys(cart)
                .filter((id) => !menuItems.some((d) => d.id === id))
                .map((id) => (
                    <p role="alert" key={id}>
                        Một món trong giỏ đã ngừng bán.{" "}
                        <button
                            onClick={() =>
                                setCart((prev) => {
                                    const next = { ...prev };
                                    delete next[id];
                                    return next;
                                })
                            }
                        >
                            Bỏ món hết
                        </button>
                    </p>
                ))}
        </main>
    );
}
