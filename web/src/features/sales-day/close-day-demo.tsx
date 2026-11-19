"use client";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { MoneyInput, money, StatusBadge } from "@/components/ops/controls";
import { closeBlockers, closeDemo, initialCloseDay } from "./close-demo";

export function CloseDayDemo() {
    const [day, setDay] = useState(initialCloseDay);
    const [error, setError] = useState("");
    function act(action: "request" | "cancel" | "close") {
        try {
            setDay(closeDemo(day, action));
            setError("");
        } catch (e) {
            setError(e instanceof Error ? e.message : "Không thể chốt.");
        }
    }
    const blockers = closeBlockers(day);
    return (
        <section className="max-w-2xl space-y-5">
            <h2 className="text-xl font-semibold">Đóng / chốt ngày</h2>
            <StatusBadge
                tone={day.status === "DA_CHOT" ? "success" : "warning"}
            >
                {day.status === "DANG_BAN"
                    ? "ĐANG BÁN"
                    : day.status === "CHO_CHOT"
                      ? "CHỜ CHỐT"
                      : "ĐÃ CHỐT"}
            </StatusBadge>
            <fieldset
                disabled={day.status === "DA_CHOT"}
                className="grid gap-4"
            >
                <legend className="py-3 font-semibold">
                    Điều kiện và đối soát (mẫu)
                </legend>
                <label className="flex gap-2">
                    <input
                        type="checkbox"
                        checked={day.openOrders > 0}
                        onChange={(e) =>
                            setDay({
                                ...day,
                                openOrders: e.target.checked ? 1 : 0,
                            })
                        }
                    />{" "}
                    Còn đơn mở
                </label>
                <label className="flex gap-2">
                    <input
                        type="checkbox"
                        checked={day.unapprovedCounts > 0}
                        onChange={(e) =>
                            setDay({
                                ...day,
                                unapprovedCounts: e.target.checked ? 1 : 0,
                            })
                        }
                    />{" "}
                    Số đếm chưa duyệt
                </label>
                <p>
                    Tiền mặt dự kiến:{" "}
                    <strong>{money(day.expectedCash)} VND</strong>
                </p>
                <label htmlFor="counted-cash">Tiền mặt kiểm đếm</label>
                <MoneyInput
                    id="counted-cash"
                    value={day.countedCash}
                    onChange={(countedCash) => setDay({ ...day, countedCash })}
                />
                <p>
                    Chênh lệch:{" "}
                    {money(
                        (day.countedCash ?? day.expectedCash) -
                            day.expectedCash,
                    )}{" "}
                    VND
                </p>
                <label htmlFor="close-reason">Lý do chênh lệch</label>
                <textarea
                    id="close-reason"
                    className="rounded border p-2"
                    value={day.reason}
                    onChange={(e) => setDay({ ...day, reason: e.target.value })}
                />
            </fieldset>
            {day.status !== "DA_CHOT" && blockers.length > 0 && (
                <ul
                    className="list-disc space-y-2 pl-5 text-red-700"
                    aria-label="Điều kiện chặn"
                >
                    {blockers.map((reason) => (
                        <li key={reason}>{reason}</li>
                    ))}
                </ul>
            )}
            {error && <p role="alert">{error}</p>}
            <div className="flex flex-wrap gap-2">
                {day.status === "DANG_BAN" && (
                    <Button onClick={() => act("request")}>Yêu cầu chốt</Button>
                )}
                {day.status === "CHO_CHOT" && (
                    <>
                        <Button
                            disabled={blockers.length > 0}
                            onClick={() => act("close")}
                        >
                            Xác nhận chốt
                        </Button>
                        <Button variant="outline" onClick={() => act("cancel")}>
                            Hủy yêu cầu chốt
                        </Button>
                    </>
                )}
                {day.status === "DA_CHOT" && (
                    <p role="status">Ngày mẫu đã chốt. Dữ liệu chỉ ở bộ nhớ.</p>
                )}
                <Button
                    variant="outline"
                    onClick={() => {
                        setDay(initialCloseDay());
                        setError("");
                    }}
                >
                    Đặt lại mẫu
                </Button>
            </div>
        </section>
    );
}
