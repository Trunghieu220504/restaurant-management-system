import type { components } from "@/lib/api/generated";

type Schema = components["schemas"];
type DayContract = Schema["BusinessDayResponse"];

export type BusinessDay = Omit<DayContract, "openingCash" | "status"> & {
    openingCash: number;
    status: "DANG_BAN" | "CHO_CHOT" | "DA_CHOT";
};
export type OpenDayInput = Omit<Schema["OpenDayRequest"], "openingCash"> & {
    openingCash: number;
};
export type CloseBlocker = Schema["CloseBlocker"];
export type CloseChecklist = Schema["CloseChecklist"];
export type SettlementPreview = Schema["SettlementPreview"];
export type DaySnapshot = Schema["DaySnapshot"];
export type FinalizeDayInput = Schema["FinalizeDay"];

export function parseDay(raw: unknown): BusinessDay | null {
    if (raw === null) return null;
    if (!raw || typeof raw !== "object")
        throw new Error("Invalid day response");
    const day = raw as Record<string, unknown>;
    if (
        typeof day.openingCash !== "number" &&
        (typeof day.openingCash !== "string" || !/^\d+$/.test(day.openingCash))
    )
        throw new Error("Invalid money");
    const openingCash = Number(day.openingCash);
    if (
        !Number.isSafeInteger(openingCash) ||
        openingCash < 0 ||
        typeof day.id !== "string" ||
        typeof day.businessDate !== "string" ||
        !/^\d{4}-\d{2}-\d{2}$/.test(day.businessDate) ||
        typeof day.openedAt !== "string" ||
        !Number.isFinite(Date.parse(day.openedAt)) ||
        !["DANG_BAN", "CHO_CHOT", "DA_CHOT"].includes(String(day.status))
    )
        throw new Error("Invalid day response");
    return {
        id: day.id,
        businessDate: day.businessDate,
        openingCash,
        openedAt: day.openedAt,
        status: day.status as BusinessDay["status"],
    };
}

export interface SalesDayApi {
    current(signal?: AbortSignal): Promise<BusinessDay | null>;
    open(input: OpenDayInput, key: string): Promise<BusinessDay>;
    checklist(dayId: string, signal?: AbortSignal): Promise<CloseChecklist>;
    beginClose(dayId: string, key: string): Promise<CloseChecklist>;
    cancelClose(dayId: string, key: string): Promise<CloseChecklist>;
    settlementPreview(dayId: string): Promise<SettlementPreview>;
    finalize(
        dayId: string,
        input: FinalizeDayInput,
        key: string,
    ): Promise<DaySnapshot>;
    snapshot(dayId: string): Promise<DaySnapshot | null>;
    history(): Promise<DaySnapshot[]>;
}

export class SalesDayError extends Error {
    constructor(
        public status: number,
        public code: string,
        public blockers: CloseBlocker[] = [],
    ) {
        super(code);
    }
}

export const errorMessages: Record<string, string> = {
    "salesday.idempotency.required":
        "Thiếu mã yêu cầu. Vui lòng tải lại trang.",
    "salesday.input.invalid": "Ngày bán hoặc dữ liệu gửi lên không hợp lệ.",
    "salesday.idempotency.conflict":
        "Mã yêu cầu đã được dùng với nội dung khác.",
    "salesday.previous_not_closed":
        "Đã có ngày bán chưa chốt. Không thể mở thêm ngày.",
    "salesday.date.invalid":
        "Ngày mới phải sau ngày bán trước và chưa từng được mở.",
    "salesday.close.blocked": "Chưa thể chốt vì còn điều kiện chưa hoàn tất.",
    "salesday.close.invalid_transition":
        "Trạng thái ngày bán không cho phép thao tác này.",
    "salesday.cash.reason_required":
        "Cần nhập lý do khi tiền kiểm đếm khác tiền dự kiến.",
    "salesday.day.already_closed": "Ngày bán đã được chốt.",
    "salesday.day.not_found": "Không tìm thấy ngày bán.",
    "salesday.snapshot.not_found": "Chưa có snapshot chốt ngày.",
    "salesday.close.busy": "Ngày bán đang được xử lý. Hãy thử lại.",
};

export function describeBlocker(code: string, count: number) {
    const labels: Record<string, string> = {
        "salesday.open_orders": "đơn hàng chưa đóng",
        "salesday.pending_batches": "lô bán hàng chưa hoàn tất",
        "salesday.pending_events": "sự kiện bán hàng chưa xử lý",
        "salesday.open_sessions": "phiên bàn chưa đóng",
        "salesday.pending_transfers": "giao dịch chuyển khoản chờ xác nhận",
        "salesday.pending_refunds": "yêu cầu hoàn tiền chờ xử lý",
        "salesday.draft_purchases": "phiếu nhập kho còn ở dạng nháp",
        "salesday.unapproved_counts": "số đếm tồn kho chưa duyệt",
        "salesday.pending_fulfillment": "món chưa hoàn tất phục vụ",
        "salesday.pending_adjustments": "điều chỉnh chưa áp dụng",
        "inventory.count.invalid_valuation":
            "dòng tồn kho chưa tính được giá trị",
    };
    return `${count} ${labels[code] ?? code}.`;
}

export function describeError(error: unknown) {
    if (error instanceof SalesDayError) {
        if (error.status === 401)
            return "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.";
        if (error.status === 403) return "Bạn không có quyền quản lý ngày bán.";
        return (
            errorMessages[error.code] ??
            "Không thể xử lý yêu cầu. Vui lòng thử lại."
        );
    }
    return "Không kết nối được máy chủ. Kiểm tra kết nối rồi thử lại cùng yêu cầu.";
}

export function createSalesDayApi(
    getToken: () => string | null,
    request: typeof fetch = fetch,
): SalesDayApi {
    async function send<T>(path: string, init: RequestInit = {}): Promise<T> {
        const token = getToken();
        if (!token) throw new SalesDayError(401, "unauthorized");
        const response = await request(`/api/salesday/${path}`, {
            ...init,
            headers: { Authorization: `Bearer ${token}`, ...init.headers },
            cache: "no-store",
        });
        if (!response.ok) {
            const body = await response.json().catch(() => ({}));
            throw new SalesDayError(
                response.status,
                body.code ?? body.title ?? "unknown",
                Array.isArray(body.blockers) ? body.blockers : [],
            );
        }
        const body = await response.text();
        return (body ? JSON.parse(body) : null) as T;
    }
    return {
        current: async (signal) => parseDay(await send("current", { signal })),
        open: async (input, key) => {
            const day = parseDay(
                await send("open", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "Idempotency-Key": key,
                    },
                    body: JSON.stringify(input),
                }),
            );
            if (!day) throw new Error("Missing opened day");
            return day;
        },
        checklist: (dayId, signal) =>
            send(`${dayId}/close-checklist`, { signal }),
        beginClose: (dayId, key) =>
            send(`${dayId}/begin-close`, {
                method: "POST",
                headers: { "Idempotency-Key": key },
            }),
        cancelClose: (dayId, key) =>
            send(`${dayId}/cancel-close`, {
                method: "POST",
                headers: { "Idempotency-Key": key },
            }),
        settlementPreview: (dayId) => send(`${dayId}/settlement-preview`),
        finalize: (dayId, input, key) =>
            send(`${dayId}/finalize`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Idempotency-Key": key,
                },
                body: JSON.stringify(input),
            }),
        snapshot: (dayId) => send(`${dayId}/snapshot`),
        history: () => send("history"),
    };
}

export function localBusinessDate(now = new Date()) {
    return new Intl.DateTimeFormat("en-CA", {
        timeZone: "Asia/Ho_Chi_Minh",
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
    }).format(now);
}
