"use client";

import { VietnamDateField } from "@/components/vietnam-date-field";
import { displayDate, isoDate, vietnamDateTime } from "@/lib/vietnam-date";
import { useCallback, useEffect, useRef, useState } from "react";
import {
    CalendarDays,
    CheckCircle2,
    History,
    RefreshCw,
    Wallet,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { MoneyInput, StatusBadge, money } from "@/components/ops/controls";
import {
    describeBlocker,
    describeError,
    localBusinessDate,
    SalesDayError,
    type BusinessDay,
    type CloseChecklist,
    type DaySnapshot,
    type OpenDayInput,
    type SettlementPreview,
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
    const [checklist, setChecklist] = useState<CloseChecklist | null>(null);
    const [preview, setPreview] = useState<SettlementPreview | null>(null);
    const [snapshot, setSnapshot] = useState<DaySnapshot | null>(null);
    const [history, setHistory] = useState<DaySnapshot[]>([]);
    const [showHistory, setShowHistory] = useState(false);
    const [loading, setLoading] = useState(true);
    const [pending, setPending] = useState(false);
    const [error, setError] = useState("");
    const [date, setDate] = useState(() => displayDate(localBusinessDate()));
    const [cash, setCash] = useState<number | null>(0);
    const [countedCash, setCountedCash] = useState<number | null>(null);
    const [differenceReason, setDifferenceReason] = useState("");
    const [lateClose, setLateClose] = useState(false);
    const [lateReason, setLateReason] = useState("");
    const [refresh, setRefresh] = useState(0);
    const submission = useRef<{ input: OpenDayInput; key: string } | null>(
        null,
    );
    const busy = useRef(false);
    const closingDay = useRef<string | null>(null);
    const closeRequest = useRef<{ payload: string; key: string } | null>(null);
    const [currentUnavailable, setCurrentUnavailable] = useState(false);

    const loadCurrent = useCallback(
        async (signal?: AbortSignal) => {
            setCurrentUnavailable(true);
            setChecklist(null);
            setPreview(null);
            const value = await api.current(signal);
            if (signal?.aborted) return;
            setDay(value);
            setCurrentUnavailable(false);
            const nextDay = value?.status === "CHO_CHOT" ? value.id : null;
            if (closingDay.current !== nextDay) {
                closingDay.current = nextDay;
                setCountedCash(null);
                setDifferenceReason("");
                setLateClose(false);
                setLateReason("");
            }
            if (value && canManage) {
                const next = await api.checklist(value.id, signal);
                if (signal?.aborted) return;
                if (next.status !== value.status) {
                    setCurrentUnavailable(true);
                    throw new SalesDayError(
                        409,
                        "salesday.close.invalid_transition",
                    );
                }
                setChecklist(next);
                if (next.status === "CHO_CHOT") {
                    const settlement = await api.settlementPreview(
                        value.id,
                        signal,
                    );
                    if (!signal?.aborted) setPreview(settlement);
                }
            }
        },
        [api, canManage],
    );
    useEffect(() => {
        const controller = new AbortController();
        setLoading(true);
        loadCurrent(controller.signal)
            .then(() => {
                if (!controller.signal.aborted) {
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
    }, [loadCurrent, refresh]);

    async function open(event: React.FormEvent) {
        event.preventDefault();
        if (
            busy.current ||
            !canManage ||
            !isoDate(date) ||
            cash === null ||
            loading ||
            currentUnavailable ||
            day
        )
            return;
        const input = { businessDate: isoDate(date), openingCash: cash };
        if (
            !submission.current ||
            JSON.stringify(submission.current.input) !== JSON.stringify(input)
        )
            submission.current = { input, key: crypto.randomUUID() };
        busy.current = true;
        setPending(true);
        setError("");
        try {
            await api.open(input, submission.current.key);
            await loadCurrent();
            submission.current = null;
        } catch (e) {
            setError(describeError(e));
            if (e instanceof SalesDayError && e.status < 500)
                submission.current = null;
        } finally {
            busy.current = false;
            setPending(false);
        }
    }
    async function changeClose(cancel: boolean) {
        if (!day || !canManage || currentUnavailable || loading || busy.current)
            return;
        busy.current = true;
        setPending(true);
        setError("");
        try {
            const key = keyFor([day.id, cancel ? "cancel" : "begin"]);
            if (cancel) await api.cancelClose(day.id, key);
            else await api.beginClose(day.id, key);
            closeRequest.current = null;
            await loadCurrent();
        } catch (e) {
            await reconcileConflict(e);
            setError(describeError(e));
            if (e instanceof SalesDayError && e.blockers.length)
                setChecklist((value) =>
                    value ? { ...value, blockers: e.blockers } : value,
                );
        } finally {
            busy.current = false;
            setPending(false);
        }
    }
    function keyFor(input: unknown) {
        const payload = JSON.stringify(input);
        if (closeRequest.current?.payload !== payload) {
            closeRequest.current = { payload, key: crypto.randomUUID() };
        }
        return closeRequest.current.key;
    }
    async function reconcileConflict(error: unknown) {
        if (!(error instanceof SalesDayError) || error.status >= 500) return;
        closeRequest.current = null;
        if (error.status === 409 || error.status === 404) {
            try {
                const closedDay = day?.id;
                await loadCurrent();
                if (closedDay && error.code === "salesday.day.already_closed") {
                    setSnapshot(await api.snapshot(closedDay));
                }
            } catch {
                // Keep the command error visible.
            }
        }
    }
    async function finalize() {
        if (!day || finalizeDisabled || countedCash === null || busy.current)
            return;
        busy.current = true;
        setPending(true);
        setError("");
        try {
            const input = {
                countedCash,
                differenceReason: difference ? differenceReason.trim() : null,
                lateClose,
                lateReason: lateClose ? lateReason.trim() || null : null,
            };
            const result = await api.finalize(
                day.id,
                input,
                keyFor([day.id, input]),
            );
            closeRequest.current = null;
            closingDay.current = null;
            setCountedCash(null);
            setSnapshot(result);
            setShowHistory(false);
            setDay(null);
            setChecklist(null);
            setPreview(null);
        } catch (e) {
            await reconcileConflict(e);
            setError(describeError(e));
            if (e instanceof SalesDayError && e.blockers.length)
                setChecklist((value) =>
                    value ? { ...value, blockers: e.blockers } : value,
                );
        } finally {
            busy.current = false;
            setPending(false);
        }
    }
    async function toggleHistory() {
        if (!showHistory) {
            try {
                setHistory(await api.history());
            } catch (e) {
                setError(describeError(e));
                return;
            }
        }
        setShowHistory((value) => !value);
    }
    const status = day?.status;
    const blockers = preview?.blockers.length
        ? preview.blockers
        : (checklist?.blockers ?? []);
    const difference =
        preview && countedCash !== null
            ? countedCash - Number(preview.expectedCash)
            : null;
    const finalizeDisabled =
        !canManage ||
        day?.status !== "CHO_CHOT" ||
        loading ||
        currentUnavailable ||
        pending ||
        blockers.length > 0 ||
        !preview ||
        countedCash === null ||
        (difference !== 0 && !differenceReason.trim()) ||
        (lateClose && !lateReason.trim());
    return (
        <section className={styles.page}>
            <header className={styles.heading}>
                <div>
                    <p className={styles.eyebrow}>VẬN HÀNH</p>
                    <h1>Ngày bán</h1>
                </div>
                <div className={styles.headerActions}>
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
                    {canManage && (
                        <Button variant="outline" onClick={toggleHistory}>
                            <History size={17} /> Lịch sử
                        </Button>
                    )}
                </div>
            </header>
            <div className={styles.banner} aria-live="polite">
                <CalendarDays size={22} />
                <div>
                    <span className={styles.label}>Trạng thái ngày</span>
                    <div>
                        {loading ? (
                            "Đang tải…"
                        ) : currentUnavailable ? (
                            "Chưa xác minh được trạng thái ngày bán"
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
                        {displayDate(day.businessDate)}
                    </strong>
                )}
            </div>
            {error && (
                <p role="alert" className={styles.error}>
                    {error}
                </p>
            )}
            {snapshot && (
                <section className={styles.success} role="status">
                    <CheckCircle2 size={20} />
                    <div>
                        <strong>Đã chốt ngày bán</strong>
                        <p>
                            Snapshot phiên bản{" "}
                            {String(snapshot.snapshotVersion ?? "mới nhất")} đã
                            được lưu.
                        </p>
                    </div>
                </section>
            )}
            {day && !loading && !currentUnavailable ? (
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
                            {vietnamDateTime.format(new Date(day.openedAt))}
                        </dd>
                    </div>
                </dl>
            ) : !loading && !currentUnavailable && canManage && !error ? (
                <form className={styles.form} onSubmit={open}>
                    <h2>Mở ngày bán</h2>
                    <div className={styles.field}>
                        <VietnamDateField
                            label="Ngày kinh doanh"
                            value={date}
                            disabled={pending}
                            onChange={setDate}
                        />
                    </div>
                    <div className={styles.field}>
                        <label htmlFor="opening-cash">Tiền mặt đầu ngày</label>
                        <MoneyInput
                            id="opening-cash"
                            value={cash}
                            onChange={setCash}
                            disabled={pending}
                        />
                    </div>
                    <Button
                        type="submit"
                        disabled={pending || cash === null || !date}
                    >
                        <Wallet size={17} />
                        {pending ? "Đang mở…" : "Mở ngày bán"}
                    </Button>
                </form>
            ) : null}
            {day &&
                canManage &&
                checklist &&
                !loading &&
                !currentUnavailable &&
                status !== "DA_CHOT" && (
                    <section className={styles.closing}>
                        <div className={styles.sectionHeading}>
                            <div>
                                <p className={styles.eyebrow}>ĐỐI SOÁT</p>
                                <h2>Chốt ngày bán</h2>
                            </div>
                            <StatusBadge
                                tone={blockers.length ? "danger" : "success"}
                            >
                                {blockers.length
                                    ? blockers.length + " điều kiện cần xử lý"
                                    : "Đủ điều kiện"}
                            </StatusBadge>
                        </div>
                        {blockers.length > 0 && (
                            <ul
                                className={styles.blockers}
                                aria-label="Điều kiện chặn"
                            >
                                {blockers.map((item) => (
                                    <li key={item.code}>
                                        {describeBlocker(
                                            item.code,
                                            Number(item.count),
                                        )}
                                    </li>
                                ))}
                            </ul>
                        )}
                        {status === "DANG_BAN" && (
                            <Button
                                disabled={pending || blockers.length > 0}
                                onClick={() => changeClose(false)}
                            >
                                Yêu cầu chốt
                            </Button>
                        )}
                        {status === "CHO_CHOT" && (
                            <>
                                <div className={styles.preview}>
                                    {preview ? (
                                        <>
                                            <div>
                                                <span>Tiền dự kiến</span>
                                                <strong>
                                                    {money(
                                                        Number(
                                                            preview.expectedCash,
                                                        ),
                                                    )}{" "}
                                                    VND
                                                </strong>
                                            </div>
                                            <div>
                                                <span>Doanh thu ròng</span>
                                                <strong>
                                                    {money(
                                                        Number(
                                                            preview.netRevenue,
                                                        ),
                                                    )}{" "}
                                                    VND
                                                </strong>
                                            </div>
                                            <div>
                                                <span>Giá vốn</span>
                                                <strong>
                                                    {preview.cost == null
                                                        ? "Chưa xác định"
                                                        : money(
                                                              Number(
                                                                  preview.cost,
                                                              ),
                                                          ) + " VND"}
                                                </strong>
                                            </div>
                                        </>
                                    ) : (
                                        "Chưa tải được đối soát. Hãy tải lại ngày bán."
                                    )}
                                </div>
                                <div className={styles.field}>
                                    <label htmlFor="counted-cash">
                                        Tiền mặt kiểm đếm
                                    </label>
                                    <MoneyInput
                                        id="counted-cash"
                                        value={countedCash}
                                        onChange={setCountedCash}
                                        disabled={pending}
                                    />
                                </div>
                                {difference !== null && (
                                    <p>
                                        Chênh lệch tiền mặt {money(difference)}{" "}
                                        VND
                                    </p>
                                )}
                                {difference !== null && difference !== 0 && (
                                    <div className={styles.field}>
                                        <label htmlFor="difference-reason">
                                            Lý do chênh lệch
                                        </label>
                                        <textarea
                                            id="difference-reason"
                                            maxLength={1000}
                                            value={differenceReason}
                                            onChange={(e) =>
                                                setDifferenceReason(
                                                    e.target.value,
                                                )
                                            }
                                            disabled={pending}
                                        />
                                    </div>
                                )}
                                <label className={styles.checkbox}>
                                    <input
                                        type="checkbox"
                                        checked={lateClose}
                                        onChange={(e) =>
                                            setLateClose(e.target.checked)
                                        }
                                        disabled={pending}
                                    />{" "}
                                    Chốt trễ
                                </label>
                                {lateClose && (
                                    <div className={styles.field}>
                                        <label htmlFor="late-reason">
                                            Lý do chốt trễ
                                        </label>
                                        <textarea
                                            id="late-reason"
                                            maxLength={1000}
                                            value={lateReason}
                                            onChange={(e) =>
                                                setLateReason(e.target.value)
                                            }
                                            disabled={pending}
                                        />
                                    </div>
                                )}
                                <div className={styles.actions}>
                                    <Button
                                        disabled={finalizeDisabled}
                                        onClick={finalize}
                                    >
                                        Xác nhận chốt
                                    </Button>
                                    <Button
                                        variant="outline"
                                        disabled={pending}
                                        onClick={() => changeClose(true)}
                                    >
                                        Hủy yêu cầu chốt
                                    </Button>
                                </div>
                            </>
                        )}
                    </section>
                )}
            {showHistory && (
                <section className={styles.history}>
                    <h2>Lịch sử ngày đã chốt</h2>
                    {history.length === 0 ? (
                        <p>Chưa có snapshot.</p>
                    ) : (
                        <ul>
                            {history.map((item) => (
                                <li
                                    key={
                                        String(item.dayId) +
                                        "-" +
                                        String(item.snapshotVersion)
                                    }
                                >
                                    <strong>
                                        {item.businessDate
                                            ? displayDate(item.businessDate)
                                            : "Không rõ ngày"}
                                    </strong>
                                    <span>
                                        {money(Number(item.netRevenue ?? 0))}{" "}
                                        VND doanh thu ròng
                                    </span>
                                    <small>
                                        {item.closedAt
                                            ? vietnamDateTime.format(
                                                  new Date(item.closedAt),
                                              )
                                            : ""}
                                    </small>
                                </li>
                            ))}
                        </ul>
                    )}
                </section>
            )}
        </section>
    );
}
