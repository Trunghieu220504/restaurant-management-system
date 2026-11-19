import { expect, test } from "@playwright/test";

for (const width of [390, 1440]) {
    test(`kitchen mock deduplicates and reconnects at ${width}`, async ({
        page,
    }) => {
        await page.setViewportSize({ width, height: 950 });
        await page.goto("/dev/week-three");
        await expect(page.getByRole("status")).toHaveText("Đã kết nối");
        await expect(page.getByRole("listitem")).toHaveCount(1);
        await page
            .getByRole("button", { name: "Phiếu mới", exact: true })
            .click();
        await expect(page.getByRole("listitem")).toHaveCount(2);
        await expect(
            page.getByRole("tabpanel").getByRole("alert"),
        ).toContainText("Âm thanh chưa được cho phép");
        await page.getByRole("button", { name: "Gửi lại sự kiện" }).click();
        await expect(page.getByRole("listitem")).toHaveCount(2);
        await page
            .getByRole("button", { name: "Bắt đầu", exact: true })
            .first()
            .click();
        await page
            .getByRole("button", { name: "Hoàn thành", exact: true })
            .click();
        await expect(
            page
                .getByRole("region", { name: "Sẵn sàng", exact: true })
                .getByRole("listitem"),
        ).toHaveCount(1);
        await page
            .getByRole("button", { name: "Ngắt kết nối", exact: true })
            .click();
        await expect(page.getByRole("status")).toHaveText("Mất kết nối");
        await page
            .getByRole("button", { name: "Kết nối lại", exact: true })
            .click();
        await expect(page.getByRole("status")).toHaveText("Đã kết nối");
        await expect(page.getByRole("listitem")).toHaveCount(2);
        expect(
            await page.evaluate(
                () => document.documentElement.scrollWidth > innerWidth,
            ),
        ).toBe(false);
        await page.screenshot({
            path: `test-results/week-three-kitchen-${width}.png`,
            fullPage: true,
        });
    });
}

test("closing checklist blocks incomplete day and allows cancellation", async ({
    page,
}) => {
    await page.goto("/dev/week-three");
    await page.getByRole("tab", { name: "Chốt ngày" }).click();
    await page
        .getByRole("button", { name: "Yêu cầu chốt", exact: true })
        .click();
    await expect(
        page.getByRole("button", { name: "Xác nhận chốt", exact: true }),
    ).toBeDisabled();
    await page.getByRole("button", { name: "Hủy yêu cầu chốt" }).click();
    await expect(page.getByText("ĐANG BÁN", { exact: true })).toBeVisible();
    await page.getByLabel("Còn đơn mở").uncheck();
    await page.getByLabel("Số đếm chưa duyệt").uncheck();
    await page.getByLabel("Tiền mặt kiểm đếm").fill("1200000");
    await page
        .getByRole("button", { name: "Yêu cầu chốt", exact: true })
        .click();
    await page
        .getByRole("button", { name: "Xác nhận chốt", exact: true })
        .click();
    await expect(page.getByText("ĐÃ CHỐT", { exact: true })).toBeVisible();
});

test("partial payment, pending transfer, waiver and receipt", async ({
    page,
}) => {
    await page.goto("/dev/week-three");
    await page.getByRole("tab", { name: "Hóa đơn", exact: true }).click();
    await page.getByRole("button", { name: "Tải hóa đơn mẫu" }).click();
    await page.getByLabel("Số tiền thu", { exact: true }).fill("50000");
    await page.getByLabel("Khách đưa").fill("100000");
    await page.getByRole("button", { name: "Ghi nhận khoản thu" }).click();
    await expect(
        page.getByRole("region", { name: "Lịch sử thu" }),
    ).toContainText("Thừa 50.000");
    await page.getByLabel("Phương thức").selectOption("transfer");
    await page.getByLabel("Số tiền thu", { exact: true }).fill("50000");
    await page.getByLabel("Mã tham chiếu").fill("BANK-TEST");
    await page.getByRole("button", { name: "Ghi nhận khoản thu" }).click();
    await expect(page.getByText("CHƯA THU ĐỦ", { exact: true })).toBeVisible();
    await page.getByRole("button", { name: "Xác nhận", exact: true }).click();
    await page.getByRole("button", { name: "Miễn thu", exact: true }).click();
    await page.getByLabel("Số tiền miễn").fill("50000");
    await page.getByLabel("Lý do", { exact: true }).fill("Quản lý duyệt mẫu");
    await page.getByRole("button", { name: "Xác nhận miễn thu" }).click();
    await page.getByRole("button", { name: "Đóng", exact: true }).click();
    await expect(
        page.getByText("ĐÃ HOÀN TẤT NGHĨA VỤ", { exact: true }),
    ).toBeVisible();
    await expect(
        page.getByRole("article", { name: "Chứng từ", exact: true }),
    ).toContainText("100.000");
    await page.getByRole("tab", { name: "Chốt ngày" }).click();
    await page.getByRole("tab", { name: "Hóa đơn", exact: true }).click();
    await expect(
        page.getByText("ĐÃ HOÀN TẤT NGHĨA VỤ", { exact: true }),
    ).toBeVisible();
    await page.emulateMedia({ media: "print" });
    await expect(
        page.getByRole("article", { name: "Chứng từ", exact: true }),
    ).toBeVisible();
    await expect(
        page.getByRole("heading", { name: "Vận hành · Tuần 2–3" }),
    ).not.toBeVisible();
    await page.pdf({
        path: "test-results/duy-receipt.pdf",
        printBackground: true,
        preferCSSPageSize: true,
    });
});

test("week-three demo preserves panel state and keyboard tab navigation", async ({
    page,
}) => {
    await page.goto("/dev/week-three");
    const kitchenTab = page.getByRole("tab", { name: "Bếp / Bar" });
    await expect(page.getByRole("status")).toHaveText("Đã kết nối");
    await kitchenTab.focus();
    await page.keyboard.press("ArrowRight");
    await expect(page.getByRole("tab", { name: "Chốt ngày" })).toBeFocused();
    await page.getByLabel("Còn đơn mở").uncheck();
    await page.getByRole("tab", { name: "Hóa đơn", exact: true }).click();
    await page.getByRole("tab", { name: "Chốt ngày" }).click();
    await expect(page.getByLabel("Còn đơn mở")).not.toBeChecked();
});

test("billing and waiver dialog fit a mobile viewport", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/dev/week-three");
    await page.getByRole("tab", { name: "Hóa đơn", exact: true }).click();
    await page.getByRole("button", { name: "Tải hóa đơn mẫu" }).click();
    await page.getByLabel("Số tiền thu", { exact: true }).fill("150001");
    await expect(
        page.getByRole("button", { name: "Ghi nhận khoản thu" }),
    ).toBeDisabled();
    await page.getByRole("button", { name: "Miễn thu", exact: true }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    const box = await dialog.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(390);
    await page.getByLabel("Quyền miễn thu (mô phỏng)").uncheck();
    await expect(
        page.getByRole("button", { name: "Xác nhận miễn thu" }),
    ).toBeDisabled();
    await page.screenshot({
        path: "test-results/week-three-waiver-mobile.png",
        fullPage: true,
    });
    await page.keyboard.press("Escape");
    await expect(dialog).not.toBeVisible();
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth > innerWidth,
        ),
    ).toBe(false);
});
