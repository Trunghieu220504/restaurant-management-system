import { describe, it, expect, vi } from "vitest";
import { createSeatingApi } from "./api";
import type { SeatingChange } from "./seating";

const area = {
    id: 1,
    name: "Main",
    code: "A",
    active: true,
    version: "AAAAAQ==",
};
const table = {
    id: "9007199254740993",
    name: "Table One",
    code: "T1",
    areaId: 1,
    active: true,
    version: "AAAAAg==",
};
const session = { id: 3, openedAt: "2026-11-19T04:00:00Z" };
const signal = new AbortController().signal;
describe("Seating API", () => {
    it.each([
        { catalog: true, sessions: false },
        { catalog: false, sessions: true },
        { catalog: true, sessions: true },
    ])("loads endpoints granted by %o", async (access) => {
        const data: Record<string, unknown> = {
            board: [
                {
                    id: area.id,
                    code: area.code,
                    name: area.name,
                    tables: [
                        {
                            id: table.id,
                            code: table.code,
                            name: table.name,
                            status: "DANG_DUNG",
                            session,
                        },
                    ],
                },
            ],
            areas: [area, { ...area, id: 2, active: false }],
            tables: [table, { ...table, id: 4, active: false }],
        };
        const request = vi.fn(
            async (path: RequestInfo | URL, init?: RequestInit) => {
                expect(init?.signal).toBe(signal);
                return Response.json(data[String(path).split("/").at(-1)!]);
            },
        );
        const board = await createSeatingApi(access, request).load(signal);
        const expectedPaths = [
            ...(access.sessions ? ["board"] : []),
            ...(access.catalog ? ["areas", "tables"] : []),
        ];
        expect(request.mock.calls.map(([path]) => path)).toEqual(
            expectedPaths.map((path) => "/api/seating/" + path),
        );
        expect(board.areas).toHaveLength(1);
        expect(board.tables).toEqual([
            {
                id: table.id,
                areaId: "1",
                code: "T1",
                name: table.name,
                version: access.catalog ? table.version : undefined,
                session: access.sessions ? { ...session, id: "3" } : null,
            },
        ]);
    });
    const existing = { id: table.id, version: table.version };
    const cases: [SeatingChange, string, string, unknown, boolean][] = [
        [
            { kind: "saveArea", input: { name: "Main", code: "A" } },
            "POST",
            "areas",
            { name: "Main", code: "A" },
            false,
        ],
        [
            { kind: "saveArea", input: { name: "New", code: "A" }, existing },
            "PUT",
            "areas/" + table.id,
            { name: "New", version: table.version },
            false,
        ],
        [
            { kind: "saveTable", input: { areaId: "1", name: "N", code: "C" } },
            "POST",
            "tables",
            { areaId: "1", name: "N", code: "C" },
            false,
        ],
        [
            {
                kind: "saveTable",
                existing,
                input: { areaId: "2", name: "N", code: "C" },
            },
            "PUT",
            "tables/" + table.id,
            { areaId: "2", name: "N", version: table.version },
            false,
        ],
        [
            { kind: "archiveArea", existing },
            "POST",
            "areas/" + table.id + "/archive",
            { version: table.version },
            false,
        ],
        [
            { kind: "archiveTable", existing },
            "POST",
            "tables/" + table.id + "/archive",
            { version: table.version },
            false,
        ],
        [
            { kind: "openSession", tableId: table.id },
            "POST",
            "tables/" + table.id + "/sessions",
            null,
            true,
        ],
        [
            {
                kind: "transferSession",
                session: { id: "3" },
                targetTableId: table.id,
                reason: "Move",
            },
            "POST",
            "sessions/3/transfer",
            { toTableId: table.id, reason: "Move" },
            true,
        ],
        [
            { kind: "closeSession", session: { id: "3" } },
            "POST",
            "sessions/3/close",
            null,
            true,
        ],
    ];
    it.each(cases)(
        "sends %o using the contract",
        async (change, method, path, body, keyed) => {
            const request = vi
                .fn<typeof fetch>()
                .mockResolvedValue(new Response(null, { status: 204 }));
            await createSeatingApi(
                { catalog: true, sessions: true },
                request,
            ).apply(change, "same-key");
            const [url, init] = request.mock.calls[0];
            expect(url).toBe("/api/seating/" + path);
            expect(init?.method).toBe(method);
            expect(init?.body ? JSON.parse(String(init.body)) : null).toEqual(
                body,
            );
            expect(new Headers(init?.headers).get("Idempotency-Key")).toBe(
                keyed ? "same-key" : null,
            );
        },
    );
    it("surfaces a version conflict returned by the backend", async () => {
        const api = createSeatingApi(
            { catalog: true, sessions: false },
            async () =>
                Response.json(
                    { code: "seating.concurrency.conflict" },
                    { status: 409 },
                ),
        );
        await expect(
            api.apply({ kind: "archiveTable", existing }, "k"),
        ).rejects.toMatchObject({ reason: "stale" });
    });
});
