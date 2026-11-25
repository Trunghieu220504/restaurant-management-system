import { expect, it } from "vitest";
import { refundActions } from "./refund-actions";

it("limits review and payout to permitted actors and backend transitions", () => {
    expect(refundActions("CHO_DUYET", false, true)).toEqual([]);
    expect(
        refundActions("CHO_DUYET", true, false).map((action) => action.route),
    ).toEqual(["approve", "reject"]);
    expect(refundActions("DA_DUYET", true, false)).toEqual([]);
    expect(
        refundActions("DA_DUYET", false, true).map((action) => action.outcome),
    ).toEqual(["DANG_CHI", "THANH_CONG", "CHO_DOI_SOAT"]);
    expect(
        refundActions("DANG_CHI", false, true).map((action) => action.outcome),
    ).toEqual(["THANH_CONG", "CHO_DOI_SOAT"]);
    expect(
        refundActions("CHO_DOI_SOAT", false, true).map(({ route, outcome }) => [
            route,
            outcome,
        ]),
    ).toEqual([
        ["reconcile", "THANH_CONG"],
        ["reconcile", "HUY"],
    ]);
    for (const status of ["THANH_CONG", "TU_CHOI", "HUY"])
        expect(refundActions(status, true, true)).toEqual([]);
});
