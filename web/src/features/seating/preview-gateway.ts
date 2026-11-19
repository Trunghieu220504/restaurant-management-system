import {
    SeatingFailure,
    type SeatingArea,
    type SeatingChange,
    type SeatingGateway,
    type SeatingTable,
} from "./seating";

type PreviewArea = SeatingArea & { archived?: boolean };
type PreviewTable = SeatingTable & { archived?: boolean };
type PreviewData = { areas: PreviewArea[]; tables: PreviewTable[] };
const storageKey = "restaurant:seating-preview:1";

function readPreview(): PreviewData {
    const saved = sessionStorage.getItem(storageKey);
    if (saved) return JSON.parse(saved) as PreviewData;
    return {
        areas: [
            { id: "hall", code: "SANH", name: "Sảnh chính", version: "1" },
            { id: "patio", code: "SAN", name: "Ngoài trời", version: "1" },
        ],
        tables: [
            {
                id: "table-01",
                areaId: "hall",
                code: "B01",
                name: "Bàn 01",
                version: "1",
                session: null,
            },
            {
                id: "table-02",
                areaId: "hall",
                code: "B02",
                name: "Bàn 02",
                version: "1",
                session: null,
            },
            {
                id: "table-03",
                areaId: "patio",
                code: "B03",
                name: "Bàn 03",
                version: "1",
                session: null,
            },
        ],
    };
}

function findActive<T extends PreviewArea>(
    records: T[],
    id: string,
    version?: string,
): T {
    const item = records.find((record) => record.id === id && !record.archived);
    if (!item || (version !== undefined && item.version !== version)) {
        throw new SeatingFailure("stale");
    }
    return item;
}

function save<T extends PreviewArea>(
    records: T[],
    input: Pick<T, "code" | "name"> & Partial<T>,
    create: (id: string, version: string) => T,
    existing?: { id: string; version?: string },
) {
    const code = input.code.trim();
    const name = input.name.trim();
    if (!code || code.length > 32 || !name || name.length > 150) {
        throw new SeatingFailure("invalidInput");
    }
    if (
        records.some(
            (entry) =>
                entry.id !== existing?.id &&
                entry.code.toUpperCase() === code.toUpperCase(),
        )
    ) {
        throw new SeatingFailure("duplicateCode");
    }
    const version = crypto.randomUUID();
    if (existing) {
        Object.assign(
            findActive(records, existing.id, existing.version),
            input,
            { code, name, version },
        );
    } else {
        records.push({ ...create(crypto.randomUUID(), version), code, name });
    }
}

function applyPreview(data: PreviewData, change: SeatingChange) {
    switch (change.kind) {
        case "saveArea": {
            const { input } = change;
            save(
                data.areas,
                input,
                (id, version) => ({ ...input, id, version }),
                change.existing,
            );
            return;
        }
        case "saveTable": {
            const { input } = change;
            findActive(data.areas, input.areaId);
            save(
                data.tables,
                input,
                (id, version) => ({ ...input, id, version, session: null }),
                change.existing,
            );
            return;
        }
        case "archiveArea": {
            const area = findActive(
                data.areas,
                change.existing.id,
                change.existing.version,
            );
            if (
                data.tables.some(
                    (table) => table.areaId === area.id && !table.archived,
                )
            ) {
                throw new SeatingFailure("areaHasTables");
            }
            area.archived = true;
            return;
        }
        case "archiveTable": {
            const table = findActive(
                data.tables,
                change.existing.id,
                change.existing.version,
            );
            if (table.session) throw new SeatingFailure("tableOccupied");
            table.archived = true;
            return;
        }
        case "openSession": {
            const table = findActive(data.tables, change.tableId);
            if (table.session) throw new SeatingFailure("tableOccupied");
            table.session = {
                id: crypto.randomUUID(),
                openedAt: new Date().toISOString(),
                version: crypto.randomUUID(),
            };
            return;
        }
        case "closeSession":
        case "transferSession": {
            const source = data.tables.find(
                (table) =>
                    !table.archived && table.session?.id === change.session.id,
            );
            if (
                !source?.session ||
                source.session.version !== change.session.version
            ) {
                throw new SeatingFailure("stale");
            }
            if (change.kind === "transferSession") {
                if (!change.reason.trim() || change.reason.length > 1000) {
                    throw new SeatingFailure("invalidInput");
                }
                const target = findActive(data.tables, change.targetTableId);
                if (target.session) throw new SeatingFailure("tableOccupied");
                target.session = {
                    ...source.session,
                    version: crypto.randomUUID(),
                };
            }
            source.session = null;
        }
    }
}

export const previewGateway: SeatingGateway = {
    async load(signal) {
        signal.throwIfAborted();
        const data = readPreview();
        return {
            areas: data.areas.filter((area) => !area.archived),
            tables: data.tables.filter((table) => !table.archived),
        };
    },
    async apply(change) {
        const data = readPreview();
        applyPreview(data, change);
        sessionStorage.setItem(storageKey, JSON.stringify(data));
    },
};
