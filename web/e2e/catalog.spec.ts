import { expect, test, type Page } from "@playwright/test";
import { mockIdentity } from "../e2e-production/helpers/identity";
import {
    assertCompleteMenu,
    createCompleteMenuItem,
    loginAndOpenMenu,
    menuNames,
    type MenuSnapshot,
} from "../e2e-catalog-real/catalog-workflow";

// These interception tests verify the UI contract and error states only.
// Actual database persistence is covered by e2e-catalog-real/catalog-real.spec.ts.
async function catalogFixture(
    page: Page,
    options: { permissions?: string[] } = {},
) {
    await mockIdentity(page, {
        signedIn: false,
        permissions: options.permissions ?? ["catalog.menu.manage"],
    });
    const state = {
        menu: { groups: [], items: [], optionGroups: [] } as MenuSnapshot,
        reads: 0,
        writes: [] as { method: string; path: string; body: unknown }[],
        readStatus: 200,
        writeStatus: 200,
        waitForRead: null as Promise<void> | null,
    };
    let sequence = 0;
    const entity = () => ({
        id: `00000000-0000-4000-8000-${String(++sequence).padStart(12, "0")}`,
        active: true,
        version: "AAAAAAAAAAE=",
    });
    await page.route("**/api/catalog/**", async (route) => {
        const request = route.request();
        const path = new URL(request.url()).pathname;
        const method = request.method();
        expect(request.headers().authorization).toBe("Bearer ui-test-access");
        if (method === "GET" && path === "/api/catalog/availability") {
            return route.fulfill({
                json: { businessDayId: "day", soldOut: [] },
            });
        }
        if (method === "GET" && path === "/api/catalog/menu") {
            state.reads++;
            if (state.waitForRead) await state.waitForRead;
            return route.fulfill({
                status: state.readStatus,
                json:
                    state.readStatus === 200
                        ? state.menu
                        : { title: "Catalog unavailable" },
            });
        }
        const body = request.postDataJSON();
        state.writes.push({ method, path, body });
        if (state.writeStatus !== 200) {
            return route.fulfill({
                status: state.writeStatus,
                json: { title: "Request denied" },
            });
        }
        if (method === "POST" && path === "/api/catalog/groups") {
            const entry = {
                ...entity(),
                ...body,
            } as MenuSnapshot["groups"][number];
            state.menu.groups.push(entry);
            return route.fulfill({ status: 201, json: entry });
        }
        if (method === "POST" && path === "/api/catalog/items") {
            const item = entity();
            const { initialVariant, ...fields } = body;
            const entry = {
                ...item,
                ...fields,
                variants: [
                    {
                        ...entity(),
                        ...(initialVariant as object),
                        itemId: item.id,
                    },
                ],
                optionGroups: [],
                imageObjectKey: null,
            } as MenuSnapshot["items"][number];
            state.menu.items.push(entry);
            return route.fulfill({ status: 201, json: entry });
        }
        const variantMatch = path.match(
            /^\/api\/catalog\/items\/([^/]+)\/variants$/,
        );
        if (method === "POST" && variantMatch) {
            const item = state.menu.items.find(
                (entry) => entry.id === variantMatch[1],
            );
            expect(item).toBeDefined();
            const entry = {
                ...entity(),
                ...body,
                itemId: item!.id,
            } as MenuSnapshot["items"][number]["variants"][number];
            item!.variants.push(entry);
            return route.fulfill({ status: 201, json: entry });
        }
        if (method === "POST" && path === "/api/catalog/option-groups") {
            const entry = {
                ...entity(),
                ...body,
                options: [],
            } as MenuSnapshot["optionGroups"][number];
            state.menu.optionGroups.push(entry);
            return route.fulfill({ status: 201, json: entry });
        }
        const optionMatch = path.match(
            /^\/api\/catalog\/option-groups\/([^/]+)\/options$/,
        );
        if (method === "POST" && optionMatch) {
            const group = state.menu.optionGroups.find(
                (entry) => entry.id === optionMatch[1],
            );
            expect(group).toBeDefined();
            const entry = {
                ...entity(),
                ...body,
                optionGroupId: group!.id,
            } as MenuSnapshot["optionGroups"][number]["options"][number];
            group!.options.push(entry);
            return route.fulfill({ status: 201, json: entry });
        }
        const assignmentMatch = path.match(
            /^\/api\/catalog\/items\/([^/]+)\/option-groups\/([^/]+)$/,
        );
        if (method === "PUT" && assignmentMatch) {
            const item = state.menu.items.find(
                (entry) => entry.id === assignmentMatch[1],
            );
            expect(item).toBeDefined();
            const entry = {
                ...body,
                optionGroupId: assignmentMatch[2],
            } as MenuSnapshot["items"][number]["optionGroups"][number];
            item!.optionGroups = [
                ...item!.optionGroups.filter(
                    (link) => link.optionGroupId !== entry.optionGroupId,
                ),
                entry,
            ];
            return route.fulfill({ status: 200, json: entry });
        }
        throw new Error(
            `Unexpected Catalog fixture request: ${method} ${path}`,
        );
    });
    return state;
}

test("Catalog fixture: complete dish creation uses the API contract and survives refetch", async ({
    page,
}) => {
    const state = await catalogFixture(page);
    const names = menuNames("UI");
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await loginAndOpenMenu(page, "catalog-test", "test-password");
    await createCompleteMenuItem(page, names);
    assertCompleteMenu(state.menu, names);
    expect(state.writes.map((entry) => entry.method)).toEqual([
        "POST",
        "POST",
        "POST",
        "POST",
        "POST",
        "PUT",
    ]);
    expect(state.writes.at(-1)?.body).toEqual({
        minSelections: 1,
        maxSelections: 1,
    });
    const readsBeforeReload = state.reads;
    await loginAndOpenMenu(page, "catalog-test", "test-password");
    await page
        .getByRole("button", {
            name: `Chi tiết món ${names.item}`,
            exact: true,
        })
        .click();
    await expect(page.getByText(names.variant, { exact: true })).toBeVisible();
    await expect(
        page.getByText(names.optionGroup, { exact: true }),
    ).toBeVisible();
    expect(state.reads).toBeGreaterThan(readsBeforeReload);
    expect(errors).toEqual([]);
});

test("Catalog fixture: min cannot exceed max and an invalid form sends no assignment", async ({
    page,
}) => {
    const state = await catalogFixture(page);
    const names = menuNames("BOUNDS");
    await loginAndOpenMenu(page, "catalog-test", "test-password");
    await createCompleteMenuItem(page, names);
    await page
        .getByRole("button", { name: "Sửa giới hạn", exact: true })
        .click();
    const dialog = page.getByRole("dialog");
    await dialog.getByLabel("Số lựa chọn tối thiểu", { exact: true }).fill("2");
    await dialog.getByLabel("Số lựa chọn tối đa", { exact: true }).fill("1");
    const writesBeforeSave = state.writes.length;
    await dialog.getByRole("button", { name: "Lưu", exact: true }).click();
    await expect(dialog).toBeVisible();
    expect(
        await dialog
            .locator('input[name="minSelections"]')
            .evaluate((input: HTMLInputElement) => input.validity.valid),
    ).toBe(false);
    expect(state.writes).toHaveLength(writesBeforeSave);
});

test("Catalog fixture: an availability-only account can read but cannot edit", async ({
    page,
}) => {
    await catalogFixture(page, {
        permissions: ["catalog.availability.manage"],
    });
    await loginAndOpenMenu(page, "catalog-test", "test-password");
    await expect(
        page.getByRole("button", { name: "Món ăn", exact: true }),
    ).toBeVisible();
    await expect(
        page.getByRole("button", { name: "Thêm món", exact: true }),
    ).toHaveCount(0);
    await page.getByRole("button", { name: "Nhóm món", exact: true }).click();
    await expect(
        page.getByRole("button", { name: "Thêm nhóm món", exact: true }),
    ).toHaveCount(0);
    await page
        .getByRole("button", { name: "Tùy chọn & topping", exact: true })
        .click();
    await expect(
        page.getByRole("button", { name: "Thêm nhóm tùy chọn", exact: true }),
    ).toHaveCount(0);
});

test("Catalog fixture: denied save keeps entered values and does not invent success", async ({
    page,
}) => {
    const state = await catalogFixture(page);
    state.writeStatus = 403;
    await loginAndOpenMenu(page, "catalog-test", "test-password");
    await page.getByRole("button", { name: "Nhóm món", exact: true }).click();
    await page
        .getByRole("button", { name: "Thêm nhóm món", exact: true })
        .click();
    const dialog = page.getByRole("dialog");
    await dialog
        .getByLabel("Tên nhóm món", { exact: true })
        .fill("Chưa được lưu");
    await dialog.getByRole("button", { name: "Lưu", exact: true }).click();
    await expect(dialog.getByRole("alert")).toContainText(/quyền/i);
    await expect(
        dialog.getByLabel("Tên nhóm món", { exact: true }),
    ).toHaveValue("Chưa được lưu");
    expect(state.menu.groups).toHaveLength(0);
    expect(state.writes).toHaveLength(1);
});

test("Catalog fixture: a slow or failed menu request shows loading then a recoverable error", async ({
    page,
}) => {
    const state = await catalogFixture(page);
    let release!: () => void;
    state.waitForRead = new Promise<void>((resolve) => {
        release = resolve;
    });
    state.readStatus = 503;
    await loginAndOpenMenu(page, "catalog-test", "test-password");
    try {
        await expect(page.getByText(/Đang tải thực đơn/i)).toBeVisible();
    } finally {
        release();
    }
    await expect(page.locator("section").getByRole("alert")).toBeVisible();
    state.readStatus = 200;
    state.waitForRead = null;
    await page.getByRole("button", { name: /Thử lại/i }).click();
    await expect(page.locator("section").getByRole("alert")).toHaveCount(0);
    await expect(
        page.getByRole("button", { name: "Thêm món", exact: true }),
    ).toBeVisible();
    expect(state.reads).toBeGreaterThan(1);
});
