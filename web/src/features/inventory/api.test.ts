import { expect, it, vi } from "vitest";
import { createInventoryApi } from "./api";

it("loads the inventory catalog", async () => {
    const request = vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ units: [], ingredients: [] }), {
            status: 200,
        }),
    );
    await expect(createInventoryApi(request).catalog()).resolves.toEqual({
        units: [],
        ingredients: [],
    });
});
