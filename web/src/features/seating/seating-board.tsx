"use client";

import { useRef, useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useSeatingBoard, useSeatingChange } from "./queries";
import { LayoutGrid, Plus, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { SeatingEditor, type SeatingEditorAction } from "./seating-editor";
import {
    describeSeatingFailure,
    type SeatingChange,
    type SeatingGateway,
    type SeatingTable,
} from "./seating";

type TableFilter = "all" | "free" | "occupied";
const vietnamTime = new Intl.DateTimeFormat("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    hour: "2-digit",
    minute: "2-digit",
});

function TableCard({
    table,
    canManageCatalog,
    canManageSessions,
    disabled,
    hasFreeTable,
    onEdit,
}: {
    table: SeatingTable;
    canManageCatalog: boolean;
    canManageSessions: boolean;
    disabled: boolean;
    hasFreeTable: boolean;
    onEdit: (action: SeatingEditorAction) => void;
}) {
    const session = table.session;
    return (
        <article
            aria-label={table.name}
            className={cn(
                "flex flex-col gap-3 rounded-xl border p-4 shadow-sm",
                session ? "border-emerald-400 bg-emerald-50" : "bg-card",
            )}
        >
            <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                    <h3 className="break-words font-semibold">{table.name}</h3>
                    <p className="break-words text-xs text-muted-foreground">
                        {table.code}
                    </p>
                </div>
                <span
                    className={cn(
                        "shrink-0 rounded-full px-2.5 py-1 text-xs font-medium",
                        session
                            ? "bg-emerald-100 text-emerald-900"
                            : "text-muted-foreground",
                    )}
                >
                    {canManageSessions
                        ? session
                            ? "Đang dùng"
                            : "Trống"
                        : "Chưa có thông tin phiên"}
                </span>
            </div>
            {session && (
                <p className="text-xs text-muted-foreground">
                    Mở lúc{" "}
                    <time dateTime={session.openedAt}>
                        {vietnamTime.format(new Date(session.openedAt))}
                    </time>
                </p>
            )}
            {canManageSessions && (
                <div className="flex flex-wrap gap-2">
                    {session ? (
                        <>
                            <Button
                                size="sm"
                                variant="outline"
                                disabled={disabled || !hasFreeTable}
                                onClick={() =>
                                    onEdit({
                                        kind: "transfer",
                                        from: table,
                                        session,
                                    })
                                }
                            >
                                Chuyển bàn
                            </Button>
                            <Button
                                size="sm"
                                variant="outline"
                                disabled={disabled}
                                onClick={() =>
                                    onEdit({
                                        kind: "confirm",
                                        title: `Đóng phiên ${table.name}`,
                                        label: "Đóng phiên",
                                        description:
                                            "Sau khi đóng phiên, bàn sẽ trở về trạng thái Trống.",
                                        change: {
                                            kind: "closeSession",
                                            session,
                                        },
                                    })
                                }
                            >
                                Đóng phiên
                            </Button>
                        </>
                    ) : (
                        <Button
                            size="sm"
                            disabled={disabled}
                            onClick={() =>
                                onEdit({
                                    kind: "confirm",
                                    title: `Mở phiên ${table.name}`,
                                    label: "Mở phiên",
                                    description:
                                        "Bàn sẽ chuyển sang trạng thái Đang dùng.",
                                    change: {
                                        kind: "openSession",
                                        tableId: table.id,
                                    },
                                })
                            }
                        >
                            Mở phiên
                        </Button>
                    )}
                </div>
            )}
            {canManageCatalog && (
                <div className="mt-auto flex flex-wrap gap-2 border-t pt-2">
                    <Button
                        size="sm"
                        variant="ghost"
                        disabled={disabled}
                        onClick={() =>
                            onEdit({ kind: "table", existing: table })
                        }
                    >
                        Sửa bàn
                    </Button>
                    <Button
                        size="sm"
                        variant="ghost"
                        disabled={disabled}
                        onClick={() =>
                            onEdit({
                                kind: "confirm",
                                title: `Lưu trữ ${table.name}`,
                                label: "Lưu trữ bàn",
                                description:
                                    "Bàn đã lưu trữ sẽ không còn xuất hiện trên sơ đồ bàn đang hoạt động.",
                                destructive: true,
                                change: {
                                    kind: "archiveTable",
                                    existing: table,
                                },
                            })
                        }
                    >
                        Lưu trữ bàn
                    </Button>
                </div>
            )}
        </article>
    );
}

type BoardProps = {
    gateway: SeatingGateway;
    canManageCatalog: boolean;
    canManageSessions: boolean;
};
// A new cache is mounted for each signed-in account and permission scope.
export function SeatingBoardView(props: BoardProps) {
    const [client] = useState(() => new QueryClient());
    return (
        <QueryClientProvider client={client}>
            <BoardContent {...props} />
        </QueryClientProvider>
    );
}
function BoardContent({
    gateway,
    canManageCatalog,
    canManageSessions,
}: BoardProps) {
    const query = useSeatingBoard(gateway);
    const mutation = useSeatingChange(gateway);
    const board = query.data,
        loading = query.isFetching,
        saving = mutation.isPending;
    const loadError = query.error ? describeSeatingFailure(query.error) : null;
    const reload = () => void query.refetch();
    const [editorError, setEditorError] = useState<string | null>(null);
    const [areaFilter, setSelectedArea] = useState("");
    const selectedArea = board?.areas.some((area) => area.id === areaFilter)
        ? areaFilter
        : "";
    const [statusFilter, setStatusFilter] = useState<TableFilter>("all");
    const [editor, setEditor] = useState<SeatingEditorAction | null>(null);
    const mutationPending = useRef(false);
    const intent = useRef<{ payload: string; key: string } | null>(null);

    function edit(action: SeatingEditorAction) {
        setEditorError(null);
        intent.current = null;
        setEditor(action);
    }

    async function apply(change: SeatingChange) {
        if (mutationPending.current) return;
        mutationPending.current = true;

        setEditorError(null);
        try {
            const payload = JSON.stringify(change);
            if (intent.current?.payload !== payload)
                intent.current = { payload, key: crypto.randomUUID() };
            await mutation.mutateAsync({ change, key: intent.current.key });
            setEditor(null);
        } catch (error) {
            setEditorError(describeSeatingFailure(error));
        } finally {
            mutationPending.current = false;
        }
    }

    const visibleAreas =
        board?.areas.filter(
            (area) => !selectedArea || area.id === selectedArea,
        ) ?? [];
    const scopedTables =
        board?.tables.filter(
            (table) => !selectedArea || table.areaId === selectedArea,
        ) ?? [];
    const freeCount = scopedTables.filter((table) => !table.session).length;
    const occupiedCount = scopedTables.length - freeCount;
    const hasFreeTable = board?.tables.some((table) => !table.session) ?? false;
    const disabled = !board || loading || saving || Boolean(loadError);

    return (
        <div className="space-y-5">
            <header className="flex flex-wrap items-center justify-between gap-3">
                <h1 className="flex items-center gap-2 text-xl font-semibold">
                    <LayoutGrid
                        aria-hidden="true"
                        className="size-5 text-red-600"
                    />
                    Sơ đồ bàn và khu vực
                </h1>
                <div className="flex flex-wrap gap-2">
                    <Button
                        variant="outline"
                        disabled={loading || saving}
                        onClick={reload}
                    >
                        <RefreshCw aria-hidden="true" className="size-4" /> Tải
                        lại sơ đồ
                    </Button>
                    {canManageCatalog && (
                        <Button
                            disabled={disabled}
                            onClick={() => edit({ kind: "area" })}
                        >
                            <Plus aria-hidden="true" className="size-4" /> Thêm
                            khu vực
                        </Button>
                    )}
                </div>
            </header>
            {loading && (
                <p role="status" className="text-sm text-muted-foreground">
                    Đang tải sơ đồ bàn…
                </p>
            )}
            {loadError && (
                <p
                    role="alert"
                    className="rounded-md bg-red-50 p-3 text-sm text-red-800"
                >
                    {loadError}
                </p>
            )}
            {board && (
                <>
                    <div className="flex flex-wrap items-center gap-3 rounded-xl border bg-card p-3 text-sm">
                        <label className="flex items-center gap-2">
                            Khu vực
                            <select
                                value={selectedArea}
                                className="h-11 w-full min-w-0 max-w-56 rounded-md border bg-background px-2"
                                onChange={(event) =>
                                    setSelectedArea(event.target.value)
                                }
                            >
                                <option value="">Tất cả</option>
                                {board.areas.map((area) => (
                                    <option key={area.id} value={area.id}>
                                        {area.name}
                                    </option>
                                ))}
                            </select>
                        </label>
                        {canManageSessions && (
                            <label className="flex items-center gap-2">
                                Trạng thái
                                <select
                                    value={statusFilter}
                                    className="h-11 rounded-md border bg-background px-2"
                                    onChange={(event) =>
                                        setStatusFilter(
                                            event.target.value as TableFilter,
                                        )
                                    }
                                >
                                    <option value="all">
                                        Tất cả ({scopedTables.length})
                                    </option>
                                    <option value="free">
                                        Trống ({freeCount})
                                    </option>
                                    <option value="occupied">
                                        Đang dùng ({occupiedCount})
                                    </option>
                                </select>
                            </label>
                        )}
                    </div>
                    {visibleAreas.length === 0 && (
                        <p className="text-sm text-muted-foreground">
                            Chưa có khu vực đang hoạt động.
                        </p>
                    )}
                    {visibleAreas.map((area) => {
                        const tables = scopedTables.filter(
                            (table) =>
                                table.areaId === area.id &&
                                (statusFilter === "all" ||
                                    Boolean(table.session) ===
                                        (statusFilter === "occupied")),
                        );
                        return (
                            <section
                                key={area.id}
                                aria-label={area.name}
                                className="space-y-3 rounded-xl border bg-muted/30 p-4"
                            >
                                <div className="flex flex-wrap items-center justify-between gap-2">
                                    <div className="min-w-0">
                                        <h2 className="break-words font-semibold">
                                            {area.name}
                                        </h2>
                                        <p className="break-words text-xs text-muted-foreground">
                                            {area.code}
                                        </p>
                                    </div>
                                    {canManageCatalog && (
                                        <div className="flex flex-wrap gap-2">
                                            <Button
                                                size="sm"
                                                variant="outline"
                                                disabled={disabled}
                                                onClick={() =>
                                                    edit({
                                                        kind: "table",
                                                        areaId: area.id,
                                                    })
                                                }
                                            >
                                                Thêm bàn
                                            </Button>
                                            <Button
                                                size="sm"
                                                variant="ghost"
                                                disabled={disabled}
                                                onClick={() =>
                                                    edit({
                                                        kind: "area",
                                                        existing: area,
                                                    })
                                                }
                                            >
                                                Sửa khu vực
                                            </Button>
                                            <Button
                                                size="sm"
                                                variant="ghost"
                                                disabled={disabled}
                                                onClick={() =>
                                                    edit({
                                                        kind: "confirm",
                                                        title: `Lưu trữ ${area.name}`,
                                                        label: "Lưu trữ khu vực",
                                                        description:
                                                            "Khu vực đã lưu trữ sẽ không còn xuất hiện trên sơ đồ bàn đang hoạt động.",
                                                        destructive: true,
                                                        change: {
                                                            kind: "archiveArea",
                                                            existing: area,
                                                        },
                                                    })
                                                }
                                            >
                                                Lưu trữ khu vực
                                            </Button>
                                        </div>
                                    )}
                                </div>
                                {tables.length === 0 && (
                                    <p className="text-sm text-muted-foreground">
                                        Không có bàn phù hợp.
                                    </p>
                                )}
                                <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                                    {tables.map((table) => (
                                        <TableCard
                                            key={table.id}
                                            table={table}
                                            canManageCatalog={canManageCatalog}
                                            canManageSessions={
                                                canManageSessions
                                            }
                                            disabled={disabled}
                                            hasFreeTable={hasFreeTable}
                                            onEdit={edit}
                                        />
                                    ))}
                                </div>
                            </section>
                        );
                    })}
                    {editor && (
                        <SeatingEditor
                            action={editor}
                            board={board}
                            pending={saving}
                            error={editorError}
                            onClose={() => setEditor(null)}
                            onSubmit={(change) => void apply(change)}
                        />
                    )}
                </>
            )}
        </div>
    );
}
