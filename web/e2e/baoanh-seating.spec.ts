import { expect, test } from "@playwright/test";

test("catalog preview creates, edits and archives areas and tables", async ({
    page,
}) => {
    await page.goto("/dev/seating");
    const dialog = page.getByRole("dialog");
    await page.getByRole("button", { name: "Thêm khu vực" }).click();
    await dialog.getByLabel("Mã", { exact: true }).fill("SANH");
    await dialog.getByLabel("Tên", { exact: true }).fill("Phòng thử");
    await dialog.getByRole("button", { name: "Lưu", exact: true }).click();
    await expect(dialog.getByRole("alert")).toHaveText(
        "Mã đã được sử dụng. Vui lòng nhập mã khác.",
    );
    await dialog.getByLabel("Mã", { exact: true }).fill("PHONG-THU");
    await dialog.getByRole("button", { name: "Lưu", exact: true }).click();

    await page
        .getByRole("region", { name: "Phòng thử" })
        .getByRole("button", { name: "Sửa khu vực" })
        .click();
    await dialog.getByLabel("Tên", { exact: true }).fill("Phòng riêng");
    await dialog.getByRole("button", { name: "Lưu", exact: true }).click();

    const area = page.getByRole("region", { name: "Phòng riêng" });
    await area.getByRole("button", { name: "Thêm bàn" }).click();
    await dialog.getByLabel("Mã", { exact: true }).fill("B20");
    await dialog.getByLabel("Tên", { exact: true }).fill("Bàn thử");
    await dialog.getByRole("button", { name: "Lưu", exact: true }).click();
    await area
        .getByRole("article", { name: "Bàn thử" })
        .getByRole("button", { name: "Sửa bàn" })
        .click();
    await dialog.getByLabel("Tên", { exact: true }).fill("Bàn riêng");
    await dialog.getByRole("button", { name: "Lưu", exact: true }).click();

    await area.getByRole("button", { name: "Lưu trữ khu vực" }).click();
    await dialog.getByRole("button", { name: "Lưu trữ khu vực" }).click();
    await expect(dialog.getByRole("alert")).toHaveText(
        "Hãy chuyển hoặc lưu trữ các bàn trong khu vực trước.",
    );
    await dialog.getByRole("button", { name: "Hủy" }).click();

    await area
        .getByRole("article", { name: "Bàn riêng" })
        .getByRole("button", { name: "Lưu trữ bàn" })
        .click();
    await dialog.getByRole("button", { name: "Lưu trữ bàn" }).click();
    await expect(area.getByRole("article", { name: "Bàn riêng" })).toHaveCount(
        0,
    );
    await area.getByRole("button", { name: "Lưu trữ khu vực" }).click();
    await dialog.getByRole("button", { name: "Lưu trữ khu vực" }).click();
    await expect(area).toHaveCount(0);
    await page.reload();
    await expect(
        page.getByRole("region", { name: "Sảnh chính" }),
    ).toBeVisible();
    await expect(area).toHaveCount(0);
});

test("staff preview shows open, transfer and close after reload", async ({
    page,
}) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/dev/seating");
    await page.getByLabel("Quyền xem thử").selectOption("staff");
    await expect(
        page.getByRole("button", { name: "Thêm khu vực" }),
    ).toHaveCount(0);

    const source = page.getByRole("article", { name: "Bàn 01" });
    const target = page.getByRole("article", { name: "Bàn 03" });
    const dialog = page.getByRole("dialog");
    await source.getByRole("button", { name: "Mở phiên" }).click();
    await dialog.getByRole("button", { name: "Mở phiên" }).click();
    await expect(source.getByText("Đang dùng", { exact: true })).toBeVisible();

    await page.reload();
    await expect(source.getByText("Đang dùng", { exact: true })).toBeVisible();
    await page.getByLabel("Quyền xem thử").selectOption("staff");
    await page.getByLabel("Trạng thái").selectOption("occupied");
    await expect(target).toHaveCount(0);
    await source.getByRole("button", { name: "Chuyển bàn" }).click();
    await dialog.getByLabel("Bàn đích").selectOption("table-03");
    await dialog.getByLabel("Lý do").fill("Chuyển ra ngoài trời");
    await dialog.getByRole("button", { name: "Chuyển bàn" }).click();
    await expect(source).toHaveCount(0);
    await expect(target.getByText("Đang dùng", { exact: true })).toBeVisible();

    await page.reload();
    await expect(source.getByText("Trống", { exact: true })).toBeVisible();
    await expect(target.getByText("Đang dùng", { exact: true })).toBeVisible();
    await target.getByRole("button", { name: "Đóng phiên" }).click();
    await dialog.getByRole("button", { name: "Đóng phiên" }).click();
    await expect(target.getByText("Trống", { exact: true })).toBeVisible();
    await page.reload();
    await expect(target.getByText("Trống", { exact: true })).toBeVisible();
    expect(
        await page.evaluate(() => document.documentElement.scrollWidth),
    ).toBeLessThanOrEqual(390);
});
