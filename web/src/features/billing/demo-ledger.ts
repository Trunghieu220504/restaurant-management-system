// Development fixture only. Not a backend contract or financial system of record.
export type PaymentInput = {
    amount: number;
    method: "cash" | "transfer";
    tendered?: number;
    reference?: string;
};
export type Payment = PaymentInput & {
    id: string;
    change: number;
    status: "pending" | "confirmed" | "cancelled";
};
export type Invoice = {
    id: string;
    lines: { name: string; amount: number }[];
    payments: Payment[];
    waivers: { amount: number; reason: string }[];
};
const sum = (values: number[]) => values.reduce((a, b) => a + b, 0);
export const total = (invoice: Invoice) =>
    sum(invoice.lines.map((line) => line.amount));
export const paid = (invoice: Invoice) =>
    sum(
        invoice.payments
            .filter((p) => p.status === "confirmed")
            .map((p) => p.amount),
    );
export const waived = (invoice: Invoice) =>
    sum(invoice.waivers.map((w) => w.amount));
export const remaining = (invoice: Invoice) =>
    total(invoice) - waived(invoice) - paid(invoice);
export const available = (invoice: Invoice) =>
    remaining(invoice) -
    sum(
        invoice.payments
            .filter((p) => p.status === "pending")
            .map((p) => p.amount),
    );
export interface BillingDemoApi {
    current(): Promise<Invoice>;
    pay(input: PaymentInput, key: string): Promise<Invoice>;
    confirm(id: string, key: string): Promise<Invoice>;
    cancel(id: string, key: string): Promise<Invoice>;
    waive(
        amount: number,
        reason: string,
        key: string,
        permitted: boolean,
    ): Promise<Invoice>;
}
function positive(amount: number) {
    if (!Number.isSafeInteger(amount) || amount <= 0)
        throw new Error("Số tiền phải là số nguyên VND lớn hơn 0.");
}
export function createDemoLedger(): BillingDemoApi {
    const invoice: Invoice = {
        id: "DEMO-001",
        lines: [
            { name: "Cơm gà × 2", amount: 120000 },
            { name: "Trà chanh × 2", amount: 30000 },
        ],
        payments: [],
        waivers: [],
    };
    const replay = new Map<string, { fingerprint: string; result: Invoice }>();
    let queue: Promise<unknown> = Promise.resolve();
    function command(
        key: string,
        payload: unknown,
        apply: () => void,
    ): Promise<Invoice> {
        const run = queue.then(async () => {
            if (!key.trim()) throw new Error("Thiếu mã yêu cầu.");
            const fingerprint = JSON.stringify(payload);
            const previous = replay.get(key);
            if (previous) {
                if (previous.fingerprint !== fingerprint)
                    throw new Error("Mã yêu cầu đã dùng với nội dung khác.");
                return structuredClone(previous.result);
            }
            apply();
            const result = structuredClone(invoice);
            replay.set(key, { fingerprint, result });
            return structuredClone(result);
        });
        queue = run.catch(() => undefined);
        return run;
    }
    function pending(id: string) {
        const payment = invoice.payments.find((p) => p.id === id);
        if (!payment || payment.status !== "pending")
            throw new Error("Khoản thu không còn chờ xác nhận.");
        return payment;
    }
    return {
        current: async () => {
            await queue;
            return structuredClone(invoice);
        },
        pay: (input, key) =>
            command(key, ["pay", input], () => {
                positive(input.amount);
                if (input.amount > available(invoice))
                    throw new Error(
                        "Không được thu vượt số còn phải thu hoặc tiền đang chờ xác nhận.",
                    );
                if (input.method === "cash") {
                    if (
                        !Number.isSafeInteger(input.tendered) ||
                        input.tendered! < input.amount
                    )
                        throw new Error("Tiền khách đưa chưa đủ.");
                } else if (
                    input.method !== "transfer" ||
                    !input.reference?.trim()
                ) {
                    throw new Error("Nhập mã tham chiếu chuyển khoản.");
                }
                invoice.payments.push({
                    ...input,
                    id: `payment-${invoice.payments.length + 1}`,
                    change:
                        input.method === "cash"
                            ? input.tendered! - input.amount
                            : 0,
                    status: input.method === "cash" ? "confirmed" : "pending",
                });
            }),
        confirm: (id, key) =>
            command(key, ["confirm", id], () => {
                pending(id).status = "confirmed";
            }),
        cancel: (id, key) =>
            command(key, ["cancel", id], () => {
                pending(id).status = "cancelled";
            }),
        waive: (amount, reason, key, permitted) =>
            command(key, ["waive", amount, reason, permitted], () => {
                if (!permitted) throw new Error("Bạn không có quyền miễn thu.");
                positive(amount);
                if (!reason.trim()) throw new Error("Nhập lý do miễn thu.");
                if (amount > available(invoice))
                    throw new Error(
                        "Chỉ được miễn phần chưa thu và không có khoản chờ xác nhận.",
                    );
                invoice.waivers.push({ amount, reason: reason.trim() });
            }),
    };
}
