"use client";

import { useState } from "react";

type SeatMode = "shared" | "private";
type OrderFlow = "auto_complete" | "auto_confirm" | "none";

export default function SettingsPage() {
    // --- Loại hình quán ---
    const [seatMode, setSeatMode] = useState<SeatMode>("private");

    // --- Luồng vận hành ---
    const [orderFlow, setOrderFlow] = useState<OrderFlow>("none");
    const [autoResetDish, setAutoResetDish] = useState(false);
    const [showServingTab, setShowServingTab] = useState(true);
    const [enableRestaurantSettings, setEnableRestaurantSettings] =
        useState(true);
    const [prepayment, setPrepayment] = useState(true);
    const [reminderDelay, setReminderDelay] = useState("15.0");
    const [defaultDiscount, setDefaultDiscount] = useState("0.0");

    // --- Thanh toán ---
    const [multiPayment, setMultiPayment] = useState(true);
    const [skipCashEntry, setSkipCashEntry] = useState(false);

    return (
        <div className="min-h-screen bg-gray-50 p-4 md:p-6">
            <h1 className="text-xl font-semibold text-gray-800 mb-6">
                Cài đặt
            </h1>

            {/* ── Loại hình quán ── */}
            <Section title="Loại hình quán:" required>
                <RadioOption
                    label="Khách ngồi chung bàn (cùng 1 bàn có nhiều khách)"
                    checked={seatMode === "shared"}
                    onChange={() => setSeatMode("shared")}
                />
                <RadioOption
                    label="Khách không ngồi chung bàn"
                    checked={seatMode === "private"}
                    onChange={() => setSeatMode("private")}
                />
            </Section>

            {/* ── Luồng vận hành ── */}
            <Section title="Luồng vận hành:">
                <RadioOption
                    label="Tự động hoàn thành order ngay sau khi khách đặt"
                    checked={orderFlow === "auto_complete"}
                    onChange={() => setOrderFlow("auto_complete")}
                />
                <RadioOption
                    label="Tự động xác nhận order ngay sau khi khách đặt"
                    checked={orderFlow === "auto_confirm"}
                    onChange={() => setOrderFlow("auto_confirm")}
                />

                <div className="mt-2 space-y-3">
                    <CheckboxOption
                        label="Tự động bật món đã hết vào đầu ngày"
                        checked={autoResetDish}
                        onChange={setAutoResetDish}
                    />
                    <CheckboxOption
                        label="Hiển thị tab Đang phục vụ ở QL Order"
                        checked={showServingTab}
                        onChange={setShowServingTab}
                    />
                    <CheckboxOption
                        label="Bật cài đặt nhà hàng"
                        checked={enableRestaurantSettings}
                        onChange={setEnableRestaurantSettings}
                    />
                    <CheckboxOption
                        label="Thanh toán đơn trước cho nhà hàng"
                        checked={prepayment}
                        onChange={setPrepayment}
                    />
                </div>

                {/* Reminder delay */}
                <div className="mt-4 flex flex-col sm:flex-row sm:items-center gap-2">
                    <span className="text-sm text-gray-700 flex-1">
                        Nhắc lại thông báo khi có món chờ xác nhận lâu
                    </span>
                    <div className="flex items-center gap-1">
                        <input
                            type="number"
                            value={reminderDelay}
                            onChange={(e) => setReminderDelay(e.target.value)}
                            className="w-20 border border-gray-300 rounded px-2 py-1 text-sm text-right focus:outline-none focus:ring-1 focus:ring-red-400"
                        />
                        <span className="text-sm text-gray-500">s</span>
                    </div>
                </div>

                {/* Default discount */}
                <div className="mt-3 flex flex-col sm:flex-row sm:items-center gap-2">
                    <span className="text-sm text-gray-700 flex-1">
                        Giảm giá mặc định cho nhà hàng
                    </span>
                    <div className="flex items-center gap-1">
                        <input
                            type="number"
                            value={defaultDiscount}
                            onChange={(e) => setDefaultDiscount(e.target.value)}
                            className="w-20 border border-gray-300 rounded px-2 py-1 text-sm text-right focus:outline-none focus:ring-1 focus:ring-red-400"
                        />
                        <span className="text-sm text-gray-500">%</span>
                    </div>
                </div>
            </Section>

            {/* ── Thanh toán ── */}
            <Section title="Cài đặt thanh toán cho quán:">
                <CheckboxOption
                    label="Cho phép thanh toán theo nhiều phương thức"
                    checked={multiPayment}
                    onChange={setMultiPayment}
                />
                <CheckboxOption
                    label="Không nhập tiền khách đưa khi thanh toán"
                    checked={skipCashEntry}
                    onChange={setSkipCashEntry}
                />
            </Section>
        </div>
    );
}

/* ─────────────────────────── helpers ─────────────────────────── */

function Section({
    title,
    required,
    children,
}: {
    title: string;
    required?: boolean;
    children: React.ReactNode;
}) {
    return (
        <div className="mb-6 bg-white rounded-lg shadow-sm p-4">
            <p className="text-sm font-semibold text-gray-700 mb-3">
                {required && <span className="text-red-500 mr-1">*</span>}
                {title}
            </p>
            <div className="space-y-3">{children}</div>
        </div>
    );
}

function RadioOption({
    label,
    checked,
    onChange,
}: {
    label: string;
    checked: boolean;
    onChange: () => void;
}) {
    return (
        <label className="flex items-center gap-3 cursor-pointer select-none">
            {/* custom radio */}
            <span
                onClick={onChange}
                className={`w-5 h-5 rounded-full border-2 flex items-center justify-center shrink-0 transition-colors ${
                    checked ? "border-red-500" : "border-gray-400"
                }`}
            >
                {checked && (
                    <span className="w-2.5 h-2.5 rounded-full bg-red-500 block" />
                )}
            </span>
            <span className="text-sm text-gray-700">{label}</span>
        </label>
    );
}

function CheckboxOption({
    label,
    checked,
    onChange,
}: {
    label: string;
    checked: boolean;
    onChange: (v: boolean) => void;
}) {
    return (
        <label className="flex items-center gap-3 cursor-pointer select-none">
            <span
                onClick={() => onChange(!checked)}
                className={`w-5 h-5 rounded border-2 flex items-center justify-center shrink-0 transition-colors ${
                    checked
                        ? "bg-blue-500 border-blue-500"
                        : "border-gray-400 bg-white"
                }`}
            >
                {checked && (
                    <svg
                        className="w-3 h-3 text-white"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke="currentColor"
                        strokeWidth={3}
                    >
                        <path
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            d="M5 13l4 4L19 7"
                        />
                    </svg>
                )}
            </span>
            <span className="text-sm text-gray-700">{label}</span>
        </label>
    );
}
