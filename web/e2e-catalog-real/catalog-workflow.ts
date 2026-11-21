import { expect, type Page } from "@playwright/test";

export interface MenuSnapshot {
    groups: {
        id: string;
        name: string;
        sortOrder: number;
        active: boolean;
        version: string;
    }[];
    items: {
        id: string;
        groupId: string;
        code: string;
        name: string;
        station: string;
        active: boolean;
        version: string;
        variants: {
            id: string;
            itemId: string;
            code: string;
            name: string;
            price: number;
            active: boolean;
            version: string;
        }[];
        optionGroups: {
            optionGroupId: string;
            minSelections: number;
            maxSelections: number;
        }[];
    }[];
    optionGroups: {
        id: string;
        name: string;
        type: string;
        active: boolean;
        version: string;
        options: {
            id: string;
            optionGroupId: string;
            name: string;
            extraPrice: number;
            active: boolean;
            version: string;
        }[];
    }[];
}

export function menuNames(suffix: string) {
    return {
        group: `Nhóm kiểm thử ${suffix}`,
        item: `Trà đào kiểm thử ${suffix}`,
        itemCode: `TEA-${suffix}`,
        variant: `Ly lớn ${suffix}`,
        variantCode: `LARGE-${suffix}`,
        optionGroup: `Topping kiểm thử ${suffix}`,
        option: `Trân châu ${suffix}`,
    };
}

export async function loginAndOpenMenu(
    page: Page,
    userName: string,
    password: string,
) {
    await page.goto("/login");
    await page.getByLabel("Tài khoản", { exact: true }).fill(userName);
    await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
    const loginResponse = page.waitForResponse(
        (response) =>
            response.url().endsWith("/api/identity/login") &&
            response.request().method() === "POST",
    );
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
    const response = await loginResponse;
    expect(response.ok()).toBeTruthy();
    const { accessToken } = (await response.json()) as { accessToken: string };
    await expect(page).toHaveURL(/\/sales-day$/);
    await page.locator('a[href="/menu"]:visible').first().click();
    await expect(page).toHaveURL(/\/menu$/);
    return accessToken;
}

/** The same user workflow is exercised against fixtures and the real API. */
export async function createCompleteMenuItem(
    page: Page,
    names: ReturnType<typeof menuNames>,
) {
    const dialog = page.getByRole("dialog");
    const save = async () => {
        await dialog.getByRole("button", { name: "Lưu", exact: true }).click();
        await expect(dialog).toHaveCount(0);
    };

    await page.getByRole("button", { name: "Nhóm món", exact: true }).click();
    await page
        .getByRole("button", { name: "Thêm nhóm món", exact: true })
        .click();
    await dialog.getByLabel("Tên nhóm món", { exact: true }).fill(names.group);
    await dialog.getByLabel("Thứ tự hiển thị", { exact: true }).fill("3");
    await save();

    await page.getByRole("button", { name: "Món ăn", exact: true }).click();
    await page.getByRole("button", { name: "Thêm món", exact: true }).click();
    await dialog.getByLabel("Tên món ăn", { exact: true }).fill(names.item);
    await dialog.getByLabel("Mã món", { exact: true }).fill(names.itemCode);
    await dialog
        .getByLabel("Nhóm món", { exact: true })
        .selectOption({ label: names.group });
    await dialog
        .getByLabel("Trạm chế biến", { exact: true })
        .selectOption("BAR");
    await dialog.getByLabel("Giá biến thể tiêu chuẩn (đ)").fill("35000");
    await save();

    await page
        .getByRole("button", { name: "Thêm biến thể", exact: true })
        .click();
    await dialog
        .getByLabel("Tên biến thể", { exact: true })
        .fill(names.variant);
    await dialog
        .getByLabel("Mã biến thể", { exact: true })
        .fill(names.variantCode);
    await dialog.getByLabel("Giá bán (đ)", { exact: true }).fill("45000");
    await save();
    await expect(page.getByText(names.variant, { exact: true })).toBeVisible();

    await page
        .getByRole("button", { name: "Tùy chọn & topping", exact: true })
        .click();
    await page
        .getByRole("button", { name: "Thêm nhóm tùy chọn", exact: true })
        .click();
    await dialog
        .getByLabel("Tên nhóm tùy chọn", { exact: true })
        .fill(names.optionGroup);
    await dialog
        .getByLabel("Loại nhóm", { exact: true })
        .selectOption("TOPPING");
    await save();

    await page
        .getByRole("button", { name: "Thêm lựa chọn", exact: true })
        .click();
    await dialog.getByLabel("Tên lựa chọn", { exact: true }).fill(names.option);
    await dialog.getByLabel("Giá cộng thêm (đ)", { exact: true }).fill("5000");
    await save();
    await expect(page.getByText(names.option, { exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Món ăn", exact: true }).click();
    await page
        .getByRole("button", {
            name: `Chi tiết món ${names.item}`,
            exact: true,
        })
        .click();
    await page
        .getByRole("button", { name: "Gắn nhóm tùy chọn", exact: true })
        .click();
    await dialog
        .getByLabel("Nhóm tùy chọn / topping", { exact: true })
        .selectOption({ label: names.optionGroup });
    await dialog.getByLabel("Số lựa chọn tối thiểu", { exact: true }).fill("1");
    await dialog.getByLabel("Số lựa chọn tối đa", { exact: true }).fill("1");
    await save();
    await expect(
        page.getByText(names.optionGroup, { exact: true }),
    ).toBeVisible();
}

export function assertCompleteMenu(
    menu: MenuSnapshot,
    names: ReturnType<typeof menuNames>,
) {
    const group = menu.groups.find((entry) => entry.name === names.group);
    expect(group).toMatchObject({ sortOrder: 3, active: true });
    const item = menu.items.find((entry) => entry.code === names.itemCode);
    expect(item).toMatchObject({
        name: names.item,
        groupId: group?.id,
        station: "BAR",
        active: true,
    });
    expect(item?.variants).toEqual(
        expect.arrayContaining([
            expect.objectContaining({
                code: names.variantCode,
                name: names.variant,
                price: 45000,
                active: true,
            }),
        ]),
    );
    const optionGroup = menu.optionGroups.find(
        (entry) => entry.name === names.optionGroup,
    );
    expect(optionGroup).toMatchObject({ type: "TOPPING", active: true });
    expect(optionGroup?.options).toEqual(
        expect.arrayContaining([
            expect.objectContaining({
                name: names.option,
                extraPrice: 5000,
                active: true,
            }),
        ]),
    );
    expect(item?.optionGroups).toEqual(
        expect.arrayContaining([
            {
                optionGroupId: optionGroup?.id,
                minSelections: 1,
                maxSelections: 1,
            },
        ]),
    );
}
