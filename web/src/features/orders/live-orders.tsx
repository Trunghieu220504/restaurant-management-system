"use client";
import { useMemo, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { usePermissions } from "@/components/permission-provider";
import { useAuth } from "@/components/auth/auth-provider";
import { Button } from "@/components/ui/button";
import { catalogApi, formatPrice } from "@/features/catalog/api";
import { BillingLive } from "@/features/billing/billing-live";
import { KitchenLive } from "@/features/kitchen/kitchen-live";
import { LiveQueryScope } from "@/lib/realtime/live-query";
import { LivePortions } from "./live-portions";
import { MenuPhoto } from "@/features/catalog/menu-photo";
import {
    useAvailability,
    unavailableItem,
} from "@/features/catalog/availability";
import { salesRequest, type LiveOrder } from "./live-api";

type SessionTable = {
    id: number;
    name: string;
    session: { id: number } | null;
};
type Day = { id: string; status: string; businessDate: string };
const labels: Record<string, string> = {
    CHO_XAC_NHAN: "Chờ xác nhận",
    DA_XAC_NHAN: "Đã xác nhận",
    DA_TU_CHOI: "Đã từ chối",
    DA_HUY: "Đã hủy",
};
export function LiveOrders() {
    const { user } = useAuth();
    return user ? (
        <LiveQueryScope key={user.id}>
            <OrdersWorkspace />
        </LiveQueryScope>
    ) : null;
}
function OrdersWorkspace() {
    const { can } = usePermissions();
    const { user } = useAuth();
    const queryClient = useQueryClient();
    const allowed = can("sales.orders.manage");
    const availability = useAvailability(allowed);
    const [page, setPage] = useState(0);
    const orders = useQuery({
        queryKey: ["live-orders", user?.id, page],
        queryFn: () =>
            salesRequest<{ items: LiveOrder[]; total: number }>(
                `/api/sales/orders/?page=${page}&pageSize=50`,
            ),
        enabled: allowed,
        refetchInterval: 3000,
    });
    const day = useQuery({
        queryKey: ["live-day", user?.id],
        queryFn: () => salesRequest<Day | null>("/api/salesday/current"),
        enabled: allowed,
        refetchInterval: 5000,
    });
    const board = useQuery({
        queryKey: ["live-board", user?.id],
        queryFn: () =>
            salesRequest<{ tables: SessionTable[] }[]>("/api/seating/board"),
        enabled: allowed,
        refetchInterval: 5000,
    });
    const menu = useQuery({
        queryKey: ["live-menu", user?.id],
        queryFn: () => catalogApi.load(),
        enabled: allowed,
        refetchInterval: 5000,
    });
    const params = useSearchParams();
    const [selected, select] = useState<number | null>(() => {
        const value = Number(params.get("orderId"));
        return Number.isSafeInteger(value) && value > 0 ? value : null;
    });
    const order = orders.data?.items.find(
        (item) => Number(item.id) === selected,
    );
    const [destination, setDestination] = useState("takeaway");
    const [customer, setCustomer] = useState("");
    const [itemId, setItem] = useState("");
    const [variantId, setVariant] = useState("");
    const [options, setOptions] = useState<string[]>([]);
    const [quantity, setQuantity] = useState(1);
    const [note, setNote] = useState("");
    const [reason, setReason] = useState("");
    const [busy, setBusy] = useState(false);
    const [message, setMessage] = useState("");
    const [error, setError] = useState("");
    const attempt = useRef<{ fingerprint: string; key: string } | null>(null);
    const item = menu.data?.items.find((value) => value.id === itemId);
    const variant =
        item?.variants.find((value) => value.id === variantId) ??
        item?.variants.find((value) => value.active);
    const permissionKey = [
        "fulfillment.portions.serve",
        "fulfillment.kitchen.process",
        "fulfillment.bar.process",
    ]
        .filter(can)
        .join(",");
    const permissions = useMemo(
        () => (permissionKey ? permissionKey.split(",") : []),
        [permissionKey],
    );
    const tables =
        board.data
            ?.flatMap((area) => area.tables)
            .filter((table) => table.session) ?? [];
    async function update(action: () => Promise<unknown>, success: string) {
        if (busy) return;
        setBusy(true);
        setError("");
        setMessage("");
        try {
            await action();
            setMessage(success);
        } catch (cause) {
            setError(
                cause instanceof Error
                    ? cause.message
                    : "Không thể xử lý yêu cầu.",
            );
        } finally {
            await queryClient.invalidateQueries({ queryKey: ["live-orders"] });
            setBusy(false);
        }
    }
    if (!allowed)
        return <p role="status">Bạn không có quyền quản lý đơn hàng.</p>;
    return (
        <div className="space-y-6">
            <header className="flex flex-wrap items-center justify-between gap-3">
                <h1 className="text-2xl font-semibold">
                    Gọi món, phục vụ và thu tiền
                </h1>
                <Link href="/tables" className="underline">
                    Mở phiên / chuyển bàn
                </Link>
            </header>
            {error && (
                <p
                    role="alert"
                    className="rounded border border-red-300 bg-red-50 p-3"
                >
                    {error}
                </p>
            )}
            {message && (
                <p role="status" className="rounded bg-emerald-50 p-3">
                    {message}
                </p>
            )}
            {(orders.isError ||
                day.isError ||
                board.isError ||
                menu.isError) && (
                <p role="alert">
                    Không tải được dữ liệu.{" "}
                    <Button
                        variant="outline"
                        onClick={() => void queryClient.invalidateQueries()}
                    >
                        Thử lại
                    </Button>
                </p>
            )}
            {day.data?.status !== "DANG_BAN" && (
                <p role="status">
                    Cần mở ngày bán trước khi gọi món.{" "}
                    <Link className="underline" href="/sales-day">
                        Đến ngày bán
                    </Link>
                </p>
            )}
            <section
                aria-label="Thực đơn hôm nay"
                className="rounded-xl border bg-white p-4"
            >
                <h2 className="mb-3 text-lg font-semibold">Thực đơn hôm nay</h2>
                <div className="flex flex-wrap gap-3">
                    {menu.data?.items
                        .filter((value) => value.active)
                        .map((value) => {
                            const sold = availability.data
                                ? unavailableItem(
                                      availability.data,
                                      menu.data!,
                                      value,
                                  )
                                : false;
                            return (
                                <button
                                    key={value.id}
                                    disabled={sold}
                                    className="w-36 rounded border p-2 text-left disabled:opacity-50"
                                    onClick={() => {
                                        setItem(value.id);
                                        setVariant("");
                                        setOptions([]);
                                    }}
                                >
                                    <MenuPhoto
                                        objectKey={value.imageObjectKey}
                                    />
                                    <span>
                                        {value.name}
                                        {sold ? " · Hết món" : ""}
                                    </span>
                                </button>
                            );
                        })}
                </div>
            </section>
            <div className="grid gap-6 xl:grid-cols-[minmax(280px,1fr)_minmax(0,2fr)]">
                <section className="space-y-4 rounded-xl border bg-white p-4">
                    <h2 className="text-lg font-semibold">Đơn đang mở</h2>
                    {orders.isLoading && <p role="status">Đang tải đơn…</p>}
                    {!orders.isLoading && orders.data?.items.length === 0 && (
                        <p>Chưa có đơn mở.</p>
                    )}
                    <ul className="space-y-2">
                        {orders.data?.items.map((row) => (
                            <li key={row.id}>
                                <button
                                    className={`w-full rounded border p-3 text-left ${selected === Number(row.id) ? "border-emerald-600 bg-emerald-50" : ""}`}
                                    onClick={() => select(Number(row.id))}
                                >
                                    <strong>{row.code}</strong>
                                    <span className="block text-sm">
                                        {row.tableLabel ??
                                            row.customerName ??
                                            "Mang về"}
                                    </span>
                                </button>
                            </li>
                        ))}
                    </ul>
                    <div className="flex gap-2">
                        <Button
                            variant="outline"
                            disabled={page === 0}
                            onClick={() => setPage((value) => value - 1)}
                        >
                            Trước
                        </Button>
                        <Button
                            variant="outline"
                            disabled={
                                (page + 1) * 50 >= (orders.data?.total ?? 0)
                            }
                            onClick={() => setPage((value) => value + 1)}
                        >
                            Sau
                        </Button>
                    </div>
                    <form
                        className="grid gap-3 border-t pt-4"
                        onSubmit={(event) => {
                            event.preventDefault();
                            if (!day.data) return;
                            void update(async () => {
                                const created = await salesRequest<{
                                    id: number;
                                }>("/api/sales/orders/", {
                                    type:
                                        destination === "takeaway"
                                            ? "MANG_DI"
                                            : "TAI_BAN",
                                    sessionId:
                                        destination === "takeaway"
                                            ? null
                                            : Number(destination),
                                    businessDayId:
                                        destination === "takeaway"
                                            ? day.data!.id
                                            : null,
                                    customerName:
                                        destination === "takeaway"
                                            ? customer.trim() || null
                                            : null,
                                });
                                select(Number(created.id));
                                setPage(0);
                            }, "Đã tạo đơn. Chọn món để gửi đợt đầu tiên.");
                        }}
                    >
                        <h3 className="font-semibold">Tạo đơn</h3>
                        <label>
                            Phục vụ
                            <select
                                aria-label="Phục vụ"
                                className="mt-1 min-h-11 w-full rounded border p-2"
                                value={destination}
                                onChange={(event) =>
                                    setDestination(event.target.value)
                                }
                            >
                                <option value="takeaway">Mang về</option>
                                {tables.map((table) => (
                                    <option
                                        key={table.id}
                                        value={String(table.session!.id)}
                                    >
                                        {table.name}
                                    </option>
                                ))}
                            </select>
                        </label>
                        {destination === "takeaway" && (
                            <label>
                                Tên khách
                                <input
                                    className="mt-1 min-h-11 w-full rounded border p-2"
                                    value={customer}
                                    onChange={(event) =>
                                        setCustomer(event.target.value)
                                    }
                                    maxLength={150}
                                />
                            </label>
                        )}
                        <Button
                            disabled={busy || day.data?.status !== "DANG_BAN"}
                        >
                            Tạo đơn mới
                        </Button>
                    </form>
                </section>
                <div className="space-y-6">
                    {order ? (
                        <>
                            <section className="space-y-4 rounded-xl border bg-white p-4">
                                <h2 className="text-xl font-semibold">
                                    {order.code} ·{" "}
                                    {order.tableLabel ??
                                        order.customerName ??
                                        "Mang về"}
                                </h2>
                                <nav
                                    className="flex flex-wrap gap-4"
                                    aria-label="Xử lý đơn"
                                >
                                    {can("fulfillment.incidents.report") && (
                                        <Link
                                            className="underline"
                                            href={`/orders?view=incidents&orderId=${order.id}`}
                                        >
                                            Báo và xử lý sự cố món
                                        </Link>
                                    )}
                                    {["request", "approve", "pay"].some(
                                        (action) =>
                                            can("billing.refunds." + action),
                                    ) && (
                                        <Link
                                            className="underline"
                                            href={`/orders?view=refunds&orderId=${order.id}`}
                                        >
                                            Hoàn tiền đơn này
                                        </Link>
                                    )}
                                </nav>
                                <form
                                    className="grid gap-3 sm:grid-cols-2"
                                    onSubmit={(event) => {
                                        event.preventDefault();
                                        if (!item || !variant) return;
                                        const body = {
                                            source: "NHAN_VIEN",
                                            lines: Array.from(
                                                { length: quantity },
                                                () => ({
                                                    menuItemId: item.id,
                                                    variantId: variant.id,
                                                    optionIds: options,
                                                    note: note.trim() || null,
                                                }),
                                            ),
                                        };
                                        const fingerprint = JSON.stringify([
                                            order.id,
                                            body,
                                        ]);
                                        if (
                                            attempt.current?.fingerprint !==
                                            fingerprint
                                        )
                                            attempt.current = {
                                                fingerprint,
                                                key: crypto.randomUUID(),
                                            };
                                        void update(async () => {
                                            await salesRequest(
                                                `/api/sales/orders/${order.id}/batches`,
                                                body,
                                                attempt.current!.key,
                                            );
                                            attempt.current = null;
                                            setNote("");
                                        }, "Đợt món đã gửi lên máy chủ, đang chờ xác nhận.");
                                    }}
                                >
                                    <label>
                                        Món
                                        <select
                                            required
                                            aria-label="Món"
                                            className="mt-1 min-h-11 w-full rounded border p-2"
                                            value={itemId}
                                            onChange={(event) => {
                                                setItem(event.target.value);
                                                setVariant("");
                                                setOptions([]);
                                            }}
                                        >
                                            <option value="">Chọn món</option>
                                            {menu.data?.items
                                                .filter((value) => value.active)
                                                .map((value) => (
                                                    <option
                                                        key={value.id}
                                                        value={value.id}
                                                    >
                                                        {value.name}
                                                    </option>
                                                ))}
                                        </select>
                                    </label>
                                    <label>
                                        Size / biến thể
                                        <select
                                            aria-label="Size / biến thể"
                                            className="mt-1 min-h-11 w-full rounded border p-2"
                                            value={variant?.id ?? ""}
                                            onChange={(event) =>
                                                setVariant(event.target.value)
                                            }
                                        >
                                            {item?.variants
                                                .filter((value) => value.active)
                                                .map((value) => (
                                                    <option
                                                        key={value.id}
                                                        value={value.id}
                                                    >
                                                        {value.name} ·{" "}
                                                        {formatPrice(
                                                            value.price,
                                                        )}
                                                    </option>
                                                ))}
                                        </select>
                                    </label>
                                    {item?.optionGroups.map((assignment) => {
                                        const group =
                                            menu.data?.optionGroups.find(
                                                (value) =>
                                                    value.id ===
                                                    assignment.optionGroupId,
                                            );
                                        return (
                                            group && (
                                                <fieldset
                                                    key={group.id}
                                                    className="rounded border p-2"
                                                >
                                                    <legend>
                                                        {group.name} (
                                                        {
                                                            assignment.minSelections
                                                        }
                                                        –
                                                        {
                                                            assignment.maxSelections
                                                        }
                                                        )
                                                    </legend>
                                                    {group.options
                                                        .filter(
                                                            (value) =>
                                                                value.active,
                                                        )
                                                        .map((option) => (
                                                            <label
                                                                key={option.id}
                                                                className="mr-3 inline-flex items-center gap-2"
                                                            >
                                                                <input
                                                                    type="checkbox"
                                                                    checked={options.includes(
                                                                        option.id,
                                                                    )}
                                                                    onChange={(
                                                                        event,
                                                                    ) =>
                                                                        setOptions(
                                                                            (
                                                                                current,
                                                                            ) =>
                                                                                event
                                                                                    .target
                                                                                    .checked
                                                                                    ? [
                                                                                          ...current,
                                                                                          option.id,
                                                                                      ]
                                                                                    : current.filter(
                                                                                          (
                                                                                              id,
                                                                                          ) =>
                                                                                              id !==
                                                                                              option.id,
                                                                                      ),
                                                                        )
                                                                    }
                                                                />
                                                                {option.name} +
                                                                {formatPrice(
                                                                    option.extraPrice,
                                                                )}
                                                            </label>
                                                        ))}
                                                </fieldset>
                                            )
                                        );
                                    })}
                                    <label>
                                        Số phần
                                        <input
                                            aria-label="Số phần"
                                            type="number"
                                            min={1}
                                            max={20}
                                            required
                                            className="mt-1 min-h-11 w-full rounded border p-2"
                                            value={quantity}
                                            onChange={(event) =>
                                                setQuantity(
                                                    Number(event.target.value),
                                                )
                                            }
                                        />
                                    </label>
                                    <label>
                                        Ghi chú
                                        <input
                                            aria-label="Ghi chú"
                                            className="mt-1 min-h-11 w-full rounded border p-2"
                                            value={note}
                                            onChange={(event) =>
                                                setNote(event.target.value)
                                            }
                                            maxLength={500}
                                        />
                                    </label>
                                    <Button
                                        disabled={
                                            busy ||
                                            !item ||
                                            !variant ||
                                            !Number.isInteger(quantity) ||
                                            quantity < 1 ||
                                            quantity > 20
                                        }
                                    >
                                        Gửi đợt món
                                    </Button>
                                </form>
                                <label className="block">
                                    Lý do từ chối / hủy
                                    <input
                                        className="mt-1 min-h-11 w-full rounded border p-2"
                                        value={reason}
                                        onChange={(event) =>
                                            setReason(event.target.value)
                                        }
                                    />
                                </label>
                                {order.batches.map((batch) => (
                                    <article
                                        key={batch.id}
                                        className="space-y-2 border-t pt-3"
                                    >
                                        <h3 className="font-semibold">
                                            Đợt {batch.number} ·{" "}
                                            {labels[batch.status] ??
                                                batch.status}
                                        </h3>
                                        <ul>
                                            {batch.portions.map((portion) => (
                                                <li key={portion.id}>
                                                    {portion.menuItemName} ·{" "}
                                                    {portion.variantName}
                                                    {portion.optionNames.length
                                                        ? " · " +
                                                          portion.optionNames.join(
                                                              ", ",
                                                          )
                                                        : ""}{" "}
                                                    ·{" "}
                                                    {formatPrice(
                                                        Number(
                                                            portion.unitPrice,
                                                        ),
                                                    )}
                                                </li>
                                            ))}
                                        </ul>
                                        <p>
                                            Tổng:{" "}
                                            {formatPrice(Number(batch.total))}
                                        </p>
                                        {batch.status === "CHO_XAC_NHAN" && (
                                            <div className="flex flex-wrap gap-2">
                                                {can(
                                                    "sales.batches.confirm",
                                                ) && (
                                                    <>
                                                        <Button
                                                            disabled={busy}
                                                            onClick={() =>
                                                                void update(
                                                                    () =>
                                                                        salesRequest(
                                                                            `/api/sales/orders/${order.id}/batches/${batch.number}/confirm`,
                                                                            {},
                                                                        ),
                                                                    "Đã xác nhận và chuyển món đến bếp/bar.",
                                                                )
                                                            }
                                                        >
                                                            Xác nhận đợt{" "}
                                                            {batch.number}
                                                        </Button>
                                                        <Button
                                                            variant="outline"
                                                            disabled={
                                                                busy ||
                                                                !reason.trim()
                                                            }
                                                            onClick={() =>
                                                                void update(
                                                                    () =>
                                                                        salesRequest(
                                                                            `/api/sales/orders/${order.id}/batches/${batch.number}/reject`,
                                                                            {
                                                                                reason: reason.trim(),
                                                                            },
                                                                        ),
                                                                    "Đã từ chối đợt món.",
                                                                )
                                                            }
                                                        >
                                                            Từ chối
                                                        </Button>
                                                    </>
                                                )}
                                                <Button
                                                    variant="outline"
                                                    disabled={busy}
                                                    onClick={() =>
                                                        void update(
                                                            () =>
                                                                salesRequest(
                                                                    `/api/sales/orders/${order.id}/batches/${batch.number}/cancel`,
                                                                    {},
                                                                ),
                                                            "Đã hủy đợt chưa xác nhận.",
                                                        )
                                                    }
                                                >
                                                    Hủy đợt
                                                </Button>
                                            </div>
                                        )}
                                    </article>
                                ))}
                            </section>
                            {can("billing.payments.collect") && (
                                <BillingLive
                                    key={order.id}
                                    orderId={Number(order.id)}
                                    businessDayId={day.data?.id}
                                />
                            )}
                            <Button
                                variant="outline"
                                disabled={busy || !day.data}
                                onClick={() =>
                                    void update(async () => {
                                        await salesRequest(
                                            `/api/billing/orders/${order.id}/close`,
                                            { businessDayId: day.data!.id },
                                        );
                                        select(null);
                                    }, "Đã đóng đơn. Với ăn tại bàn, kết thúc phiên ở Sơ đồ bàn khi khách rời đi.")
                                }
                            >
                                Đóng đơn đã xử lý xong
                            </Button>
                        </>
                    ) : (
                        <p className="rounded-xl border bg-white p-6">
                            Chọn một đơn đang mở hoặc tạo đơn mới.
                        </p>
                    )}
                    <LivePortions />
                    {can("fulfillment.portions.serve") && (
                        <KitchenLive permissions={permissions} />
                    )}
                </div>
            </div>
        </div>
    );
}
