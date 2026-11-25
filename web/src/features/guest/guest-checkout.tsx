import { useEffect, useRef, useState } from "react";

type Line = {
    menuItemId: string;
    variantId: string;
    quantity: number;
    note: string;
    optionIds: string[];
};
type Receipt = { id: string; status: string };
const statuses: Record<string, string> = {
    CHO_XAC_NHAN: "Chờ xác nhận",
    DA_XAC_NHAN: "Đã xác nhận",
    DA_TU_CHOI: "Đã từ chối",
    DA_HUY: "Đã hủy",
    HUY: "Đã hủy",
    DANG_LAM: "Đang chế biến",
    DA_PHUC_VU: "Đã phục vụ",
};
const endpoint = "/api/public/guest-access/batches";

function waitMs(r: Response) {
    const seconds = Number(r.headers.get("Retry-After") ?? 60);
    return 1000 * (Number.isFinite(seconds) ? Math.max(1, seconds) : 60);
}

export function GuestCheckout({
    token,
    count,
    total,
    lines,
    valid,
    onSent,
    onSoldOut,
    onBusy,
}: {
    token: string | null;
    count: number;
    total: number;
    lines: Line[];
    valid: boolean;
    onSent: () => void;
    onSoldOut: () => void;
    onBusy: (busy: boolean) => void;
}) {
    const [receipt, setReceipt] = useState<Receipt | null>(null);
    const [message, setMessage] = useState("");
    const [tracking, setTracking] = useState("");
    const [busy, setBusy] = useState(false);
    const [until, setUntil] = useState(0);
    const [denied, setDenied] = useState(false);
    const request = useRef<{ body: string; key: string } | null>(null);
    const pending = useRef(false);
    useEffect(() => {
        if (!until) return;
        const timer = setTimeout(
            () => setUntil(0),
            Math.max(0, until - Date.now()),
        );
        return () => clearTimeout(timer);
    }, [until]);
    const id = receipt?.id;
    useEffect(() => {
        if (!token || !id || denied) return;
        const controller = new AbortController();
        let timer: ReturnType<typeof setTimeout>;
        async function poll() {
            let delay = 5000;
            try {
                const res = await fetch(
                    endpoint + "/" + encodeURIComponent(id!),
                    {
                        headers: { Authorization: "Guest " + token },
                        signal: controller.signal,
                        credentials: "omit",
                        cache: "no-store",
                    },
                );
                if (res.status === 401 || res.status === 403) {
                    setDenied(true);
                    setTracking(
                        "Phiên không còn quyền theo dõi. Hãy quét mã mới.",
                    );
                    return;
                }
                if (res.status === 429) {
                    delay = waitMs(res);
                    setTracking("Đang chờ cập nhật theo giới hạn của máy chủ.");
                } else {
                    if (!res.ok) throw Error();
                    const data: Receipt = await res.json();
                    if (data.id !== id || typeof data.status !== "string")
                        throw Error();
                    if (controller.signal.aborted) return;
                    setReceipt(data);
                    setTracking("");
                    if (
                        ["DA_TU_CHOI", "DA_HUY", "HUY", "DA_PHUC_VU"].includes(
                            data.status,
                        )
                    )
                        return;
                }
            } catch {
                if (controller.signal.aborted) return;
                setTracking("Chưa cập nhật được trạng thái. Đang thử lại.");
            }
            if (!controller.signal.aborted) timer = setTimeout(poll, delay);
        }
        void poll();
        return () => {
            controller.abort();
            clearTimeout(timer);
        };
    }, [token, id, denied]);
    async function send() {
        if (!token || !count || !valid || pending.current || until || denied)
            return;
        pending.current = true;
        setBusy(true);
        onBusy(true);
        setMessage("");
        try {
            const body = JSON.stringify({
                lines: lines.flatMap(({ quantity, ...portion }) =>
                    Array.from({ length: quantity }, () => portion),
                ),
            });
            if (request.current?.body !== body)
                request.current = { body, key: crypto.randomUUID() };
            const response = await fetch(endpoint, {
                method: "POST",
                credentials: "omit",
                cache: "no-store",
                headers: {
                    Authorization: "Guest " + token,
                    "Content-Type": "application/json",
                    "Idempotency-Key": request.current.key,
                },
                body,
            });
            if (response.status === 401 || response.status === 403) {
                setDenied(true);
                setMessage(
                    "Phiên không còn hiệu lực. Hãy xin nhân viên mã mới.",
                );
                return;
            }
            if (response.status === 429) {
                setUntil(Date.now() + waitMs(response));
                setMessage(
                    "Quá nhiều yêu cầu. Vui lòng chờ trước khi gửi lại.",
                );
                return;
            }
            const problem =
                response.status === 400
                    ? await response.json().catch(() => null)
                    : null;
            if (
                problem?.code === "sales.batch.line_invalid" ||
                response.status === 422
            ) {
                onSoldOut();
                setMessage(
                    "Thực đơn đã thay đổi. Kiểm tra món, giá và lựa chọn trước khi gửi lại.",
                );
                request.current = null;
                return;
            }
            if (!response.ok) throw Error();
            const data: Receipt = await response.json();
            if (!data.id || typeof data.status !== "string") throw Error();
            setReceipt(data);
            setTracking("");
            request.current = null;
            onSent();
        } catch {
            setMessage(
                "Chưa xác nhận được kết quả. Hãy thử gửi lại cùng giỏ món.",
            );
        } finally {
            pending.current = false;
            setBusy(false);
            onBusy(false);
        }
    }
    return (
        <section
            aria-label="Giỏ và trạng thái gọi món"
            className="sticky bottom-0 z-30 mx-auto max-w-xl space-y-1 rounded-xl bg-white px-4 py-2 shadow-sm"
        >
            {count > 0 && (
                <>
                    <h2 className="font-bold">Giỏ món · {count} phần</h2>
                    <p>Tổng cộng {total.toLocaleString("vi-VN")} đ</p>
                    {!token && (
                        <p>
                            Giỏ tạm · Chưa gửi món. Hãy hỏi nhân viên nếu phiên
                            bàn chưa hỗ trợ gửi món.
                        </p>
                    )}
                    {count > 50 && <p>Mỗi đợt gọi món tối đa 50 phần.</p>}
                    {!valid && (
                        <p>
                            Kiểm tra lại món trong giỏ và số tùy chọn cần chọn.
                        </p>
                    )}
                    <button
                        className="rounded-lg bg-red-700 px-4 py-3 font-semibold text-white disabled:bg-slate-600"
                        disabled={
                            !token ||
                            !valid ||
                            count > 50 ||
                            busy ||
                            !!until ||
                            denied
                        }
                        onClick={send}
                    >
                        {busy ? "Đang gửi…" : "Gửi đợt gọi món"}
                    </button>
                </>
            )}
            {message && <p role="alert">{message}</p>}
            {receipt && token && (
                <p aria-live="polite">
                    Đợt vừa gửi · {statuses[receipt.status] ?? receipt.status}
                </p>
            )}
            {tracking && <p aria-live="polite">{tracking}</p>}
        </section>
    );
}
