import { useState } from "react";
import type { Batch, StaffOrder } from "./batches";
const statuses = {
    pending: "Chờ xác nhận",
    confirmed: "Đã xác nhận",
    rejected: "Đã từ chối",
    cancelled: "Đã hủy",
};
function BatchDetail({
    batch,
    manage,
    confirm,
    update,
}: {
    batch: Batch;
    manage: boolean;
    confirm: boolean;
    update: (next: Batch) => void;
}) {
    const [reason, setReason] = useState("");
    const [lineId, setLineId] = useState("");
    const [count, setCount] = useState(1);
    return (
        <section className="my-3 border-t pt-3">
            <h4>
                Đợt {batch.id.slice(0, 8)} · {statuses[batch.status]}
            </h4>
            {batch.lines.map((l) => (
                <div key={l.id}>
                    {l.label} × {l.quantity}
                    {!!l.cancelled && (
                        <span>
                            {" "}
                            · Đã hủy {l.cancelled} ({l.cancelReason})
                        </span>
                    )}
                    {manage &&
                        (batch.status === "confirmed" ||
                            batch.status === "pending") &&
                        (l.cancelled ?? 0) < l.quantity && (
                            <button
                                type="button"
                                onClick={() => {
                                    setLineId(l.id);
                                    setReason("");
                                    setCount(1);
                                }}
                            >
                                Hủy phần món
                            </button>
                        )}
                    {lineId === l.id && manage && (
                        <form
                            onSubmit={(e) => {
                                e.preventDefault();
                                if (
                                    !reason.trim() ||
                                    count < 1 ||
                                    count > l.quantity - (l.cancelled ?? 0)
                                )
                                    return;
                                const remaining =
                                    batch.lines.reduce(
                                        (sum, row) =>
                                            sum +
                                            row.quantity -
                                            (row.cancelled ?? 0),
                                        0,
                                    ) - count;
                                update({
                                    ...batch,
                                    status:
                                        remaining === 0 &&
                                        batch.status === "pending"
                                            ? "cancelled"
                                            : batch.status,
                                    lines: batch.lines.map((row) =>
                                        row.id === l.id
                                            ? {
                                                  ...row,
                                                  cancelled:
                                                      (row.cancelled ?? 0) +
                                                      count,
                                                  cancelReason: reason.trim(),
                                              }
                                            : row,
                                    ),
                                });
                                setLineId("");
                            }}
                        >
                            <label>
                                Số phần hủy
                                <input
                                    type="number"
                                    min={1}
                                    max={l.quantity - (l.cancelled ?? 0)}
                                    required
                                    value={count}
                                    onChange={(e) =>
                                        setCount(Number(e.target.value))
                                    }
                                />
                            </label>
                            <label>
                                Lý do hủy
                                <input
                                    required
                                    value={reason}
                                    onChange={(e) => setReason(e.target.value)}
                                />
                            </label>
                            <button>Xác nhận hủy</button>
                        </form>
                    )}
                </div>
            ))}
            {batch.reason && <p>Lý do từ chối: {batch.reason}</p>}
            {confirm && batch.status === "pending" && (
                <div>
                    <button
                        onClick={() =>
                            update({ ...batch, status: "confirmed" })
                        }
                    >
                        Xác nhận đợt
                    </button>{" "}
                    <label>
                        Lý do từ chối
                        <input
                            value={reason}
                            onChange={(e) => setReason(e.target.value)}
                        />
                    </label>
                    <button
                        disabled={!reason.trim()}
                        onClick={() =>
                            update({
                                ...batch,
                                status: "rejected",
                                reason: reason.trim(),
                            })
                        }
                    >
                        Từ chối đợt
                    </button>
                </div>
            )}
        </section>
    );
}
export function BatchList({
    orders,
    manage,
    confirm,
    save,
}: {
    orders: StaffOrder[];
    manage: boolean;
    confirm: boolean;
    save: (orders: StaffOrder[]) => void;
}) {
    const [pending, filter] = useState(true);
    return (
        <section>
            <h2>Đợt gọi món</h2>
            <label>
                <input
                    type="checkbox"
                    checked={pending}
                    onChange={(e) => filter(e.target.checked)}
                />{" "}
                Chỉ hiện đợt chờ xác nhận
            </label>
            {!orders.some((o) =>
                o.batches.some((b) => !pending || b.status === "pending"),
            ) && <p>Chưa có đợt phù hợp.</p>}
            {orders.map((order) => {
                const batches = order.batches.filter(
                    (b) => !pending || b.status === "pending",
                );
                return (
                    batches.length > 0 && (
                        <article
                            className="mt-3 rounded-xl border p-4"
                            key={order.id}
                        >
                            <h3>
                                {order.destination.kind === "takeaway"
                                    ? "Mang về · "
                                    : ""}
                                {order.destination.name} · Đơn{" "}
                                {order.id.slice(0, 8)}
                            </h3>
                            {order.destination.kind === "takeaway" && (
                                <p>
                                    {order.destination.phone} · Giờ nhận{" "}
                                    {order.destination.pickup.replace("T", " ")}
                                </p>
                            )}
                            {batches.map((b) => (
                                <BatchDetail
                                    key={b.id}
                                    batch={b}
                                    manage={manage}
                                    confirm={confirm}
                                    update={(next) =>
                                        save(
                                            orders.map((o) =>
                                                o.id === order.id
                                                    ? {
                                                          ...o,
                                                          batches:
                                                              o.batches.map(
                                                                  (old) =>
                                                                      old.id ===
                                                                      b.id
                                                                          ? next
                                                                          : old,
                                                              ),
                                                      }
                                                    : o,
                                            ),
                                        )
                                    }
                                />
                            ))}
                        </article>
                    )
                );
            })}
        </section>
    );
}
