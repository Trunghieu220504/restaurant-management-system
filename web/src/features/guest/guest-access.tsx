import { useEffect, useRef, useState } from "react";

const errors: Record<string, string> = {
    "qr.code.invalid": "Mã tham gia không hợp lệ hoặc phiên bàn đã đóng.",
    "qr.code.expired": "Mã tham gia đã hết hạn. Hãy xin mã mới từ nhân viên.",
    "qr.code.used": "Mã tham gia đã được dùng. Hãy xin mã mới từ nhân viên.",
    "qr.token.expired": "Quyền xem phiên đã hết hạn. Hãy quét mã mới.",
    "qr.token.revoked":
        "Phiên bàn đã đóng hoặc quyền truy cập đã được thu hồi.",
    "qr.token.invalid": "Quyền truy cập không còn hợp lệ. Hãy quét mã mới.",
};
async function guestRequest(path: string, init: RequestInit) {
    const response = await fetch("/api/public/guest-access/" + path, {
        ...init,
        cache: "no-store",
        credentials: "omit",
        referrerPolicy: "no-referrer",
    });
    if (response.status === 429) {
        const wait = Number(response.headers.get("Retry-After") ?? 60);
        throw new LimitError(Number.isFinite(wait) ? Math.max(1, wait) : 60);
    }
    if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        throw Error(
            errors[problem.code] ??
                "Chưa xác minh được phiên. Hãy quét lại mã.",
        );
    }
    return response.json();
}
class LimitError extends Error {
    constructor(readonly seconds: number) {
        super(`Quá nhiều yêu cầu. Vui lòng chờ ${seconds} giây.`);
    }
}

export function GuestAccessStatus({
    tableCode,
    onToken,
}: {
    tableCode: string;
    onToken: (token: string | null) => void;
}) {
    const [message, setMessage] = useState("Thực đơn công khai");
    const join = useRef<string | null>(null);
    const exchange = useRef<Promise<{
        token: string;
        expiresAt: string;
    }> | null>(null);
    useEffect(() => {
        if (location.hash) {
            join.current = new URLSearchParams(location.hash.slice(1)).get("j");
            history.replaceState(
                history.state,
                "",
                location.pathname + location.search,
            );
        }
        if (!join.current) return;
        let active = true,
            timer: ReturnType<typeof setTimeout>,
            expiryTimer: ReturnType<typeof setTimeout>;
        let token = "";
        async function check() {
            try {
                const state = await guestRequest("session", {
                    headers: { Authorization: "Guest " + token },
                });
                if (active) {
                    onToken(state.canSubmitOrders === true ? token : null);
                    setMessage("Đã vào phiên · " + state.tableName);
                    timer = setTimeout(check, 20000);
                }
            } catch (e) {
                if (active) {
                    onToken(null);
                    setMessage(
                        e instanceof Error
                            ? e.message
                            : "Mất kết nối với phiên bàn.",
                    );
                    if (e instanceof LimitError || e instanceof TypeError)
                        timer = setTimeout(
                            check,
                            e instanceof LimitError ? e.seconds * 1000 : 5000,
                        );
                }
            }
        }
        async function enter() {
            try {
                exchange.current ??= guestRequest("exchange", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ tableCode, joinCode: join.current }),
                });
                const grant = await exchange.current;
                if (!active) return;
                const ms = Date.parse(grant.expiresAt) - Date.now();
                if (!grant.token || !Number.isFinite(ms) || ms <= 0)
                    throw Error(errors["qr.token.expired"]);
                token = grant.token;
                join.current = null;
                exchange.current = null;
                expiryTimer = setTimeout(() => {
                    active = false;
                    clearTimeout(timer);
                    token = "";
                    onToken(null);
                    setMessage(errors["qr.token.expired"]);
                }, ms);
                await check();
            } catch (e) {
                if (!active) return;
                setMessage(
                    e instanceof TypeError
                        ? "Không thể kết nối để đổi mã. Hãy quét lại mã."
                        : e instanceof Error
                          ? e.message
                          : "Không thể đổi mã.",
                );
                if (e instanceof LimitError) {
                    exchange.current = null;
                    timer = setTimeout(enter, e.seconds * 1000);
                } else {
                    join.current = null;
                    exchange.current = null;
                }
            }
        }
        setMessage("Đang kiểm tra mã tham gia…");
        void enter();
        return () => {
            active = false;
            clearTimeout(timer);
            clearTimeout(expiryTimer);
        };
    }, [tableCode, onToken]);
    return (
        <p role="status" className="text-xs">
            {message}
        </p>
    );
}
