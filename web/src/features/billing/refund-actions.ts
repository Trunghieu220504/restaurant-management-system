type Route = "approve" | "reject" | "pay" | "reconcile";
export type RefundAction = {
    route: Route;
    label: string;
    outcome?: "DANG_CHI" | "THANH_CONG" | "CHO_DOI_SOAT" | "HUY";
};

export function refundActions(
    status: string,
    review: boolean,
    pay: boolean,
): RefundAction[] {
    if (status === "CHO_DUYET" && review)
        return [
            { route: "approve", label: "Duyệt" },
            { route: "reject", label: "Từ chối" },
        ];
    if (!pay) return [];
    if (status === "DA_DUYET" || status === "DANG_CHI")
        return [
            ...(status === "DA_DUYET"
                ? [
                      {
                          route: "pay",
                          label: "Bắt đầu chi",
                          outcome: "DANG_CHI",
                      } as const,
                  ]
                : []),
            { route: "pay", label: "Xác nhận đã chi", outcome: "THANH_CONG" },
            {
                route: "pay",
                label: "Chuyển chờ đối soát",
                outcome: "CHO_DOI_SOAT",
            },
        ];
    if (status === "CHO_DOI_SOAT")
        return [
            {
                route: "reconcile",
                label: "Xác nhận đã chi",
                outcome: "THANH_CONG",
            },
            {
                route: "reconcile",
                label: "Xác nhận chưa chi và hủy",
                outcome: "HUY",
            },
        ];
    return [];
}
