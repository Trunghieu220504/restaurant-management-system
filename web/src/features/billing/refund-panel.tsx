"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { MoneyInput, StatusBadge, money } from "@/components/ops/controls";
import { usePermissions } from "@/components/permission-provider";
import type { components } from "@/lib/api/generated";
import {
    readOperation,
    useOperationCommand,
} from "@/features/operations/operation-request";
import { refundActions, type RefundAction } from "./refund-actions";
import { BillDocuments } from "./bill-documents";

type Schema = components["schemas"];
type Refund = Schema["RefundView"];
const field = "min-h-11 min-w-0 rounded-md border bg-background px-3 py-2";
const labels: Record<string, string> = {
    CHO_DUYET: "Chờ duyệt",
    DA_DUYET: "Đã duyệt",
    DANG_CHI: "Đang chi",
    CHO_DOI_SOAT: "Chờ đối soát",
    THANH_CONG: "Đã hoàn tiền",
    TU_CHOI: "Đã từ chối",
    HUY: "Đã hủy",
};
const methodLabel = (method: string) =>
    method === "TIEN_MAT" ? "Tiền mặt" : "Chuyển khoản";

export function RefundPanel({ orderId }: { orderId?: string }) {
    const [input, setInput] = useState(orderId ?? "");
    const [lookup, setLookup] = useState(
        orderId && /^[1-9][0-9]{0,18}$/.test(orderId) ? orderId : "",
    );
    return (
        <section
            className="grid max-w-3xl gap-5"
            aria-labelledby="refund-title"
        >
            <h1 id="refund-title" className="text-2xl font-semibold">
                Hoàn tiền
            </h1>
            <form
                className="flex flex-wrap items-end gap-2"
                onSubmit={(event) => {
                    event.preventDefault();
                    setLookup(input.trim());
                }}
            >
                <label className="grid gap-1">
                    Mã đơn hàng
                    <input
                        className={field}
                        required
                        inputMode="numeric"
                        pattern="[1-9][0-9]*"
                        maxLength={19}
                        value={input}
                        onChange={(event) => setInput(event.target.value)}
                    />
                </label>
                <Button type="submit" className="min-h-11">
                    Tìm hóa đơn
                </Button>
            </form>
            {lookup && <RefundInvoice key={lookup} orderId={lookup} />}
        </section>
    );
}

function RefundInvoice({ orderId }: { orderId: string }) {
    const { can } = usePermissions();
    const [paymentId, selectPayment] = useState("");
    const [amount, setAmount] = useState<number | null>(null);
    const [reason, setReason] = useState("");
    const [choice, setChoice] = useState<{
        row: Refund;
        action: RefundAction;
    } | null>(null);
    const [reference, setReference] = useState("");
    const [notice, setNotice] = useState("");
    const invoice = useQuery({
        queryKey: ["refund", "invoice", orderId],
        queryFn: ({ signal }) =>
            readOperation<Schema["InvoiceView"]>(
                `/api/billing/orders/${orderId}/invoice`,
                signal,
            ),
    });
    const invoiceId = invoice.data?.id;
    const refunds = useQuery({
        queryKey: ["refund", "list", invoiceId],
        enabled: invoiceId != null,
        queryFn: ({ signal }) =>
            readOperation<Refund[]>(
                `/api/billing/invoices/${invoiceId}/refunds`,
                signal,
            ),
    });
    const day = useQuery({
        queryKey: ["refund", "active-day"],
        queryFn: ({ signal }) =>
            readOperation<Schema["BusinessDayResponse"] | null>(
                "/api/salesday/current",
                signal,
            ),
    });
    const command = useOperationCommand(
        async () => {
            selectPayment("");
            setAmount(null);
            setReason("");
            setChoice(null);
            setNotice("Đã cập nhật yêu cầu hoàn tiền.");
            await Promise.all([
                invoice.refetch(),
                refunds.refetch(),
                day.refetch(),
            ]);
        },
        async () => {
            await Promise.all([
                invoice.refetch(),
                refunds.refetch(),
                day.refetch(),
            ]);
        },
    );
    const activeDayId =
        !day.isError && day.data?.status === "DANG_BAN" ? day.data.id : null;
    const payments =
        invoice.data?.payments.filter(
            (payment) => payment.status === "THANH_CONG",
        ) ?? [];
    const payment = payments.find((row) => String(row.id) === paymentId);
    const selected = refunds.data?.find(
        (row) => String(row.id) === String(choice?.row.id),
    );
    const sameState = selected?.status === choice?.row.status;
    const needsReference =
        choice?.action.outcome === "THANH_CONG" &&
        choice.row.method === "CHUYEN_KHOAN";
    const error = invoice.error ?? refunds.error ?? day.error ?? command.error;
    return (
        <div
            className="grid min-w-0 gap-4"
            aria-busy={command.isPending || invoice.isPending}
        >
            <div className="flex flex-wrap items-center gap-3">
                {invoice.data && (
                    <h2 className="text-lg font-semibold">
                        Hóa đơn {invoice.data.number}
                    </h2>
                )}
                <Button
                    variant="outline"
                    disabled={
                        command.isPending ||
                        invoice.isFetching ||
                        refunds.isFetching ||
                        day.isFetching
                    }
                    onClick={() => {
                        command.reset();
                        void invoice.refetch();
                        if (invoiceId != null) void refunds.refetch();
                        void day.refetch();
                    }}
                >
                    Tải lại
                </Button>
            </div>
            {(invoice.isPending ||
                (invoiceId != null && refunds.isPending)) && (
                <p role="status">Đang tải hóa đơn và yêu cầu hoàn tiền…</p>
            )}
            {error && (
                <p role="alert" className="text-sm text-red-700">
                    {error.message}
                </p>
            )}
            {notice && <p role="status">{notice}</p>}
            {invoice.data && !activeDayId && !day.isPending && !day.isError && (
                <p>Ngày bán đang mở là điều kiện để cập nhật hoàn tiền.</p>
            )}
            {can("billing.refunds.request") &&
                invoice.data &&
                !invoice.isError && (
                    <form
                        className="grid max-w-md gap-2 border-t pt-4"
                        onSubmit={(event) => {
                            event.preventDefault();
                            if (
                                !activeDayId ||
                                !payment ||
                                !amount ||
                                amount > Number(payment.amount) ||
                                !reason.trim() ||
                                command.isPending
                            )
                                return;
                            setNotice("");
                            command.mutate({
                                path: `/api/billing/payments/${payment.id}/refunds`,
                                body: {
                                    businessDayId: activeDayId,
                                    amount,
                                    reason: reason.trim(),
                                } satisfies Schema["RefundRequest"],
                            });
                        }}
                    >
                        <h3 className="font-semibold">Đề nghị hoàn tiền</h3>
                        {payments.length ? (
                            <>
                                <label className="grid gap-1">
                                    Khoản thu đã xác nhận
                                    <select
                                        className={field}
                                        value={paymentId}
                                        disabled={command.isPending}
                                        onChange={(event) =>
                                            selectPayment(event.target.value)
                                        }
                                    >
                                        <option value="">Chọn khoản thu</option>
                                        {payments.map((row) => (
                                            <option key={row.id} value={row.id}>
                                                #{row.id} ·{" "}
                                                {methodLabel(row.method)} ·{" "}
                                                {money(Number(row.amount))} VND
                                            </option>
                                        ))}
                                    </select>
                                </label>
                                <label htmlFor="refund-amount">
                                    Số tiền hoàn
                                </label>
                                <MoneyInput
                                    id="refund-amount"
                                    value={amount}
                                    onChange={setAmount}
                                    disabled={command.isPending}
                                />
                                {payment &&
                                    amount !== null &&
                                    amount > Number(payment.amount) && (
                                        <p className="text-sm text-red-700">
                                            Số tiền hoàn vượt quá khoản thu.
                                        </p>
                                    )}
                                <label className="grid gap-1">
                                    Lý do
                                    <textarea
                                        className={field}
                                        required
                                        maxLength={1000}
                                        value={reason}
                                        disabled={command.isPending}
                                        onChange={(event) =>
                                            setReason(event.target.value)
                                        }
                                    />
                                </label>
                                <Button
                                    className="min-h-11 justify-self-start"
                                    disabled={
                                        !activeDayId ||
                                        !payment ||
                                        !amount ||
                                        amount > Number(payment.amount) ||
                                        !reason.trim() ||
                                        command.isPending
                                    }
                                >
                                    Gửi đề nghị
                                </Button>
                            </>
                        ) : (
                            <p>Hóa đơn chưa có khoản thu đã xác nhận.</p>
                        )}
                    </form>
                )}
            {refunds.data && (
                <section className="grid gap-3 border-t pt-4">
                    <h3 className="font-semibold">Yêu cầu hoàn tiền</h3>
                    {refunds.data.length === 0 && <p>Chưa có yêu cầu.</p>}
                    {refunds.data.map((row) => (
                        <article
                            key={row.id}
                            className="grid gap-2 border-b pb-3"
                        >
                            <div className="flex flex-wrap items-center gap-2">
                                <strong>
                                    #{row.id} · {money(Number(row.amount))} VND
                                </strong>
                                <span>{methodLabel(row.method)}</span>
                                <StatusBadge>
                                    {labels[row.status] ?? row.status}
                                </StatusBadge>
                            </div>
                            <p className="break-words text-sm">{row.reason}</p>
                            {row.reference && (
                                <p className="break-all text-sm">
                                    Mã tham chiếu {row.reference}
                                </p>
                            )}
                            {activeDayId === row.businessDayId &&
                                !refunds.isError && (
                                    <div className="flex flex-wrap gap-2">
                                        {refundActions(
                                            row.status,
                                            can("billing.refunds.approve"),
                                            can("billing.refunds.pay"),
                                        ).map((action) => (
                                            <Button
                                                key={action.label}
                                                variant="outline"
                                                className="min-h-11"
                                                disabled={command.isPending}
                                                onClick={() => {
                                                    command.reset();
                                                    setNotice("");
                                                    setChoice({ row, action });
                                                    setReference(
                                                        row.reference ?? "",
                                                    );
                                                }}
                                            >
                                                {action.label}
                                            </Button>
                                        ))}
                                    </div>
                                )}
                        </article>
                    ))}
                </section>
            )}
            {invoice.data && !invoice.isError && (
                <BillDocuments documents={invoice.data.documents} />
            )}
            {choice && (
                <form
                    className="grid max-w-md gap-2 border-t pt-4"
                    onSubmit={(event) => {
                        event.preventDefault();
                        if (
                            !sameState ||
                            activeDayId !== choice.row.businessDayId ||
                            refunds.isError ||
                            command.isPending
                        )
                            return;
                        command.mutate({
                            path: `/api/billing/refunds/${choice.row.id}/${choice.action.route}`,
                            body: {
                                businessDayId: choice.row.businessDayId,
                                ...(choice.action.outcome
                                    ? {
                                          outcome: choice.action.outcome,
                                          reference: reference.trim() || null,
                                      }
                                    : {}),
                            },
                        });
                    }}
                >
                    <h3 className="font-semibold">
                        {choice.action.label} · yêu cầu #{choice.row.id} ·{" "}
                        {money(Number(choice.row.amount))} VND
                    </h3>
                    {!sameState && (
                        <p role="alert">Yêu cầu đã đổi trạng thái.</p>
                    )}
                    {choice.action.outcome && (
                        <label className="grid gap-1">
                            Mã tham chiếu giao dịch
                            <input
                                className={field}
                                maxLength={150}
                                pattern={"[\\x20-\\x7E]*"}
                                required={needsReference}
                                value={reference}
                                disabled={command.isPending}
                                onChange={(event) =>
                                    setReference(event.target.value)
                                }
                            />
                        </label>
                    )}
                    <div className="flex flex-wrap gap-2">
                        <Button
                            className="min-h-11"
                            disabled={
                                command.isPending ||
                                !sameState ||
                                activeDayId !== choice.row.businessDayId ||
                                refunds.isError ||
                                (needsReference && !reference.trim())
                            }
                        >
                            {command.isPending ? "Đang lưu…" : "Xác nhận"}
                        </Button>
                        <Button
                            type="button"
                            variant="outline"
                            className="min-h-11"
                            disabled={command.isPending}
                            onClick={() => setChoice(null)}
                        >
                            Quay lại
                        </Button>
                    </div>
                </form>
            )}
        </div>
    );
}
