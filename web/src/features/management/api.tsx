"use client";
import { useEffect, useState } from "react";
import { authFetch } from "@/lib/auth/auth-client";
const messages: Record<string, string> = {
    "reporting.range.invalid": "Ngày kết thúc phải từ ngày bắt đầu trở đi.",
    "identity.account.input_invalid":
        "Kiểm tra tên đăng nhập, email, mật khẩu và vai trò.",
    "identity.account.password_invalid":
        "Mật khẩu chưa đáp ứng yêu cầu của hệ thống.",
    "identity.account.not_found": "Tài khoản này không còn tồn tại.",
    "identity.account.forbidden": "Bạn không có quyền quản trị tài khoản.",
    "identity.account.out_of_scope":
        "Tài khoản nằm ngoài phạm vi quản lý của bạn.",
    "identity.account.role_not_allowed": "Bạn không được cấp vai trò đã chọn.",
    "identity.account.self_change":
        "Bạn không thể thực hiện thay đổi này trên chính tài khoản của mình.",
    "identity.account.user_name_taken": "Tên đăng nhập đã được sử dụng.",
    "identity.account.email_taken": "Email đã được sử dụng.",
};
export async function request(
    url: string,
    init: RequestInit = {},
): Promise<Response> {
    let response: Response;
    try {
        response = await authFetch(url, { cache: "no-store", ...init });
    } catch (e) {
        if (init.signal?.aborted) throw e;
        throw Error("Không kết nối được máy chủ. Vui lòng thử lại.");
    }
    if (!response.ok) {
        const p = await response.json().catch(() => null);
        throw Error(
            messages[p?.code] ??
                {
                    401: "Phiên đăng nhập đã hết hạn.",
                    403: "Bạn không có quyền thực hiện thao tác này.",
                }[response.status] ??
                "Máy chủ chưa xử lý được yêu cầu. Vui lòng thử lại.",
        );
    }
    return response;
}
export function useRead<T>(url: string) {
    const [round, setRound] = useState(0);
    const [result, setResult] = useState<{
        url: string;
        round: number;
        data?: T;
        error?: string;
    }>();
    useEffect(() => {
        const c = new AbortController();
        request(url, { signal: c.signal })
            .then((response) => response.json() as Promise<T>)
            .then(
                (data) => {
                    if (!c.signal.aborted) setResult({ url, round, data });
                },
                (e) => {
                    if (!c.signal.aborted)
                        setResult({ url, round, error: e.message });
                },
            );
        return () => c.abort();
    }, [url, round]);
    const current =
        result?.url === url && result.round === round ? result : undefined;
    return {
        data: current?.data,
        error: current?.error,
        loading: !current,
        reload: () => setRound((n) => n + 1),
    };
}
export function ReadState({
    loading,
    error,
    reload,
}: {
    loading: boolean;
    error?: string;
    reload: () => void;
}) {
    if (loading) return <p role="status">Đang tải dữ liệu…</p>;
    return error ? (
        <div role="alert">
            {error}{" "}
            <button type="button" onClick={reload}>
                Thử lại
            </button>
        </div>
    ) : null;
}
