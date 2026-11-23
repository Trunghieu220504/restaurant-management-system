import type { Page } from "@playwright/test";
export function mockPublicMenu(
    page: Page,
    photo: () => string | null = () => null,
) {
    return page.route("**/api/public/menu", (r) =>
        r.fulfill({
            json: {
                businessDayId: "day",
                groups: [
                    {
                        id: "g",
                        name: "Đồ uống",
                        items: ["Trà đào", "Nước cam"].map((name, i) => ({
                            id: "i" + i,
                            name,
                            imageObjectKey: photo(),
                            variants: [
                                { id: "v" + i, name: "Ly", price: 30000 },
                            ],
                            optionGroups: [],
                        })),
                    },
                ],
            },
        }),
    );
}
