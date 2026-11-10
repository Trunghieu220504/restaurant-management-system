"use client";
import { useEffect, useState, type ReactNode } from "react";
import { Minus, Plus, Printer } from "lucide-react";
import { Button } from "@/components/ui/button";
import styles from "./ops.module.css";
export const money = (amount: number) =>
    new Intl.NumberFormat("vi-VN").format(amount);
export function StatusBadge({
    children,
    tone = "neutral",
}: {
    children: ReactNode;
    tone?: "neutral" | "success" | "warning" | "danger";
}) {
    return (
        <span className={`${styles.badge} ${styles[tone]}`}>{children}</span>
    );
}
export function elapsedSeconds(since: string, now: number) {
    const timestamp = Date.parse(since);
    return Number.isFinite(timestamp)
        ? Math.max(0, Math.floor((now - timestamp) / 1000))
        : null;
}
export function WaitingTimer({ since }: { since: string }) {
    const [now, setNow] = useState<number | null>(null);
    useEffect(() => {
        const update = () => setNow(Date.now());
        update();
        const timer = setInterval(update, 1000);
        document.addEventListener("visibilitychange", update);
        return () => {
            clearInterval(timer);
            document.removeEventListener("visibilitychange", update);
        };
    }, [since]);
    const seconds = now === null ? null : elapsedSeconds(since, now);
    return (
        <time
            dateTime={since}
            className={styles.timer}
            aria-label="Thời gian chờ"
        >
            {seconds === null
                ? "--:--"
                : `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`}
        </time>
    );
}
export function parseVnd(raw: string): number | null {
    if (!/^\d+$/.test(raw)) return null;
    const value = Number(raw);
    return Number.isSafeInteger(value) && value >= 0 ? value : null;
}
export function MoneyInput({
    id,
    value,
    onChange,
    disabled = false,
}: {
    id: string;
    value: number | null;
    onChange: (value: number | null) => void;
    disabled?: boolean;
}) {
    const [invalid, setInvalid] = useState(false);
    return (
        <div>
            <div className={styles.moneyInput}>
                <input
                    id={id}
                    inputMode="numeric"
                    autoComplete="off"
                    disabled={disabled}
                    aria-invalid={invalid}
                    aria-describedby={invalid ? `${id}-error` : undefined}
                    value={value === null ? "" : money(value)}
                    onChange={(event) => {
                        if (
                            event.target.value.includes(".") &&
                            !/^\d{1,3}(\.\d{3})+$/.test(event.target.value)
                        ) {
                            setInvalid(true);
                            onChange(null);
                            return;
                        }
                        const raw = event.target.value.replace(/\./g, "");
                        const parsed = parseVnd(raw);
                        if (raw !== "" && parsed === null) {
                            setInvalid(true);
                            onChange(null);
                            return;
                        }
                        setInvalid(false);
                        onChange(parsed);
                    }}
                />
                <span>VND</span>
            </div>
            {invalid && (
                <p id={`${id}-error`} role="alert" className={styles.error}>
                    Chỉ nhập số nguyên không âm trong giới hạn an toàn.
                </p>
            )}
        </div>
    );
}
export function QuantityStepper({
    value,
    onChange,
    min = 1,
    max = 99,
}: {
    value: number;
    onChange: (value: number) => void;
    min?: number;
    max?: number;
}) {
    return (
        <div className={styles.stepper}>
            <Button
                type="button"
                size="icon"
                variant="outline"
                aria-label="Giảm số lượng"
                title="Giảm số lượng"
                disabled={value <= min}
                onClick={() => onChange(Math.max(min, value - 1))}
            >
                <Minus size={16} />
            </Button>
            <input
                aria-label="Số lượng"
                type="number"
                min={min}
                max={max}
                step={1}
                value={value}
                onChange={(event) => {
                    const n = event.target.valueAsNumber;
                    if (Number.isInteger(n) && n >= min && n <= max)
                        onChange(n);
                }}
            />
            <Button
                type="button"
                size="icon"
                variant="outline"
                aria-label="Tăng số lượng"
                title="Tăng số lượng"
                disabled={value >= max}
                onClick={() => onChange(Math.min(max, value + 1))}
            >
                <Plus size={16} />
            </Button>
        </div>
    );
}
export function Receipt({
    number,
    lines,
    total,
}: {
    number: string;
    lines: { name: string; amount: number }[];
    total: number;
}) {
    return (
        <section className={styles.receiptSection}>
            <Button
                type="button"
                variant="outline"
                onClick={() => window.print()}
            >
                <Printer size={16} />
                In chứng từ
            </Button>
            <article className={styles.receipt} aria-label="Chứng từ">
                <h3>PHIẾU THU</h3>
                <p>{number}</p>
                <hr />
                {lines.map((line, i) => (
                    <div className={styles.receiptLine} key={i}>
                        <span>{line.name}</span>
                        <span>{money(line.amount)}</span>
                    </div>
                ))}
                <hr />
                <div className={styles.receiptLine}>
                    <strong>Tổng cộng</strong>
                    <strong>{money(total)} VND</strong>
                </div>
            </article>
        </section>
    );
}
