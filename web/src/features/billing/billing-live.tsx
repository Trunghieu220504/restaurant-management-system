"use client";
import { useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { usePermissions } from "@/components/permission-provider";
import { MoneyInput, money } from "@/components/ops/controls";
import {
    BillingApiError,
    createBillingApi,
    type BillingPaymentInput,
} from "./api";
import { RequestAttempts } from "./request-attempts";

export function BillingLive(
    props: { orderId?: number; businessDayId?: string } = {},
) {
    if (!props.orderId)
        return (
            <p>
                Chọn đơn tại{" "}
                <Link href="/orders" className="underline">
                    màn đơn hàng
                </Link>{" "}
                để thu tiền.
            </p>
        );
    return <BillingWorkspace {...props} />;
}

function BillingWorkspace({
    orderId,
    businessDayId,
}: { orderId?: number; businessDayId?: string } = {}) {
    const api = useMemo(() => createBillingApi(), []);
    const { can } = usePermissions();
    const attempts = useRef(new RequestAttempts());
    const invoice = useQuery({
        queryKey: ["billing-live", orderId],
        queryFn: () => api.invoiceForOrder(orderId!),
        enabled: !!orderId,
        refetchInterval: 3000,
        retry: false,
    });
    const [method, setMethod] =
        useState<BillingPaymentInput["method"]>("TIEN_MAT");
    const [amount, setAmount] = useState<number | null>(null);
    const [tendered, setTendered] = useState<number | null>(null);
    const [reference, setReference] = useState("");
    const [waiver, setWaiver] = useState<number | null>(null);
    const [reason, setReason] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    async function write(
        operation: string,
        payload: unknown,
        action: (key: string) => Promise<unknown>,
    ) {
        if (busy) return;
        const key = attempts.current.begin(operation, payload);
        setBusy(true);
        setError("");
        setNotice("");
        try {
            await action(key);
            attempts.current.complete(operation, key);
            setNotice("Đã ghi nhận thành công.");
            setAmount(null);
            setTendered(null);
            setWaiver(null);
            await invoice.refetch();
        } catch (cause) {
            const messages: Record<string, string> = {
                "billing.amount.exceeds_available":
                    "Số tiền vượt phần còn được phép thu hoặc miễn thu. Hãy tải lại hóa đơn.",
                "billing.idempotency.conflict":
                    "Yêu cầu đã được dùng cho nội dung khác. Kiểm tra lại trạng thái.",
                "salesday.day.not_open": "Ngày bán không còn mở.",
            };
            setError(
                cause instanceof BillingApiError
                    ? (messages[cause.code] ??
                          "Không thể ghi nhận. Hãy kiểm tra trạng thái và thử lại cùng nội dung nếu chưa rõ kết quả.")
                    : "Mất kết nối. Kiểm tra hóa đơn trước khi thử lại; yêu cầu đang giữ mã chống trùng.",
            );
        } finally {
            setBusy(false);
        }
    }
    if (!orderId)
        return (
            <p>
                Chọn đơn tại{" "}
                <Link href="/orders" className="underline">
                    màn đơn hàng
                </Link>{" "}
                để thu tiền.
            </p>
        );
    if (invoice.isLoading) return <p role="status">Đang tải hóa đơn…</p>;
    if (
        invoice.error instanceof BillingApiError &&
        invoice.error.status === 404
    )
        return (
            <p role="status">Xác nhận ít nhất một đợt món để lập hóa đơn.</p>
        );
    if (!invoice.data)
        return (
            <p role="alert">
                Không tải được hóa đơn.{" "}
                <Button
                    variant="outline"
                    onClick={() => void invoice.refetch()}
                >
                    Thử lại
                </Button>
            </p>
        );
    const bill = invoice.data;
    const due = Number(bill.due);
    return (
        <section
            className="space-y-4 rounded-xl border bg-white p-4"
            aria-label="Hóa đơn và thu tiền"
        >
            <h2 className="text-xl font-semibold">Hóa đơn và thu tiền</h2>
            <div className="grid grid-cols-3 gap-3">
                <p>
                    Tổng nghĩa vụ
                    <strong className="block">
                        {money(Number(bill.obligation))} VND
                    </strong>
                </p>
                <p>
                    Đã thu
                    <strong className="block">
                        {money(Number(bill.netCollected))} VND
                    </strong>
                </p>
                <p>
                    Còn phải thu
                    <strong className="block" data-testid="invoice-due">
                        {money(due)} VND
                    </strong>
                </p>
            </div>
            {error && (
                <p role="alert" className="rounded bg-red-50 p-3">
                    {error}
                </p>
            )}
            {notice && (
                <p role="status" className="rounded bg-emerald-50 p-3">
                    {notice}
                </p>
            )}
            {can("billing.payments.collect") && (
                <form
                    className="grid gap-3 sm:grid-cols-2"
                    onSubmit={(event) => {
                        event.preventDefault();
                        if (!amount || !businessDayId) return;
                        const input: BillingPaymentInput = {
                            businessDayId,
                            method,
                            amount,
                            tendered:
                                method === "TIEN_MAT"
                                    ? (tendered ?? undefined)
                                    : undefined,
                            bankReference:
                                method === "CHUYEN_KHOAN"
                                    ? reference
                                    : undefined,
                        };
                        void write("collect", [bill.id, input], (key) =>
                            api.collect(bill.id, input, key),
                        );
                    }}
                >
                    <label>
                        Phương thức
                        <select
                            className="mt-1 min-h-11 w-full rounded border p-2"
                            value={method}
                            disabled={busy}
                            onChange={(event) =>
                                setMethod(
                                    event.target
                                        .value as BillingPaymentInput["method"],
                                )
                            }
                        >
                            <option value="TIEN_MAT">Tiền mặt</option>
                            <option value="CHUYEN_KHOAN">Chuyển khoản</option>
                        </select>
                    </label>
                    <label>
                        Số tiền thu
                        <MoneyInput
                            id="live-payment-amount"
                            value={amount}
                            onChange={setAmount}
                            disabled={busy}
                        />
                    </label>
                    {method === "TIEN_MAT" ? (
                        <label>
                            Khách đưa
                            <MoneyInput
                                id="live-tendered"
                                value={tendered}
                                onChange={setTendered}
                                disabled={busy}
                            />
                        </label>
                    ) : (
                        <label>
                            Mã tham chiếu
                            <input
                                required
                                className="mt-1 min-h-11 w-full rounded border p-2"
                                value={reference}
                                disabled={busy}
                                onChange={(event) =>
                                    setReference(event.target.value)
                                }
                            />
                        </label>
                    )}
                    <div className="flex flex-wrap items-end gap-2">
                        <Button
                            type="button"
                            variant="outline"
                            disabled={busy || due <= 0}
                            onClick={() => {
                                setAmount(due);
                                setTendered(due);
                            }}
                        >
                            Điền số tiền còn lại
                        </Button>
                        <Button
                            disabled={
                                busy ||
                                !businessDayId ||
                                !amount ||
                                amount > due ||
                                (method === "TIEN_MAT" &&
                                    (tendered ?? 0) < amount)
                            }
                        >
                            {busy ? "Đang xử lý…" : "Ghi nhận khoản thu"}
                        </Button>
                    </div>
                </form>
            )}
            <section aria-label="Lịch sử thanh toán">
                <h3 className="font-semibold">Lịch sử thanh toán</h3>
                {bill.payments.length === 0 && <p>Chưa có khoản thu.</p>}
                {bill.payments.map((payment) => (
                    <div
                        key={String(payment.id)}
                        className="flex flex-wrap items-center justify-between gap-2 border-b py-2"
                    >
                        <span>
                            {money(Number(payment.amount))} VND ·{" "}
                            {payment.method === "TIEN_MAT"
                                ? "Tiền mặt"
                                : "Chuyển khoản"}{" "}
                            ·{" "}
                            {payment.status === "THANH_CONG"
                                ? "Thành công"
                                : payment.status === "CHO_XAC_NHAN"
                                  ? "Chờ xác nhận"
                                  : "Đã hủy"}
                        </span>
                        {payment.status === "CHO_XAC_NHAN" &&
                            can("billing.payments.collect") &&
                            businessDayId && (
                                <div className="flex gap-2">
                                    {[true, false].map((confirm) => (
                                        <Button
                                            key={String(confirm)}
                                            variant="outline"
                                            disabled={busy}
                                            onClick={() =>
                                                void write(
                                                    "transfer-" + payment.id,
                                                    [businessDayId, confirm],
                                                    (key) =>
                                                        api.decide(
                                                            bill.id,
                                                            payment.id,
                                                            businessDayId,
                                                            confirm,
                                                            key,
                                                        ),
                                                )
                                            }
                                        >
                                            {confirm
                                                ? "Xác nhận chuyển khoản"
                                                : "Hủy khoản chờ"}
                                        </Button>
                                    ))}
                                </div>
                            )}
                    </div>
                ))}
            </section>
            {can("billing.waivers.approve") && due > 0 && (
                <form
                    className="grid gap-3 border-t pt-3"
                    onSubmit={(event) => {
                        event.preventDefault();
                        if (!waiver || !businessDayId || !reason.trim()) return;
                        const input = {
                            businessDayId,
                            amount: waiver,
                            reason: reason.trim(),
                        };
                        void write("waive", [bill.id, input], (key) =>
                            api.waive(bill.id, input, key),
                        );
                    }}
                >
                    <h3 className="font-semibold">Miễn thu có lý do</h3>
                    <label>
                        Số tiền miễn thu
                        <MoneyInput
                            id="live-waiver"
                            value={waiver}
                            onChange={setWaiver}
                            disabled={busy}
                        />
                    </label>
                    <label>
                        Lý do miễn thu
                        <textarea
                            required
                            className="w-full rounded border p-2"
                            value={reason}
                            disabled={busy}
                            onChange={(event) => setReason(event.target.value)}
                        />
                    </label>
                    <Button
                        variant="outline"
                        disabled={
                            busy || !waiver || waiver > due || !businessDayId
                        }
                    >
                        Xác nhận miễn thu
                    </Button>
                </form>
            )}
        </section>
    );
}
