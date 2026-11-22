import { useState } from "react";
import type { Batch, StaffOrder } from "./batches";
import { useAuth } from "@/components/auth/auth-provider";
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
    const [editing, setEditing] = useState(false);
    const [note, setNote] = useState("");
    const { user } = useAuth();
    // Draft batches have no kitchen ticket to prove a confirmed portion is still unstarted.
    const allowed =
        manage &&
        (batch.status === "pending" ||
            (batch.status === "confirmed" && user?.roles.includes("Manager")));
    return (
        <section className="my-3 border-t pt-3">
            <h4>
                Đợt {batch.id.slice(0, 8)} · {statuses[batch.status]}
            </h4>
            {batch.lines.map((l) => (
                <div key={l.id}>
                    {l.label} × {l.quantity}
                    {l.note && <span> · {l.note}</span>}
                    {l.editReason && <span> · Lý do sửa: {l.editReason}</span>}
                    {!!l.cancelled && (
                        <span>
                            {" "}
                            · Đã hủy {l.cancelled} ({l.cancelReason})
                        </span>
                    )}
                    {allowed && (l.cancelled ?? 0) < l.quantity && (
                        <>
                            <button
                                type="button"
                                onClick={() => {
                                    setLineId(l.id);
                                    setEditing(true);
                                    setNote(l.note ?? "");
                                    setReason("");
                                    setCount(1);
                                }}
                            >
                                Sửa phần món
                            </button>
                            <button
                                type="button"
                                onClick={() => {
                                    setLineId(l.id);
                                    setEditing(false);
                                    setReason("");
                                    setCount(1);
                                }}
                            >
                                Hủy phần món
                            </button>
                        </>
                    )}
                    {lineId === l.id && allowed && (
                        <form
                            onSubmit={(e) => {
                                e.preventDefault();
                                if (
                                    ((!editing ||
                                        batch.status === "confirmed") &&
                                        !reason.trim()) ||
                                    !Number.isInteger(count) ||
                                    count < 1 ||
                                    count > l.quantity - (l.cancelled ?? 0)
                                )
                                    return;
                                if (editing) {
                                    const revised = {
                                        ...l,
                                        id: crypto.randomUUID(),
                                        quantity: count,
                                        cancelled: 0,
                                        cancelReason: undefined,
                                        note: note.trim(),
                                        editReason: reason.trim() || undefined,
                                    };
                                    update({
                                        ...batch,
                                        lines: batch.lines.flatMap((row) => {
                                            if (row.id !== l.id) return [row];
                                            return count === row.quantity
                                                ? [revised]
                                                : [
                                                      {
                                                          ...row,
                                                          quantity:
                                                              row.quantity -
                                                              count,
                                                      },
                                                      revised,
                                                  ];
                                        }),
                                    });
                                    setLineId("");
                                    return;
                                }
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
                                {editing ? "Số phần sửa" : "Số phần hủy"}
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
                            {editing && (
                                <label>
                                    Ghi chú chế biến{" "}
                                    <input
                                        maxLength={2000}
                                        value={note}
                                        onChange={(e) =>
                                            setNote(e.target.value)
                                        }
                                    />
                                </label>
                            )}
                            {(!editing || batch.status === "confirmed") && (
                                <label>
                                    {editing ? "Lý do sửa" : "Lý do hủy"}
                                    <input
                                        required
                                        value={reason}
                                        onChange={(e) =>
                                            setReason(e.target.value)
                                        }
                                    />
                                </label>
                            )}
                            <button>
                                {editing ? "Lưu phần món" : "Xác nhận hủy"}
                            </button>
                            <button type="button" onClick={() => setLineId("")}>
                                Đóng
                            </button>
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
