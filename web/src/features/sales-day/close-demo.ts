// Provisional UI fixture, not the pending SalesDay close API contract.
export type CloseDay = {
    status: "DANG_BAN" | "CHO_CHOT" | "DA_CHOT";
    openOrders: number;
    unapprovedCounts: number;
    expectedCash: number;
    countedCash: number | null;
    reason: string;
};
export const initialCloseDay = (): CloseDay => ({
    status: "DANG_BAN",
    openOrders: 1,
    unapprovedCounts: 1,
    expectedCash: 1200000,
    countedCash: null,
    reason: "",
});
export function closeBlockers(day: CloseDay): string[] {
    return [
        ...(day.openOrders ? ["Còn đơn chưa đóng."] : []),
        ...(day.unapprovedCounts ? ["Số đếm kho chưa được duyệt."] : []),
        ...(!Number.isSafeInteger(day.countedCash) || day.countedCash! < 0
            ? ["Chưa nhập tiền mặt kiểm đếm hợp lệ."]
            : []),
        ...(day.countedCash !== null &&
        day.countedCash !== day.expectedCash &&
        !day.reason.trim()
            ? ["Cần lý do chênh lệch tiền mặt."]
            : []),
    ];
}
export function closeDemo(
    day: CloseDay,
    action: "request" | "cancel" | "close",
): CloseDay {
    if (action === "request" && day.status === "DANG_BAN")
        return { ...day, status: "CHO_CHOT" };
    if (action === "cancel" && day.status === "CHO_CHOT")
        return { ...day, status: "DANG_BAN" };
    if (
        action === "close" &&
        day.status === "CHO_CHOT" &&
        closeBlockers(day).length === 0
    )
        return { ...day, status: "DA_CHOT" };
    throw new Error("Chưa đủ điều kiện chuyển trạng thái ngày.");
}
