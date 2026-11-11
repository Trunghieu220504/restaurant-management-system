"use client";
import { useEffect, useRef, useState } from "react";
import { CalendarDays, RefreshCw, Wallet } from "lucide-react";
import { Button } from "@/components/ui/button";
import { MoneyInput, StatusBadge, money } from "@/components/ops/controls";
import {
    describeError,
    localBusinessDate,
    SalesDayError,
    type BusinessDay,
    type OpenDayInput,
    type SalesDayApi,
} from "./api";
import styles from "./sales-day.module.css";

export function SalesDayScreen({
    api,
    canManage,
}: {
    api: SalesDayApi;
    canManage: boolean;
}) {
    const [day, setDay] = useState<BusinessDay | null>(null);
    const [loading, setLoading] = useState(true);
    const [pending, setPending] = useState(false);
    const [error, setError] = useState("");
    const [date, setDate] = useState(() => localBusinessDate());
    const [cash, setCash] = useState<number | null>(0);
    const [refresh, setRefresh] = useState(0);
    const submission = useRef<{ input: OpenDayInput; key: string } | null>(
        null,
    );
    const busy = useRef(false);
    useEffect(() => {
        const controller = new AbortController();
        api.current(controller.signal)
            .then((value) => {
                if (!controller.signal.aborted) {
                    setDay(value);
                    setError("");
                    setLoading(false);
                }
            })
            .catch((e) => {
                if (!controller.signal.aborted) {
                    setError(describeError(e));
                    setLoading(false);
                }
            });
        return () => controller.abort();
    }, [api, refresh]);
    async function open(event: React.FormEvent) {
        event.preventDefault();
        if (
            busy.current ||
            !canManage ||
            !date ||
            cash === null ||
            loading ||
            day
        )
            return;
        const input = { businessDate: date, openingCash: cash };
        if (
            !submission.current ||
            JSON.stringify(submission.current.input) !== JSON.stringify(input)
        )
            submission.current = { input, key: crypto.randomUUID() };
        busy.current = true;
        setPending(true);
        setError("");
        try {
            setDay(await api.open(input, submission.current.key));
            submission.current = null;
        } catch (e) {
            setError(describeError(e));
            if (e instanceof SalesDayError && e.status < 500)
                submission.current = null;
            if (e instanceof SalesDayError && e.status === 409) {
                try {
                    setDay(await api.current());
                } catch {
                    /* Keep the original command error. */
                }
            }
        } finally {
            busy.current = false;
            setPending(false);
        }
    }
    const status = day?.status;
    return (
        <section className={styles.page}>
            <header className={styles.heading}>
                <div>
                    <p className={styles.eyebrow}>VẬN HÀNH</p>
                    <h1>Ngày bán</h1>
                </div>
                <Button
                    variant="outline"
                    size="icon"
                    aria-label="Tải lại ngày bán"
                    title="Tải lại ngày bán"
                    disabled={pending || loading}
                    onClick={() => {
                        setLoading(true);
                        setRefresh((v) => v + 1);
                    }}
                >
                    <RefreshCw size={17} />
                </Button>
            </header>
            <div className={styles.banner} aria-live="polite">
                <CalendarDays size={22} />
                <div>
                    <span className={styles.label}>Trạng thái ngày</span>
                    <div>
                        {loading ? (
                            "Đang tải…"
                        ) : status ? (
                            <StatusBadge
                                tone={
                                    status === "DANG_BAN"
                                        ? "success"
                                        : status === "CHO_CHOT"
                                          ? "warning"
                                          : "neutral"
                                }
                            >
                                {status === "DANG_BAN"
                                    ? "ĐANG BÁN"
                                    : status === "CHO_CHOT"
                                      ? "CHỜ CHỐT"
                                      : "ĐÃ CHỐT"}
                            </StatusBadge>
                        ) : (
                            "Chưa có ngày bán đang hoạt động"
                        )}
                    </div>
                </div>
                {day && (
                    <strong className={styles.date}>
                        {day.businessDate.split("-").reverse().join("/")}
                    </strong>
                )}
            </div>
            {error && (
                <p role="alert" className={styles.error}>
                    {error}
                </p>
            )}
            {day && !loading ? (
                <dl className={styles.summary}>
                    <div>
                        <dt>Tiền đầu ngày</dt>
                        <dd>
                            {money(day.openingCash)} <small>VND</small>
                        </dd>
                    </div>
                    <div>
                        <dt>Mở lúc</dt>
                        <dd className={styles.timestamp}>
                            {new Intl.DateTimeFormat("vi-VN", {
                                dateStyle: "short",
                                timeStyle: "short",
                                timeZone: "Asia/Ho_Chi_Minh",
                            }).format(new Date(day.openedAt))}
                        </dd>
                    </div>
                </dl>
            ) : !loading && canManage && !error ? (
                <form className={styles.form} onSubmit={open}>
                    <h2>Mở ngày bán</h2>
                    <label htmlFor="business-date">Ngày kinh doanh</label>
                    <input
                        id="business-date"
                        type="date"
                        required
                        value={date}
                        disabled={pending}
                        onChange={(e) => setDate(e.target.value)}
                    />
                    <label htmlFor="opening-cash">Tiền mặt đầu ngày</label>
                    <MoneyInput
                        id="opening-cash"
                        value={cash}
                        onChange={setCash}
                        disabled={pending}
                    />
                    <Button
                        type="submit"
                        disabled={pending || cash === null || !date}
                    >
                        <Wallet size={17} />
                        {pending ? "Đang mở…" : "Mở ngày bán"}
                    </Button>
                </form>
            ) : null}
            {!canManage && !loading && !day && (
                <p className={styles.notice}>Bạn không có quyền mở ngày bán.</p>
            )}
            {!loading && !day && error && canManage && (
                <Button variant="outline" onClick={() => setError("")}>
                    Thử lại yêu cầu
                </Button>
            )}
        </section>
    );
}
