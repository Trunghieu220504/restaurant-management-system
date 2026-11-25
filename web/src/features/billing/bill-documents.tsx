"use client";

import { useState } from "react";
import { createPortal } from "react-dom";
import { useQuery } from "@tanstack/react-query";
import { Printer } from "lucide-react";
import { Button } from "@/components/ui/button";
import { vietnamDateTime } from "@/lib/vietnam-date";
import { readOperation } from "@/features/operations/operation-request";
import type { components } from "@/lib/api/generated";

type Schema = components["schemas"];
const labels: Record<string, string> = {
    HOA_DON: "Hóa đơn",
    PHIEU_THU: "Phiếu thu",
    PHIEU_HOAN: "Phiếu hoàn tiền",
};
const vnd = (amount: number | string) =>
    new Intl.NumberFormat("vi-VN").format(
        typeof amount === "string" && /^-?\d+$/.test(amount)
            ? BigInt(amount)
            : Number(amount),
    ) + " VND";

export function BillDocuments({
    documents,
}: {
    documents: Schema["BillDocument"][];
}) {
    const [id, select] = useState("");
    const documentQuery = useQuery({
        queryKey: ["billing", "document", id],
        enabled: Boolean(id),
        queryFn: ({ signal }) =>
            readOperation<Schema["PrintableBill"]>(
                "/api/billing/documents/" + encodeURIComponent(id),
                signal,
            ),
    });
    const bill = documentQuery.data;
    return (
        <section className="grid gap-3 border-t pt-4">
            <h3 className="font-semibold">Chứng từ đã phát hành</h3>
            {documents.length === 0 ? (
                <p>Chưa có chứng từ.</p>
            ) : (
                <label className="grid max-w-lg gap-1">
                    Chọn chứng từ
                    <select
                        className="min-h-11 rounded-md border bg-background px-3"
                        value={id}
                        onChange={(event) => select(event.target.value)}
                    >
                        <option value="">Chọn chứng từ</option>
                        {documents.map((row) => (
                            <option key={row.id} value={row.id}>
                                {labels[row.kind] ?? row.kind} · {row.number}
                            </option>
                        ))}
                    </select>
                </label>
            )}
            {id && documentQuery.isPending && (
                <p role="status">Đang tải chứng từ…</p>
            )}
            {documentQuery.isError && (
                <div>
                    <p role="alert">{documentQuery.error.message}</p>
                    <Button
                        variant="outline"
                        onClick={() => void documentQuery.refetch()}
                    >
                        Tải lại chứng từ
                    </Button>
                </div>
            )}
            {bill && !documentQuery.isError && (
                <>
                    <BillContent bill={bill} />
                    <Button
                        className="min-h-11 justify-self-start"
                        onClick={() => window.print()}
                    >
                        <Printer aria-hidden="true" /> In chứng từ
                    </Button>
                    {createPortal(
                        <div className="hidden print:block" data-billing-print>
                            <BillContent bill={bill} />
                        </div>,
                        document.body,
                    )}
                </>
            )}
        </section>
    );
}

function BillContent({ bill }: { bill: Schema["PrintableBill"] }) {
    const movement = bill.refund ?? bill.payment;
    const reference = bill.refund?.reference ?? bill.payment?.bankReference;
    return (
        <article className="grid max-w-3xl gap-3 break-words border p-4 text-sm print:max-w-none print:border-0 print:p-0">
            <header>
                {bill.storeName && (
                    <p className="font-semibold">{bill.storeName}</p>
                )}
                {bill.storeAddress && <p>{bill.storeAddress}</p>}
                <h4 className="text-lg font-semibold">
                    {labels[bill.kind] ?? bill.kind} · {bill.number}
                </h4>
                <p>
                    Đơn #{bill.orderId} · Hóa đơn {bill.invoiceNumber}
                </p>
                <time dateTime={bill.issued}>
                    {vietnamDateTime.format(new Date(bill.issued))}
                </time>
            </header>
            <table className="w-full border-collapse text-left">
                <thead>
                    <tr className="border-b">
                        <th scope="col">Món</th>
                        <th scope="col" className="text-right">
                            Thành tiền
                        </th>
                    </tr>
                </thead>
                <tbody>
                    {bill.lines.map((line) => (
                        <tr
                            key={line.portionId}
                            className="break-inside-avoid border-b"
                        >
                            <td className="py-1">{line.description}</td>
                            <td className="py-1 text-right">
                                {vnd(line.amount)}
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
            <dl className="grid gap-1">
                {bill.waivers.map((waiver) => (
                    <div key={waiver.id} className="flex justify-between gap-3">
                        <dt>Miễn thu · {waiver.reason}</dt>
                        <dd>{vnd(waiver.amount)}</dd>
                    </div>
                ))}
                <div className="flex justify-between gap-3">
                    <dt>Phải thu</dt>
                    <dd>{vnd(bill.obligation)}</dd>
                </div>
                <div className="flex justify-between gap-3">
                    <dt>Thực thu ròng</dt>
                    <dd>{vnd(bill.netCollected)}</dd>
                </div>
                <div className="flex justify-between gap-3 font-semibold">
                    <dt>Còn phải thu</dt>
                    <dd>{vnd(bill.due)}</dd>
                </div>
                {movement && (
                    <div className="flex justify-between gap-3 font-semibold">
                        <dt>
                            {bill.refund ? "Số tiền hoàn" : "Số tiền thu"} ·{" "}
                            {movement.method === "TIEN_MAT"
                                ? "Tiền mặt"
                                : "Chuyển khoản"}
                        </dt>
                        <dd>{vnd(movement.amount)}</dd>
                    </div>
                )}
                {bill.payment?.tendered != null && (
                    <div className="flex justify-between gap-3">
                        <dt>Khách đưa</dt>
                        <dd>{vnd(bill.payment.tendered)}</dd>
                    </div>
                )}
                {bill.payment?.change != null && (
                    <div className="flex justify-between gap-3">
                        <dt>Tiền thừa</dt>
                        <dd>{vnd(bill.payment.change)}</dd>
                    </div>
                )}
                {reference && (
                    <div>
                        <dt>Mã giao dịch</dt>
                        <dd>{reference}</dd>
                    </div>
                )}
                {bill.refund && (
                    <div>
                        <dt>Lý do hoàn</dt>
                        <dd>{bill.refund.reason}</dd>
                    </div>
                )}
            </dl>
        </article>
    );
}
