import { authFetch } from "@/lib/auth/auth-client";
import type { components } from "@/lib/api/generated";

type Schema = components["schemas"];
export type BillingInvoice = Schema["InvoiceView"];
export type BillingPaymentInput = {
    businessDayId: string;
    method: "TIEN_MAT" | "CHUYEN_KHOAN";
    amount: number;
    tendered?: number;
    bankReference?: string;
};

export class BillingApiError extends Error {
    constructor(
        readonly status: number,
        readonly code: string,
    ) {
        super(code);
    }
}

function id(value: number | string) {
    const result = Number(value);
    if (!Number.isSafeInteger(result) || result <= 0)
        throw new Error("Invalid invoice id");
    return result;
}

async function json<T>(
    request: typeof authFetch,
    path: string,
    init?: RequestInit,
): Promise<T> {
    const response = await request(path, { ...init, cache: "no-store" });
    if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        throw new BillingApiError(
            response.status,
            typeof problem?.code === "string"
                ? problem.code
                : typeof problem?.title === "string"
                  ? problem.title
                  : "billing.unavailable",
        );
    }
    return response.json();
}

function commandHeaders(key: string) {
    return {
        "Content-Type": "application/json",
        "Idempotency-Key": key,
    };
}

export function createBillingApi(request: typeof authFetch = authFetch) {
    return {
        invoice(invoiceId: number | string) {
            return json<BillingInvoice>(
                request,
                "/api/billing/invoices/" + id(invoiceId),
            );
        },
        invoiceForOrder(orderId: number | string) {
            return json<BillingInvoice>(
                request,
                "/api/billing/orders/" + id(orderId) + "/invoice",
            );
        },
        collect(
            invoiceId: number | string,
            input: BillingPaymentInput,
            key: string,
        ) {
            return json<Schema["MoneyResult"]>(
                request,
                "/api/billing/invoices/" + id(invoiceId) + "/payments",
                {
                    method: "POST",
                    headers: commandHeaders(key),
                    body: JSON.stringify({
                        ...input,
                        tendered: input.tendered ?? null,
                        bankReference: input.bankReference ?? null,
                    } satisfies Schema["CollectPayment"]),
                },
            );
        },
        decide(
            invoiceId: number | string,
            paymentId: number | string,
            businessDayId: string,
            confirm: boolean,
            key: string,
        ) {
            return json<Schema["MoneyResult"]>(
                request,
                "/api/billing/invoices/" +
                    id(invoiceId) +
                    "/payments/" +
                    id(paymentId) +
                    "/" +
                    (confirm ? "confirm" : "cancel"),
                {
                    method: "POST",
                    headers: commandHeaders(key),
                    body: JSON.stringify({
                        businessDayId,
                    } satisfies Schema["TransferDecision"]),
                },
            );
        },
        waive(
            invoiceId: number | string,
            input: { businessDayId: string; amount: number; reason: string },
            key: string,
        ) {
            return json<Schema["MoneyResult"]>(
                request,
                "/api/billing/invoices/" + id(invoiceId) + "/waivers",
                {
                    method: "POST",
                    headers: commandHeaders(key),
                    body: JSON.stringify(
                        input satisfies Schema["WaiveBalance"],
                    ),
                },
            );
        },
    };
}
