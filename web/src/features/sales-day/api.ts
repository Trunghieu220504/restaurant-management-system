export type BusinessDay = {
    id: string;
    businessDate: string;
    openingCash: number;
    status: "DANG_BAN" | "CHO_CHOT" | "DA_CHOT";
    openedAt: string;
};
export type OpenDayInput = { businessDate: string; openingCash: number };
export interface SalesDayApi {
    current(signal?: AbortSignal): Promise<BusinessDay | null>;
    open(input: OpenDayInput, key: string): Promise<BusinessDay>;
}
export class SalesDayError extends Error {
    constructor(
        public status: number,
        public code: string,
    ) {
        super(code);
    }
}
export const errorMessages: Record<string, string> = {
    "salesday.idempotency.required":
        "Thiếu mã yêu cầu. Vui lòng tải lại trang.",
    "salesday.input.invalid": "Ngày bán hoặc tiền đầu ngày không hợp lệ.",
    "salesday.idempotency.conflict":
        "Mã yêu cầu đã được dùng với nội dung khác. Hãy kiểm tra ngày hiện tại.",
    "salesday.previous_not_closed":
        "Đã có ngày bán chưa chốt. Không thể mở thêm ngày.",
    "salesday.date.invalid":
        "Ngày mới phải sau ngày bán trước và không được mở lại ngày đã chốt.",
};
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
// Auth owns token storage/refresh. Inject its authenticated fetch here when available.
export function createSalesDayApi(
    getToken: () => string | null,
    request: typeof fetch = fetch,
): SalesDayApi {
    async function send(path: string, init: RequestInit = {}) {
        const token = getToken();
        if (!token) throw new SalesDayError(401, "unauthorized");
        const response = await request(`/api/salesday/${path}`, {
            ...init,
            headers: { Authorization: `Bearer ${token}`, ...init.headers },
            cache: "no-store",
        });
        if (!response.ok) {
            const body = await response.json().catch(() => ({}));
            throw new SalesDayError(response.status, body.code ?? "unknown");
        }
        const body = await response.text();
        return body ? JSON.parse(body) : null;
    }
    return {
        current: (signal) => send("current", { signal }),
        open: (input, key) =>
            send("open", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Idempotency-Key": key,
                },
                body: JSON.stringify(input),
            }),
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
