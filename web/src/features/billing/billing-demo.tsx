"use client";
import { useEffect, useRef, useState } from "react";
import { Banknote, CreditCard, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { MoneyInput, Receipt, money } from "@/components/ops/controls";
import {
    createDemoLedger,
    available,
    paid,
    remaining,
    total,
    waived,
    type Invoice,
    type PaymentInput,
} from "./demo-ledger";

export function BillingDemo() {
    const [api] = useState(createDemoLedger);
    const [invoice, setInvoice] = useState<Invoice | null>(null);
    const [method, setMethod] = useState<PaymentInput["method"]>("cash");
    const [amount, setAmount] = useState<number | null>(150000);
    const [tendered, setTendered] = useState<number | null>(150000);
    const [reference, setReference] = useState("");
    const [waiverOpen, setWaiverOpen] = useState(false);
    const [waiver, setWaiver] = useState<number | null>(0);
    const [reason, setReason] = useState("");
    const [permitted, setPermitted] = useState(true);
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);
    const lock = useRef(false);
    const dialog = useRef<HTMLDialogElement>(null);
    useEffect(() => {
        if (waiverOpen) dialog.current?.showModal();
        else dialog.current?.close();
    }, [waiverOpen]);
    async function execute(run: (key: string) => Promise<Invoice>) {
        if (lock.current) return;
        lock.current = true;
        setBusy(true);
        setError("");
        try {
            setInvoice(await run(crypto.randomUUID()));
        } catch (e) {
            setError(
                e instanceof Error ? e.message : "Không xử lý được yêu cầu.",
            );
        } finally {
            lock.current = false;
            setBusy(false);
        }
    }
    return (
        <section className="space-y-5" aria-label="Hóa đơn thử nghiệm">
            <header className="flex flex-wrap justify-between gap-3">
                <h2 className="text-xl font-semibold">Hóa đơn DEMO-001</h2>
                <Button
                    variant="outline"
                    disabled={busy}
                    onClick={() => void execute(() => api.current())}
                >
                    Tải hóa đơn mẫu
                </Button>
            </header>
            {error && (
                <p role="alert" className="text-red-700">
                    {error}
                </p>
            )}
            {invoice && (
                <>
                    <table className="w-full text-left text-sm">
                        <caption className="sr-only">
                            Khoản trên hóa đơn
                        </caption>
                        <thead>
                            <tr>
                                <th className="py-2">Khoản</th>
                                <th className="text-right">Thành tiền</th>
                            </tr>
                        </thead>
                        <tbody>
                            {invoice.lines.map((line) => (
                                <tr key={line.name} className="border-t">
                                    <td className="py-3">{line.name}</td>
                                    <td className="text-right">
                                        {money(line.amount)} VND
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                    <dl className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
                        {[
                            ["Tổng", total(invoice)],
                            ["Đã thu", paid(invoice)],
                            ["Miễn thu", waived(invoice)],
                            ["Còn phải thu", remaining(invoice)],
                        ].map(([label, value]) => (
                            <div key={label}>
                                <dt>{label}</dt>
                                <dd className="mt-1 font-semibold">
                                    {money(Number(value))} VND
                                </dd>
                            </div>
                        ))}
                    </dl>
                    <p role="status" className="font-semibold text-emerald-800">
                        {remaining(invoice) === 0
                            ? "ĐÃ HOÀN TẤT NGHĨA VỤ"
                            : "CHƯA THU ĐỦ"}
                    </p>
                    <form
                        className="grid max-w-lg gap-3 border-t pt-5"
                        onSubmit={(event) => {
                            event.preventDefault();
                            if (amount !== null)
                                void execute((key) =>
                                    api.pay(
                                        {
                                            amount,
                                            method,
                                            tendered: tendered ?? undefined,
                                            reference,
                                        },
                                        key,
                                    ),
                                );
                        }}
                    >
                        <h3 className="font-semibold">Thu tiền</h3>
                        <label htmlFor="payment-method">Phương thức</label>
                        <select
                            id="payment-method"
                            className="rounded border p-2"
                            value={method}
                            disabled={busy}
                            onChange={(e) =>
                                setMethod(
                                    e.target.value as PaymentInput["method"],
                                )
                            }
                        >
                            <option value="cash">Tiền mặt</option>
                            <option value="transfer">Chuyển khoản</option>
                        </select>
                        <label htmlFor="payment-amount">Số tiền thu</label>
                        <MoneyInput
                            id="payment-amount"
                            value={amount}
                            onChange={setAmount}
                            disabled={busy}
                        />
                        {method === "cash" ? (
                            <>
                                <label htmlFor="cash-tendered">Khách đưa</label>
                                <MoneyInput
                                    id="cash-tendered"
                                    value={tendered}
                                    onChange={setTendered}
                                    disabled={busy}
                                />
                                <p>
                                    Tiền thừa:{" "}
                                    {money(
                                        Math.max(
                                            0,
                                            (tendered ?? 0) - (amount ?? 0),
                                        ),
                                    )}{" "}
                                    VND
                                </p>
                            </>
                        ) : (
                            <>
                                <label htmlFor="bank-reference">
                                    Mã tham chiếu
                                </label>
                                <input
                                    id="bank-reference"
                                    className="rounded border p-2"
                                    required
                                    maxLength={100}
                                    value={reference}
                                    onChange={(e) =>
                                        setReference(e.target.value)
                                    }
                                    disabled={busy}
                                />
                            </>
                        )}
                        <Button
                            type="submit"
                            disabled={
                                busy ||
                                !amount ||
                                amount > available(invoice) ||
                                (method === "cash" && (tendered ?? 0) < amount)
                            }
                        >
                            {method === "cash" ? (
                                <Banknote size={16} />
                            ) : (
                                <CreditCard size={16} />
                            )}{" "}
                            {busy ? "Đang xử lý…" : "Ghi nhận khoản thu"}
                        </Button>
                    </form>
                    <section
                        className="space-y-3 border-t pt-5"
                        aria-label="Lịch sử thu"
                    >
                        <h3 className="font-semibold">Lịch sử thu</h3>
                        {invoice.payments.length === 0 && (
                            <p>Chưa có khoản thu.</p>
                        )}
                        {invoice.payments.map((payment) => (
                            <div
                                key={payment.id}
                                className="flex flex-wrap items-center justify-between gap-3 border-b py-3"
                            >
                                <div>
                                    <strong>
                                        {money(payment.amount)} VND ·{" "}
                                        {payment.method === "cash"
                                            ? "Tiền mặt"
                                            : "Chuyển khoản"}
                                    </strong>
                                    <p className="text-sm">
                                        {payment.status === "pending"
                                            ? "Chờ xác nhận"
                                            : payment.status === "confirmed"
                                              ? "Đã xác nhận"
                                              : "Đã hủy"}
                                        {payment.reference
                                            ? ` · ${payment.reference}`
                                            : ""}
                                        {payment.change
                                            ? ` · Thừa ${money(payment.change)} VND`
                                            : ""}
                                    </p>
                                </div>
                                {payment.status === "pending" && (
                                    <div className="flex gap-2">
                                        <Button
                                            disabled={busy}
                                            onClick={() =>
                                                void execute((key) =>
                                                    api.confirm(
                                                        payment.id,
                                                        key,
                                                    ),
                                                )
                                            }
                                        >
                                            Xác nhận
                                        </Button>
                                        <Button
                                            variant="outline"
                                            disabled={busy}
                                            onClick={() =>
                                                void execute((key) =>
                                                    api.cancel(payment.id, key),
                                                )
                                            }
                                        >
                                            Hủy khoản chờ
                                        </Button>
                                    </div>
                                )}
                            </div>
                        ))}
                    </section>
                    <Button
                        variant="outline"
                        disabled={busy || available(invoice) <= 0}
                        onClick={() => setWaiverOpen(true)}
                    >
                        <ShieldCheck size={16} /> Miễn thu
                    </Button>
                    {waiverOpen && (
                        <dialog
                            ref={dialog}
                            onCancel={() => setWaiverOpen(false)}
                            aria-labelledby="waiver-title"
                            className="fixed inset-0 z-50 m-auto w-11/12 max-w-lg rounded border bg-white p-6 shadow-xl"
                        >
                            <form
                                className="grid gap-3"
                                onSubmit={(event) => {
                                    event.preventDefault();
                                    if (waiver !== null)
                                        void execute((key) =>
                                            api.waive(
                                                waiver,
                                                reason,
                                                key,
                                                permitted,
                                            ),
                                        );
                                }}
                            >
                                <h3
                                    id="waiver-title"
                                    className="text-lg font-semibold"
                                >
                                    Miễn phần chưa thu
                                </h3>
                                <label className="flex gap-2">
                                    <input
                                        type="checkbox"
                                        checked={permitted}
                                        onChange={(e) =>
                                            setPermitted(e.target.checked)
                                        }
                                    />{" "}
                                    Quyền miễn thu (mô phỏng)
                                </label>
                                <label htmlFor="waiver-amount">
                                    Số tiền miễn
                                </label>
                                <MoneyInput
                                    id="waiver-amount"
                                    value={waiver}
                                    onChange={setWaiver}
                                    disabled={busy}
                                />
                                <label htmlFor="waiver-reason">Lý do</label>
                                <textarea
                                    id="waiver-reason"
                                    required
                                    className="rounded border p-2"
                                    value={reason}
                                    onChange={(e) => setReason(e.target.value)}
                                />
                                {error && (
                                    <p role="alert" className="text-red-700">
                                        {error}
                                    </p>
                                )}
                                <div className="flex flex-wrap gap-2">
                                    <Button
                                        type="submit"
                                        disabled={
                                            busy ||
                                            !permitted ||
                                            !waiver ||
                                            waiver > available(invoice)
                                        }
                                    >
                                        Xác nhận miễn thu
                                    </Button>
                                    <Button
                                        type="button"
                                        variant="outline"
                                        disabled={busy}
                                        onClick={() => setWaiverOpen(false)}
                                    >
                                        Đóng
                                    </Button>
                                </div>
                            </form>
                        </dialog>
                    )}
                    {remaining(invoice) === 0 && (
                        <Receipt
                            number={invoice.id}
                            lines={[
                                ...invoice.lines,
                                ...invoice.waivers.map((w) => ({
                                    name: `Miễn thu: ${w.reason}`,
                                    amount: -w.amount,
                                })),
                            ]}
                            total={total(invoice) - waived(invoice)}
                        />
                    )}
                </>
            )}
        </section>
    );
}
