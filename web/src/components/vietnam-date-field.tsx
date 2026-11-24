"use client";
import { useEffect, useId, useRef, useState } from "react";
import { CalendarDays, ChevronLeft, ChevronRight } from "lucide-react";
import { displayDate, isoDate } from "@/lib/vietnam-date";
import { localBusinessDate } from "@/features/sales-day/api";
import s from "./vietnam-date-field.module.css";

export function VietnamDateField({
    label,
    value,
    onChange,
    min,
    disabled,
}: {
    label: string;
    value: string;
    onChange: (text: string) => void;
    min?: string;
    disabled?: boolean;
}) {
    const id = useId(),
        dialog = useRef<HTMLDialogElement>(null),
        input = useRef<HTMLInputElement>(null);
    const [month, setMonth] = useState(() => localBusinessDate().slice(0, 7));
    const selected = isoDate(value);
    const message = !selected
        ? "Nhập ngày hợp lệ theo dạng ngày/tháng/năm (dd/MM/yyyy)."
        : min && selected < min
          ? "Ngày kết thúc phải từ ngày bắt đầu trở đi."
          : "";
    useEffect(() => {
        input.current?.setCustomValidity(message);
    }, [message]);
    const first = new Date(month + "-01T00:00:00Z");
    const monthDate = (offset: number, day: number) => {
        const date = new Date(first);
        date.setUTCMonth(date.getUTCMonth() + offset, day);
        return date;
    };
    const count = monthDate(1, 0).getUTCDate();
    function open() {
        setMonth((selected || min || localBusinessDate()).slice(0, 7));
        dialog.current?.showModal();
    }
    return (
        <div className={s.field}>
            <label htmlFor={id}>{label}</label>
            <div className={s.entry}>
                <input
                    ref={input}
                    id={id}
                    disabled={disabled}
                    value={value}
                    required
                    placeholder="dd/MM/yyyy"
                    maxLength={10}
                    autoComplete="off"
                    onChange={(e) => onChange(e.target.value)}
                />
                <button
                    className={s.trigger}
                    type="button"
                    disabled={disabled}
                    aria-label={"Mở lịch: " + label}
                    title={"Mở lịch: " + label}
                    onClick={open}
                >
                    <CalendarDays size={17} />
                </button>
            </div>
            <dialog
                ref={dialog}
                aria-label={"Chọn ngày: " + label}
                className={s.calendar}
            >
                <div className={s.month}>
                    <button
                        type="button"
                        aria-label="Tháng trước"
                        disabled={month === "0001-01"}
                        onClick={() =>
                            setMonth(monthDate(-1, 1).toISOString().slice(0, 7))
                        }
                    >
                        <ChevronLeft size={18} />
                    </button>
                    <strong aria-live="polite">
                        Tháng {first.getUTCMonth() + 1} năm{" "}
                        {first.getUTCFullYear()}
                    </strong>
                    <button
                        type="button"
                        aria-label="Tháng sau"
                        disabled={month === "9999-12"}
                        onClick={() =>
                            setMonth(monthDate(1, 1).toISOString().slice(0, 7))
                        }
                    >
                        <ChevronRight size={18} />
                    </button>
                </div>
                <div className={s.days}>
                    {["T2", "T3", "T4", "T5", "T6", "T7", "CN"].map((d) => (
                        <span key={d}>{d}</span>
                    ))}
                    {Array.from({ length: count }, (_, i) => {
                        const date =
                            month + "-" + String(i + 1).padStart(2, "0");
                        return (
                            <button
                                type="button"
                                key={date}
                                style={
                                    i === 0
                                        ? {
                                              gridColumnStart:
                                                  ((first.getUTCDay() + 6) %
                                                      7) +
                                                  1,
                                          }
                                        : undefined
                                }
                                disabled={!!min && date < min}
                                aria-pressed={date === selected}
                                aria-label={displayDate(date)}
                                onClick={() => {
                                    onChange(displayDate(date));
                                    dialog.current?.close();
                                }}
                            >
                                {i + 1}
                            </button>
                        );
                    })}
                </div>
                <button type="button" onClick={() => dialog.current?.close()}>
                    Đóng lịch
                </button>
            </dialog>
        </div>
    );
}
