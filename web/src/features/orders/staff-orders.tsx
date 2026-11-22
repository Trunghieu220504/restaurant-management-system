"use client";
import { useEffect, useState, useRef } from "react";
import Link from "next/link";
import {
    catalogApi,
    formatPrice,
    describeCatalogError,
    type CatalogMenu,
    type MenuItem,
} from "@/features/catalog/api";
import { authFetch } from "@/lib/auth/auth-client";
import { useAuth } from "@/components/auth/auth-provider";
import { usePermissions } from "@/components/permission-provider";
import {
    appendBatch,
    quote,
    type StaffOrder,
    type Line,
    type Destination,
} from "./batches";
import { BatchList } from "./batch-list";
import { ItemPicker } from "./item-picker";
import { LiveQueryScope, liveInterval } from "@/lib/realtime/live-query";
import { useQuery } from "@tanstack/react-query";
import {
    unavailableItem,
    readAvailability,
    unavailableChoice,
    useAvailability,
} from "@/features/catalog/availability";
import { LivePortions } from "./live-portions";

type Table = { name: string; session: { id: string | number } | null };
async function readTables(signal?: AbortSignal): Promise<Table[]> {
    const response = await authFetch("/api/seating/board", {
        signal,
        cache: "no-store",
    });
    if (!response.ok) throw Error("Không thể tải danh sách phiên bàn.");
    const areas: { tables: Table[] }[] = await response.json();
    return areas.flatMap((a) => a.tables).filter((t) => t.session);
}
export function StaffOrders() {
    const { user } = useAuth();
    return (
        user && (
            <LiveQueryScope key={user.id}>
                <Workspace account={user.id} />
            </LiveQueryScope>
        )
    );
}
function Workspace({ account }: { account: string }) {
    const { can } = usePermissions();
    const manage = can("sales.orders.manage"),
        confirm = can("sales.batches.confirm"),
        seating = can("seating.sessions.manage");
    const [menu, setMenu] = useState<CatalogMenu | null>(null);
    const board = useQuery({
        queryKey: ["staff", "tables"],
        queryFn: ({ signal }) => readTables(signal),
        enabled: manage && seating,
        refetchInterval: liveInterval,
    });
    const tables = board.data ?? [];
    const availability = useAvailability(manage);
    const stock = availability.isError ? undefined : availability.data;
    const [takeaway, setTakeaway] = useState(false),
        [session, chooseTable] = useState("");
    const [name, setName] = useState(""),
        [phone, setPhone] = useState(""),
        [pickup, setPickup] = useState("");
    const [search, find] = useState(""),
        [group, filter] = useState(""),
        [item, choose] = useState<MenuItem | null>(null);
    const [lines, cart] = useState<Line[]>([]),
        [orders, setOrders] = useState<StaffOrder[]>([]);
    const [error, fail] = useState(""),
        [notice, notify] = useState(""),
        [busy, disable] = useState(false),
        [attempt, retry] = useState(0);
    const lock = useRef(false),
        key = "bao.sales.batches.v1." + account;
    useEffect(() => {
        const c = new AbortController();
        async function load() {
            try {
                setOrders(JSON.parse(sessionStorage.getItem(key) ?? "[]"));
                if (manage) {
                    const data = await catalogApi.load(c.signal);
                    if (!c.signal.aborted) setMenu(data);
                }
            } catch (err) {
                if (!c.signal.aborted)
                    fail(
                        err instanceof Error
                            ? err.message
                            : describeCatalogError(err),
                    );
            }
        }
        void load();
        return () => c.abort();
    }, [key, manage, seating, attempt]);
    function persist(next: StaffOrder[]) {
        sessionStorage.setItem(key, JSON.stringify(next));
        setOrders(next);
    }
    async function submit(e: React.FormEvent) {
        e.preventDefault();
        if (!manage || !lines.length || lock.current) return;
        lock.current = true;
        disable(true);
        fail("");
        notify("");
        try {
            const day = await authFetch("/api/salesday/current", {
                cache: "no-store",
            });
            if (!day.ok) throw Error("Không thể kiểm tra ngày bán.");
            if ((await day.json())?.status !== "DANG_BAN")
                throw Error("Cần mở ngày bán trước khi gửi đợt món.");
            let destination: Destination;
            if (takeaway) {
                if (!name.trim() || !phone.trim() || !pickup)
                    throw Error("Nhập tên, điện thoại và giờ nhận.");
                destination = {
                    kind: "takeaway",
                    name: name.trim(),
                    phone: phone.trim(),
                    pickup,
                };
            } else {
                const latest = await readTables();
                const table = latest.find(
                    (t) => String(t.session?.id) === session,
                );
                if (!table) throw Error("Chọn bàn có phiên đang mở.");
                destination = { kind: "table", name: table.name, session };
            }
            const [fresh, flags] = await Promise.all([
                catalogApi.load(),
                readAvailability(),
            ]);
            if (lines.some((line) => unavailableChoice(flags, line)))
                throw Error(
                    "Giỏ có mục đã hết hoặc ngày bán đã đóng. Kiểm tra lại giỏ món.",
                );
            setMenu(fresh);
            const checked = lines.map((l) => ({ ...l, ...quote(fresh, l) }));
            if (checked.some((l, i) => l.unitPrice !== lines[i].unitPrice)) {
                cart(checked);
                throw Error(
                    "Giá món đã đổi (order.price.changed). Kiểm tra giá mới trong giỏ rồi gửi lại.",
                );
            }
            persist(appendBatch(orders, destination, checked));
            cart([]);
            notify("Đã gửi đợt món. Trạng thái: Chờ xác nhận.");
            if (takeaway) {
                setName("");
                setPhone("");
                setPickup("");
            }
        } catch (err) {
            fail(
                err instanceof Error ? err.message : "Không gửi được đợt món.",
            );
        } finally {
            lock.current = false;
            disable(false);
        }
    }
    return (
        <div className="space-y-5 [&_h1]:text-2xl [&_h2]:text-xl [&_h3]:font-semibold [&_button]:rounded-md [&_button]:border [&_button]:px-3 [&_button]:py-2 [&_button:disabled]:opacity-50 [&_input:not([type=checkbox])]:border [&_select]:border [&_section]:space-y-3">
            <h1>Đơn hàng nhân viên</h1>
            {seating && (
                <Link className="underline" href="/tables">
                    Sơ đồ và chuyển bàn
                </Link>
            )}
            {manage && <LivePortions />}
            <p>Đợt gọi món đang được lưu thử trong tab này.</p>
            {manage && (
                <Link className="underline" href="/orders?view=service">
                    Phục vụ và thanh toán đơn hiện có
                </Link>
            )}
            {error && (
                <p role="alert">
                    {error}{" "}
                    <button
                        onClick={() => {
                            fail("");
                            retry(attempt + 1);
                        }}
                    >
                        Tải lại dữ liệu
                    </button>
                </p>
            )}
            {notice && <p role="status">{notice}</p>}
            {manage && (
                <section>
                    <h2>Gọi món</h2>
                    {availability.isError && (
                        <p role="alert">Chưa xác minh được tình trạng bán.</p>
                    )}
                    {stock?.businessDayId === null && <p>Chưa mở ngày bán.</p>}
                    {board.isError && (
                        <p role="alert">Không thể cập nhật bàn đang mở.</p>
                    )}
                    <fieldset disabled={busy} className="space-y-3">
                        <label>
                            Loại đơn{" "}
                            <select
                                value={takeaway ? "away" : "table"}
                                onChange={(e) =>
                                    setTakeaway(e.target.value === "away")
                                }
                            >
                                <option value="table">Tại bàn</option>
                                <option value="away">Mang về</option>
                            </select>
                        </label>
                        <form
                            id="send-batch"
                            onSubmit={submit}
                            className="flex flex-wrap gap-3"
                        >
                            {takeaway ? (
                                <>
                                    <label>
                                        Tên khách{" "}
                                        <input
                                            required
                                            value={name}
                                            onChange={(e) =>
                                                setName(e.target.value)
                                            }
                                        />
                                    </label>
                                    <label>
                                        Điện thoại{" "}
                                        <input
                                            required
                                            type="tel"
                                            pattern="[+]?[0-9 ]{8,20}"
                                            value={phone}
                                            onChange={(e) =>
                                                setPhone(e.target.value)
                                            }
                                        />
                                    </label>
                                    <label>
                                        Giờ nhận{" "}
                                        <input
                                            required
                                            type="datetime-local"
                                            value={pickup}
                                            onChange={(e) =>
                                                setPickup(e.target.value)
                                            }
                                        />
                                    </label>
                                </>
                            ) : (
                                <label>
                                    Bàn đang mở{" "}
                                    <select
                                        required
                                        value={session}
                                        onChange={(e) =>
                                            chooseTable(e.target.value)
                                        }
                                    >
                                        <option value="">Chọn bàn</option>
                                        {tables.map((t) => (
                                            <option
                                                key={String(t.session!.id)}
                                                value={String(t.session!.id)}
                                            >
                                                {t.name}
                                            </option>
                                        ))}
                                    </select>
                                </label>
                            )}
                        </form>
                        <label>
                            Nhóm món{" "}
                            <select
                                value={group}
                                onChange={(e) => filter(e.target.value)}
                            >
                                <option value="">Tất cả</option>
                                {menu?.groups
                                    .filter((g) => g.active)
                                    .map((g) => (
                                        <option key={g.id} value={g.id}>
                                            {g.name}
                                        </option>
                                    ))}
                            </select>
                        </label>{" "}
                        <label>
                            Tìm món{" "}
                            <input
                                type="search"
                                value={search}
                                onChange={(e) => find(e.target.value)}
                            />
                        </label>
                        <div className="flex flex-wrap gap-2">
                            {menu?.items
                                .filter(
                                    (i) =>
                                        i.active &&
                                        menu.groups.some(
                                            (g) =>
                                                g.active && g.id === i.groupId,
                                        ) &&
                                        i.variants.some((v) => v.active) &&
                                        (!group || group === i.groupId) &&
                                        i.name
                                            .toLocaleLowerCase()
                                            .includes(
                                                search
                                                    .trim()
                                                    .toLocaleLowerCase(),
                                            ),
                                )
                                .map((i) => {
                                    const sold =
                                        stock &&
                                        unavailableItem(stock, menu, i);
                                    return (
                                        <button
                                            key={i.id}
                                            disabled={
                                                !stock?.businessDayId || sold
                                            }
                                            onClick={() => choose(i)}
                                        >
                                            {i.name}
                                            {stock?.businessDayId &&
                                                sold &&
                                                " · Hết món"}
                                        </button>
                                    );
                                })}
                        </div>
                        <h3>Giỏ món</h3>
                        {lines.map((l) => (
                            <p key={l.id}>
                                {l.label} · {formatPrice(l.unitPrice)}{" "}
                                {stock?.businessDayId &&
                                    unavailableChoice(stock, l) && (
                                        <strong>Hết món</strong>
                                    )}
                                <label>
                                    Số lượng{" "}
                                    <input
                                        type="number"
                                        min={1}
                                        max={99}
                                        value={l.quantity}
                                        onChange={(e) => {
                                            const n = Number(e.target.value);
                                            if (
                                                Number.isInteger(n) &&
                                                n >= 1 &&
                                                n <= 99
                                            )
                                                cart(
                                                    lines.map((r) =>
                                                        r.id === l.id
                                                            ? {
                                                                  ...r,
                                                                  quantity: n,
                                                              }
                                                            : r,
                                                    ),
                                                );
                                        }}
                                    />
                                </label>{" "}
                                <button
                                    onClick={() =>
                                        cart(lines.filter((r) => r.id !== l.id))
                                    }
                                >
                                    Bỏ món
                                </button>
                            </p>
                        ))}
                        <p>
                            Tổng{" "}
                            {formatPrice(
                                lines.reduce(
                                    (sum, l) => sum + l.unitPrice * l.quantity,
                                    0,
                                ),
                            )}
                        </p>
                        <button
                            form="send-batch"
                            disabled={
                                !stock?.businessDayId ||
                                !lines.length ||
                                busy ||
                                lines.some((l) => unavailableChoice(stock, l))
                            }
                        >
                            Gửi đợt món
                        </button>
                    </fieldset>
                </section>
            )}
            {item && menu && (
                <ItemPicker
                    item={item}
                    menu={menu}
                    stock={stock}
                    close={() => choose(null)}
                    add={(l) => cart([...lines, l])}
                />
            )}
            <fieldset disabled={busy}>
                <BatchList
                    orders={orders.map((order) => {
                        const dest = order.destination;
                        if (dest.kind !== "table") return order;
                        const table = tables.find(
                            (t) => String(t.session?.id) === dest.session,
                        );
                        return table
                            ? {
                                  ...order,
                                  destination: { ...dest, name: table.name },
                              }
                            : order;
                    })}
                    manage={manage}
                    confirm={confirm}
                    save={(next) => {
                        try {
                            persist(next);
                            notify("Đã cập nhật trạng thái đợt món.");
                        } catch {
                            fail("Không thể lưu đợt món trong tab này.");
                        }
                    }}
                />
            </fieldset>
        </div>
    );
}
