"use client";
import { useState } from "react";
import Link from "next/link";
import { KitchenDemo } from "@/features/kitchen/kitchen-demo";
import { BillingDemo } from "@/features/billing/billing-demo";
import { CloseDayDemo } from "@/features/sales-day/close-day-demo";

export function WeekThreeDemo() {
    const [tab, setTab] = useState("kitchen");
    return (
        <main className="mx-auto min-h-screen max-w-7xl space-y-6 px-4 py-6 sm:px-8">
            <header className="space-y-2 border-b pb-5">
                <h1 className="text-2xl font-semibold">Vận hành · Tuần 2–3</h1>
                <p className="text-sm text-amber-900">
                    MÔ PHỎNG · Không ghi dữ liệu hoặc thu tiền thật.
                </p>
                <Link
                    className="inline-block text-sm underline"
                    href="/dev/components"
                >
                    Thành phần tuần 1
                </Link>
            </header>
            <div
                role="tablist"
                aria-label="Nghiệp vụ"
                className="flex flex-wrap gap-1 border-b"
            >
                {[
                    ["kitchen", "Bếp / Bar"],
                    ["closing", "Chốt ngày"],
                    ["billing", "Hóa đơn"],
                ].map(([value, label]) => (
                    <button
                        key={value}
                        role="tab"
                        id={`tab-${value}`}
                        aria-selected={tab === value}
                        aria-controls={`panel-${value}`}
                        tabIndex={tab === value ? 0 : -1}
                        onKeyDown={(event) => {
                            const tabs = ["kitchen", "closing", "billing"];
                            const index = tabs.indexOf(value);
                            const next =
                                event.key === "ArrowRight"
                                    ? tabs[(index + 1) % tabs.length]
                                    : event.key === "ArrowLeft"
                                      ? tabs[
                                            (index + tabs.length - 1) %
                                                tabs.length
                                        ]
                                      : event.key === "Home"
                                        ? tabs[0]
                                        : event.key === "End"
                                          ? tabs[tabs.length - 1]
                                          : null;
                            if (next) {
                                event.preventDefault();
                                setTab(next);
                                document.getElementById(`tab-${next}`)?.focus();
                            }
                        }}
                        className={`min-h-11 border-b-2 px-4 py-2 ${tab === value ? "border-emerald-600 font-semibold text-emerald-800" : "border-transparent"}`}
                        onClick={() => setTab(value)}
                    >
                        {label}
                    </button>
                ))}
            </div>
            <div
                role="tabpanel"
                id="panel-kitchen"
                aria-labelledby="tab-kitchen"
                hidden={tab !== "kitchen"}
            >
                <KitchenDemo />
            </div>
            <div
                role="tabpanel"
                id="panel-closing"
                aria-labelledby="tab-closing"
                hidden={tab !== "closing"}
            >
                <CloseDayDemo />
            </div>
            <div
                role="tabpanel"
                id="panel-billing"
                aria-labelledby="tab-billing"
                hidden={tab !== "billing"}
            >
                <BillingDemo />
            </div>
        </main>
    );
}
