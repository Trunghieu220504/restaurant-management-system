import { useRef } from "react";
import { useMutation } from "@tanstack/react-query";
import { authFetch } from "@/lib/auth/auth-client";

const errors: Record<string, string> = {
    "salesday.day.not_open": "Ngày bán chưa mở hoặc đang chờ chốt.",
    "billing.invoice.not_found": "Chưa tìm thấy hóa đơn của đơn hàng này.",
    "billing.refund.invalid_transition":
        "Trạng thái hoàn tiền đã thay đổi. Hãy tải lại.",
    "billing.amount.exceeds_available": "Số tiền vượt quá mức có thể hoàn.",
    "billing.input.invalid": "Thông tin hoàn tiền không hợp lệ.",
    "fulfillment.incident.pending": "Phiếu đã có sự cố chờ duyệt.",
    "fulfillment.ticket.concurrency_conflict":
        "Dữ liệu đã thay đổi. Hãy tải lại.",
};

export class OperationError extends Error {
    constructor(
        public status: number,
        code: string,
    ) {
        super(
            errors[code] ??
                (status === 401
                    ? "Phiên đăng nhập đã hết hạn."
                    : status === 403
                      ? "Tài khoản không có quyền thực hiện thao tác này."
                      : status === 409
                        ? "Máy chủ từ chối thao tác do dữ liệu đã thay đổi hoặc không đủ điều kiện."
                        : status >= 500
                          ? "Máy chủ chưa xác nhận kết quả. Hãy thử lại cùng nội dung."
                          : "Không thể xử lý yêu cầu."),
        );
    }
}

export async function readOperation<T>(
    path: string,
    signal?: AbortSignal,
): Promise<T> {
    return send<T>(path, { signal, cache: "no-store" });
}

async function send<T>(path: string, init: RequestInit): Promise<T> {
    const response = await authFetch(path, init);
    if (!response.ok) {
        const problem = await response.json().catch(() => null);
        throw new OperationError(
            response.status,
            problem?.code ?? problem?.title ?? "",
        );
    }
    const body = await response.text();
    return (body ? JSON.parse(body) : null) as T;
}

type Command = {
    path: string;
    body: Record<string, unknown>;
    keyInBody?: boolean;
};

export function useOperationCommand(
    onSuccess: () => Promise<unknown>,
    onConflict: () => Promise<unknown> = onSuccess,
) {
    const inFlight = useRef(false);
    const requests = useRef(new Map<string, string>());
    return useMutation({
        mutationFn: async ({ path, body, keyInBody }: Command) => {
            if (inFlight.current) return false;
            inFlight.current = true;
            const fingerprint = JSON.stringify([path, body]);
            const key =
                requests.current.get(fingerprint) ?? crypto.randomUUID();
            requests.current.set(fingerprint, key);
            try {
                await send(path, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        ...(keyInBody ? {} : { "Idempotency-Key": key }),
                    },
                    body: JSON.stringify(
                        keyInBody ? { ...body, requestKey: key } : body,
                    ),
                });
                requests.current.delete(fingerprint);
                await onSuccess();
                return true;
            } catch (error) {
                if (error instanceof OperationError && error.status < 500) {
                    requests.current.delete(fingerprint);
                    if (error.status === 409) await onConflict();
                }
                throw error;
            } finally {
                inFlight.current = false;
            }
        },
    });
}
