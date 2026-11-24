"use client";

import { useState } from "react";
import { Banknote, CreditCard, RefreshCw, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { MoneyInput, money } from "@/components/ops/controls";
import { BillingApiError, createBillingApi, type BillingInvoice, type BillingPaymentInput } from "./api";

const amount = (value: number | string) => Number(value);

export function BillingLive() {
    const api = createBillingApi();
    const [invoiceId, setInvoiceId] = useState("");
    const [dayId, setDayId] = useState("");
    const [invoice, setInvoice] = useState<BillingInvoice | null>(null);
    const [method, setMethod] = useState<BillingPaymentInput["method"]>("TIEN_MAT");
    const [paymentAmount, setPaymentAmount] = useState<number | null>(null);
    const [tendered, setTendered] = useState<number | null>(null);
    const [reference, setReference] = useState("");
    const [waiver, setWaiver] = useState<number | null>(null);
    const [reason, setReason] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");

    async function run(operation: () => Promise<unknown>) {
        setBusy(true);
        setError("");
        try {
            await operation();
            if (invoiceId) setInvoice(await api.invoice(invoiceId));
        } catch (cause) {
            setError(
                cause instanceof BillingApiError
                    ? cause.code
                    : "Không thể xử lý yêu cầu Billing.",
            );
        } finally {
            setBusy(false);
        }
    }

    const due = invoice ? amount(invoice.due) : 0;
    return (
        <section className="space-y-5" aria-label="Hóa đơn thật">
            <header>
                <h2 className="text-xl font-semibold">Hóa đơn và thu tiền</h2>
                <p className="text-sm text-muted-foreground">
                    Dữ liệu và thao tác được gửi tới Billing API
                </p>
            </header>
            <form
                className="grid gap-3 rounded border bg-white p-4 sm:grid-cols-[1fr_1fr_auto]"
                onSubmit={(event) => {
                    event.preventDefault();
                    if (invoiceId) void run(async () => setInvoice(await api.invoice(invoiceId)));
                }}
            >
                <label className="grid gap-1 text-sm">
                    Mã hóa đơn
                    <input
                        required
                        inputMode="numeric"
                        className="min-h-11 rounded border p-2"
                        value={invoiceId}
                        onChange={(event) => setInvoiceId(event.target.value)}
                    />
                </label>
                <label className="grid gap-1 text-sm">
                    Mã ngày bán
                    <input
                        required
                        className="min-h-11 rounded border p-2"
                        value={dayId}
                        onChange={(event) => setDayId(event.target.value)}
                    />
                </label>
                <Button className="self-end" disabled={busy} type="submit">
                    <RefreshCw size={16} /> Tải hóa đơn
                </Button>
            </form>
            {error && <p role="alert" className="border-l-4 border-red-500 bg-red-50 p-3 text-sm">{error}</p>}
            {invoice && (
                <div className="space-y-5">
                    <div className="grid grid-cols-2 gap-3 rounded border bg-white p-4 sm:grid-cols-4">
                        <span>Tổng<br /><strong>{money(amount(invoice.obligation))} VND</strong></span>
                        <span>Đã thu<br /><strong>{money(amount(invoice.netCollected))} VND</strong></span>
                        <span>Còn lại<br /><strong>{money(due)} VND</strong></span>
                        <span>Trạng thái<br /><strong>{invoice.paymentStatus ?? "CHƯA RÕ"}</strong></span>
                    </div>
                    <form
                        className="grid max-w-xl gap-3 rounded border bg-white p-4"
                        onSubmit={(event) => {
                            event.preventDefault();
                            if (!paymentAmount || !dayId) return;
                            void run(() =>
                                api.collect(
                                    invoice.id,
                                    {
                                        businessDayId: dayId,
                                        method,
                                        amount: paymentAmount,
                                        tendered: method === "TIEN_MAT" ? tendered ?? undefined : undefined,
                                        bankReference: method === "CHUYEN_KHOAN" ? reference : undefined,
                                    },
                                    crypto.randomUUID(),
                                ),
                            );
                        }}
                    >
                        <h3 className="font-semibold">Ghi nhận khoản thu</h3>
                        <label className="grid gap-1 text-sm">Phương thức
                            <select className="min-h-11 rounded border p-2" value={method} onChange={(event) => setMethod(event.target.value as BillingPaymentInput["method"])}>
                                <option value="TIEN_MAT">Tiền mặt</option>
                                <option value="CHUYEN_KHOAN">Chuyển khoản</option>
                            </select>
                        </label>
                        <label className="grid gap-1 text-sm">Số tiền
                                <MoneyInput id="live-payment-amount" value={paymentAmount} onChange={setPaymentAmount} disabled={busy} />
                        </label>
                        {method === "TIEN_MAT" ? (
                            <label className="grid gap-1 text-sm">Khách đưa
                                <MoneyInput id="live-tendered" value={tendered} onChange={setTendered} disabled={busy} />
                            </label>
                        ) : (
                            <label className="grid gap-1 text-sm">Mã tham chiếu
                                <input required className="min-h-11 rounded border p-2" value={reference} onChange={(event) => setReference(event.target.value)} />
                            </label>
                        )}
                        <Button disabled={busy || !paymentAmount || paymentAmount > due}>
                            {method === "TIEN_MAT" ? <Banknote size={16} /> : <CreditCard size={16} />}
                            {busy ? "Đang xử lý…" : "Ghi nhận khoản thu"}
                        </Button>
                    </form>
                    <section className="space-y-3 rounded border bg-white p-4" aria-label="Lịch sử thanh toán">
                        <h3 className="font-semibold">Lịch sử thanh toán</h3>
                        {invoice.payments.map((payment) => (
                            <div key={String(payment.id)} className="flex flex-wrap items-center justify-between gap-3 border-b py-2 text-sm">
                                <span>{money(amount(payment.amount))} VND · {payment.method} · {payment.status}</span>
                                {payment.status === "CHO_XAC_NHAN" && (
                                    <div className="flex gap-2">
                                        <Button disabled={busy} onClick={() => void run(() => api.decide(invoice.id, payment.id, dayId, true, crypto.randomUUID()))}>Xác nhận</Button>
                                        <Button variant="outline" disabled={busy} onClick={() => void run(() => api.decide(invoice.id, payment.id, dayId, false, crypto.randomUUID()))}>Hủy</Button>
                                    </div>
                                )}
                            </div>
                        ))}
                    </section>
                    <form className="grid max-w-xl gap-3 rounded border bg-white p-4" onSubmit={(event) => {
                        event.preventDefault();
                        if (waiver && reason.trim()) void run(() => api.waive(invoice.id, { businessDayId: dayId, amount: waiver, reason }, crypto.randomUUID()));
                    }}>
                        <h3 className="font-semibold"><ShieldCheck className="mr-1 inline" size={16} /> Miễn thu</h3>
                        <MoneyInput id="live-waiver" value={waiver} onChange={setWaiver} disabled={busy} />
                        <textarea required className="rounded border p-2" placeholder="Lý do miễn thu" value={reason} onChange={(event) => setReason(event.target.value)} />
                        <Button variant="outline" disabled={busy || !waiver || waiver > due}>Xác nhận miễn thu</Button>
                    </form>
                </div>
            )}
        </section>
    );
}
