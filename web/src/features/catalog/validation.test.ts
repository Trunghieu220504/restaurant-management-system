import { describe, expect, it } from "vitest";
import { integerValue, selectionLimits } from "./validation";
import { CatalogError, createCatalogApi, parseMenu } from "./api";

describe("Catalog contract and validation", () => {
    it("rejects fractional, negative and unsafe VND amounts", () => {
        for (const value of ["", "1.5", "-1", "9007199254740992"])
            expect(() => integerValue(value, "Giá")).toThrow();
        expect(integerValue("45000", "Giá")).toBe(45000);
    });
    it("requires selection limits that the available options can satisfy", () => {
        expect(selectionLimits(0, 2, 2)).toEqual({
            minSelections: 0,
            maxSelections: 2,
        });
        for (const [min, max, count] of [
            [2, 1, 3],
            [0, 0, 1],
            [0, 3, 2],
        ])
            expect(() => selectionLimits(min, max, count)).toThrow();
    });
    it("rejects malformed server data instead of displaying sample data", () => {
        expect(() => parseMenu({ items: [] })).toThrow();
    });
    it("uses the real Catalog route and propagates denied writes", async () => {
        const calls: string[] = [];
        const api = createCatalogApi(async (url) => {
            calls.push(String(url));
            return new Response(
                JSON.stringify({ code: "catalog.item.code_conflict" }),
                { status: 409 },
            );
        });
        await expect(
            api.apply({ method: "POST", path: "items", body: { code: "TEA" } }),
        ).rejects.toEqual(new CatalogError(409, "catalog.item.code_conflict"));
        expect(calls).toEqual(["/api/catalog/items"]);
    });
});
