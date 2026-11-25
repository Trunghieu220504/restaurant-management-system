import { authFetch } from "@/lib/auth/auth-client";

export type LiveBatch = {
    id: number;
    number: number;
    status: string;
    total: number;
    portions: {
        id: number;
        menuItemName: string;
        variantName: string;
        optionNames: string[];
        unitPrice: number;
        status: string;
    }[];
};
export type LiveOrder = {
    id: number;
    code: string;
    type: string;
    businessDayId: string;
    sessionId: number | null;
    tableLabel: string | null;
    customerName: string | null;
    batches: LiveBatch[];
};
export async function salesRequest<T>(
    path: string,
    body?: unknown,
    key?: string,
): Promise<T> {
    const response = await authFetch(
        path,
        body === undefined
            ? { cache: "no-store" }
            : {
                  method: "POST",
                  headers: {
                      "Content-Type": "application/json",
                      ...(key ? { "Idempotency-Key": key } : {}),
                  },
                  body: JSON.stringify(body),
              },
    );
    if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        const messages: Record<string, string> = {
            "sales.order.session_has_open_order":
                "Bàn này đã có đơn mở. Tải lại danh sách và chọn đơn đó.",
            "sales.batch.not_pending":
                "Đợt này đã được xử lý. Danh sách sẽ được tải lại.",
            "salesday.day.not_open": "Ngày bán chưa mở hoặc đang chốt.",
            "sales.pricing.changed":
                "Giá món đã thay đổi. Hãy kiểm tra giỏ món trước khi gửi lại.",
            "billing.order.not_settled":
                "Đơn còn món chưa giao hoặc nghĩa vụ chưa xử lý xong.",
        };
        throw new Error(
            messages[problem.code] ??
                (response.status === 403
                    ? "Bạn không có quyền thực hiện thao tác này."
                    : (problem.code ??
                      "Không thể xử lý yêu cầu. Hãy tải lại để kiểm tra trạng thái.")),
        );
    }
    return response.json();
}
