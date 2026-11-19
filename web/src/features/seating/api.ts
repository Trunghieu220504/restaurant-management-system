import { authFetch } from "@/lib/auth/auth-client";
import type { components } from "@/lib/api/generated";
import {
    SeatingFailure,
    type SeatingBoard,
    type SeatingChange,
    type SeatingGateway,
} from "./seating";

type Schema = components["schemas"];
type Access = { catalog: boolean; sessions: boolean };

const failures = {
    "seating.input.invalid": "invalidInput",
    "seating.area.code_conflict": "duplicateCode",
    "seating.table.code_conflict": "duplicateCode",
    "seating.area.has_active_tables": "areaHasTables",
    "seating.table.occupied": "tableOccupied",
    "seating.table.in_use": "tableOccupied",
    "seating.concurrency.conflict": "stale",
    "seating.session.closed": "stale",
    "seating.area.inactive": "stale",
    "seating.table.inactive": "stale",
    "seating.session.open_orders": "openOrders",
    "salesday.day.not_open": "dayClosed",
    "seating.transfer.same_table": "invalidInput",
    "seating.idempotency.conflict": "stale",
} as const;

function id(value: string | number) {
    if (typeof value === "number" && !Number.isSafeInteger(value))
        throw new SeatingFailure("invalidResponse");
    const text = String(value);
    if (!/^[1-9]\d*$/.test(text)) throw new SeatingFailure("invalidResponse");
    return text;
}

export function createSeatingApi(
    access: Access,
    request = authFetch,
): SeatingGateway {
    async function send(path: string, init: RequestInit) {
        const response = await request(`/api/seating/${path}`, {
            ...init,
            cache: "no-store",
        });
        if (!response.ok) {
            const problem = await response.json().catch(() => ({}));
            const code = problem?.code as keyof typeof failures;
            const reason =
                response.status === 403
                    ? "forbidden"
                    : response.status === 401
                      ? "signedOut"
                      : response.status === 404
                        ? "stale"
                        : (failures[code] ?? "unavailable");
            throw new SeatingFailure(reason);
        }
        return response;
    }
    async function read<T>(path: string, signal: AbortSignal): Promise<T> {
        return (await send(path, { signal })).json();
    }
    async function load(signal: AbortSignal): Promise<SeatingBoard> {
        const [board, areas, tables] = await Promise.all([
            access.sessions
                ? read<Schema["BoardAreaDto"][]>("board", signal)
                : [],
            access.catalog ? read<Schema["AreaDto"][]>("areas", signal) : [],
            access.catalog ? read<Schema["TableDto"][]>("tables", signal) : [],
        ]);
        const sessions = new Map(
            board.flatMap((area) =>
                area.tables.map(
                    (table) => [id(table.id), table.session] as const,
                ),
            ),
        );
        const visibleAreas = access.catalog
            ? areas.filter((row) => row.active)
            : board;
        const visibleTables = access.catalog
            ? tables.filter(
                  (row) =>
                      row.active &&
                      (!access.sessions || sessions.has(id(row.id))),
              )
            : board.flatMap((area) =>
                  area.tables.map((table) => ({ ...table, areaId: area.id })),
              );
        return {
            areas: visibleAreas.map((row) => ({
                id: id(row.id),
                code: row.code,
                name: row.name,
                version: "version" in row ? String(row.version) : undefined,
            })),
            tables: visibleTables.map((row) => {
                const session = sessions.get(id(row.id));
                return {
                    id: id(row.id),
                    areaId: id(row.areaId),
                    code: row.code,
                    name: row.name,
                    version: "version" in row ? String(row.version) : undefined,
                    session: session
                        ? { id: id(session.id), openedAt: session.openedAt }
                        : null,
                };
            }),
        };
    }
    async function apply(change: SeatingChange, key: string) {
        const write = (
            path: string,
            body?: object,
            method = "POST",
            session = false,
        ) =>
            send(path, {
                method,
                headers: {
                    ...(body ? { "Content-Type": "application/json" } : {}),
                    ...(session ? { "Idempotency-Key": key } : {}),
                },
                body: body ? JSON.stringify(body) : undefined,
            });
        const version = (ref: { version?: string }) => {
            if (!ref.version) throw new SeatingFailure("stale");
            return ref.version;
        };
        switch (change.kind) {
            case "saveArea":
                if (change.existing)
                    await write(
                        `areas/${id(change.existing.id)}`,
                        {
                            name: change.input.name,
                            version: version(change.existing),
                        } satisfies Schema["UpdateAreaRequest"],
                        "PUT",
                    );
                else
                    await write(
                        "areas",
                        change.input satisfies Schema["CreateAreaRequest"],
                    );
                break;
            case "saveTable": {
                const { name, code, areaId } = change.input;
                if (change.existing)
                    await write(
                        `tables/${id(change.existing.id)}`,
                        {
                            areaId: id(areaId),
                            name,
                            version: version(change.existing),
                        } satisfies Schema["UpdateTableRequest"],
                        "PUT",
                    );
                else
                    await write("tables", {
                        areaId: id(areaId),
                        name,
                        code,
                    } satisfies Schema["CreateTableRequest"]);
                break;
            }
            case "archiveArea":
            case "archiveTable":
                await write(
                    `${change.kind === "archiveArea" ? "areas" : "tables"}/${id(change.existing.id)}/archive`,
                    {
                        version: version(change.existing),
                    } satisfies Schema["ArchiveRequest"],
                );
                break;
            case "openSession":
                await write(
                    `tables/${id(change.tableId)}/sessions`,
                    undefined,
                    "POST",
                    true,
                );
                break;
            case "transferSession":
                await write(
                    `sessions/${id(change.session.id)}/transfer`,
                    {
                        toTableId: id(change.targetTableId),
                        reason: change.reason,
                    } satisfies Schema["TransferRequest"],
                    "POST",
                    true,
                );
                break;
            case "closeSession":
                await write(
                    `sessions/${id(change.session.id)}/close`,
                    undefined,
                    "POST",
                    true,
                );
                break;
        }
    }
    return { load, apply };
}
