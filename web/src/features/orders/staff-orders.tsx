"use client";
import { VietnamDateField } from "@/components/vietnam-date-field";
import { isoDate } from "@/lib/vietnam-date";
import { useEffect, useState, useRef } from "react";
import Link from "next/link";
import {
    ArrowUpRight,
    ChevronRight,
    Coffee,
    Grid2X2,
    Plus,
    ReceiptText,
    Search,
    ShoppingBasket,
    Soup,
    Utensils,
    WalletCards,
} from "lucide-react";
import styles from "./orders.module.css";
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
import { MenuPhoto } from "@/features/catalog/menu-photo";
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
        [pickupDate, setPickupDate] = useState(""),
        [pickupTime, setPickupTime] = useState("");
    const pickup =
        isoDate(pickupDate) && pickupTime
            ? isoDate(pickupDate) + "T" + pickupTime
            : "";
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
                setPickupDate("");
                setPickupTime("");
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
        <div className={styles.page}>
            <header className={styles.header}>
                <h1>
                    <span className={styles.titleIcon}>
                        <ReceiptText />
                    </span>
                    Đơn hàng nhân viên
                </h1>
                <nav aria-label="Điều hướng đơn hàng">
                    {seating && (
                        <Link href="/tables">
                            <Grid2X2 /> Sơ đồ và chuyển bàn
                        </Link>
                    )}
                    {manage && (
                        <Link href="/orders?view=service">
                            <WalletCards /> Phục vụ và thanh toán{" "}
                            <ArrowUpRight />
                        </Link>
                    )}
                </nav>
            </header>
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
                <div className={styles.workspace}>
                    <section className={styles.panel}>
                        <h2>
                            <Utensils />
                            Gọi món
                        </h2>
                        {availability.isError && (
                            <p role="alert">
                                Chưa xác minh được tình trạng bán.
                            </p>
                        )}
                        {stock?.businessDayId === null && (
                            <p>Chưa mở ngày bán.</p>
                        )}
                        {board.isError && (
                            <p role="alert">Không thể cập nhật bàn đang mở.</p>
                        )}
                        <fieldset disabled={busy}>
                            <div className={styles.destination}>
                                <label>
                                    Loại đơn{" "}
                                    <select
                                        value={takeaway ? "away" : "table"}
                                        onChange={(e) =>
                                            setTakeaway(
                                                e.target.value === "away",
                                            )
                                        }
                                    >
                                        <option value="table">Tại bàn</option>
                                        <option value="away">Mang về</option>
                                    </select>
                                </label>
                                <form
                                    id="send-batch"
                                    onSubmit={submit}
                                    className={styles.fields}
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
                                            <VietnamDateField
                                                label="Ngày nhận"
                                                value={pickupDate}
                                                onChange={setPickupDate}
                                            />
                                            <label>
                                                Giờ nhận
                                                <input
                                                    type="text"
                                                    required
                                                    placeholder="HH:mm"
                                                    maxLength={5}
                                                    value={pickupTime}
                                                    pattern="([01][0-9]|2[0-3]):[0-5][0-9]"
                                                    onChange={(e) => {
                                                        const field =
                                                            e.currentTarget;
                                                        field.setCustomValidity(
                                                            field.validity
                                                                .patternMismatch
                                                                ? "Nhập giờ từ 00:00 đến 23:59."
                                                                : "",
                                                        );
                                                        setPickupTime(
                                                            field.value,
                                                        );
                                                    }}
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
                                                <option value="">
                                                    Chọn bàn
                                                </option>
                                                {tables.map((t) => (
                                                    <option
                                                        key={String(
                                                            t.session!.id,
                                                        )}
                                                        value={String(
                                                            t.session!.id,
                                                        )}
                                                    >
                                                        {t.name}
                                                    </option>
                                                ))}
                                            </select>
                                        </label>
                                    )}
                                </form>
                            </div>
                            <div className={styles.filters}>
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
                                    <span className={styles.search}>
                                        <Search aria-hidden="true" />
                                        <input
                                            type="search"
                                            placeholder="Nhập tên món…"
                                            value={search}
                                            onChange={(e) =>
                                                find(e.target.value)
                                            }
                                        />
                                    </span>
                                </label>
                            </div>
                            <div className={styles.products}>
                                {menu?.items
                                    .filter(
                                        (i) =>
                                            i.active &&
                                            menu.groups.some(
                                                (g) =>
                                                    g.active &&
                                                    g.id === i.groupId,
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
                                        const prices = i.variants
                                            .filter((v) => v.active)
                                            .map((v) => v.price);
                                        const Icon =
                                            i.station === "BAR" ? Coffee : Soup;
                                        return (
                                            <button
                                                key={i.id}
                                                className={styles.product}
                                                aria-label={
                                                    i.name +
                                                    (stock?.businessDayId &&
                                                    sold
                                                        ? " · Hết món"
                                                        : "")
                                                }
                                                disabled={
                                                    !stock?.businessDayId ||
                                                    sold
                                                }
                                                onClick={() => choose(i)}
                                            >
                                                <span
                                                    className={
                                                        styles.productTop
                                                    }
                                                >
                                                    <span
                                                        className={
                                                            styles.dishIcon
                                                        }
                                                    >
                                                        {i.imageObjectKey ? (
                                                            <MenuPhoto
                                                                objectKey={
                                                                    i.imageObjectKey
                                                                }
                                                            />
                                                        ) : (
                                                            <Icon />
                                                        )}
                                                    </span>
                                                    <span
                                                        className={
                                                            styles.addIcon
                                                        }
                                                    >
                                                        <Plus size={14} />
                                                    </span>
                                                </span>
                                                <small>
                                                    {
                                                        menu.groups.find(
                                                            (g) =>
                                                                g.id ===
                                                                i.groupId,
                                                        )?.name
                                                    }
                                                </small>
                                                <strong>{i.name}</strong>
                                                <span className={styles.price}>
                                                    {stock?.businessDayId &&
                                                    sold
                                                        ? "Hết món"
                                                        : (Math.min(
                                                              ...prices,
                                                          ) !==
                                                          Math.max(...prices)
                                                              ? "Từ "
                                                              : "") +
                                                          formatPrice(
                                                              Math.min(
                                                                  ...prices,
                                                              ),
                                                          )}
                                                </span>
                                            </button>
                                        );
                                    })}
                            </div>
                        </fieldset>
                    </section>
                    <section className={styles.cart}>
                        <div className={styles.cartHeader}>
                            <h2>
                                <ShoppingBasket />
                                Giỏ món
                            </h2>
                            <span>
                                {lines.reduce((sum, l) => sum + l.quantity, 0)}{" "}
                                phần
                            </span>
                        </div>
                        <fieldset disabled={busy} className={styles.cartBody}>
                            {!lines.length && (
                                <div className={styles.emptyCart}>
                                    <ShoppingBasket size={28} />
                                    <strong>Giỏ món đang trống</strong>
                                    <span>
                                        Chọn món từ thực đơn để thêm vào giỏ.
                                    </span>
                                </div>
                            )}
                            {lines.map((l) => (
                                <div key={l.id} className={styles.cartLine}>
                                    <strong>{l.label}</strong>
                                    <span>{formatPrice(l.unitPrice)}</span>
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
                                                const n = Number(
                                                    e.target.value,
                                                );
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
                                                                      quantity:
                                                                          n,
                                                                  }
                                                                : r,
                                                        ),
                                                    );
                                            }}
                                        />
                                    </label>{" "}
                                    <button
                                        onClick={() =>
                                            cart(
                                                lines.filter(
                                                    (r) => r.id !== l.id,
                                                ),
                                            )
                                        }
                                    >
                                        Bỏ món
                                    </button>
                                </div>
                            ))}
                            <p className={styles.total}>
                                Tổng cộng{" "}
                                <strong>
                                    {formatPrice(
                                        lines.reduce(
                                            (sum, l) =>
                                                sum + l.unitPrice * l.quantity,
                                            0,
                                        ),
                                    )}
                                </strong>
                            </p>
                            <button
                                className={styles.submit}
                                form="send-batch"
                                disabled={
                                    !stock?.businessDayId ||
                                    !lines.length ||
                                    busy ||
                                    lines.some((l) =>
                                        unavailableChoice(stock, l),
                                    )
                                }
                            >
                                Gửi đợt món <ChevronRight size={16} />
                            </button>
                        </fieldset>
                    </section>
                </div>
            )}
            {manage && <LivePortions />}
            {item && menu && (
                <ItemPicker
                    item={item}
                    menu={menu}
                    stock={stock}
                    close={() => choose(null)}
                    add={(l) => cart([...lines, l])}
                />
            )}
            <fieldset className={styles.panel} disabled={busy}>
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
