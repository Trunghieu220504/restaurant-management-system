"use client";

import { useEffect, useId, useRef, type FormEvent } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type {
    SeatingArea,
    SeatingBoard,
    SeatingChange,
    SeatingSession,
    SeatingTable,
} from "./seating";

export type SeatingEditorAction =
    | { kind: "area"; existing?: SeatingArea }
    | { kind: "table"; existing?: SeatingTable; areaId?: string }
    | { kind: "transfer"; session: SeatingSession; from: SeatingTable }
    | {
          kind: "confirm";
          title: string;
          description: string;
          label: string;
          change: SeatingChange;
          destructive?: boolean;
      };

const selectClass = "h-10 w-full rounded-md border bg-background px-3 text-sm";

function editorTitle(action: SeatingEditorAction) {
    switch (action.kind) {
        case "area":
            return action.existing ? "Sửa khu vực" : "Thêm khu vực";
        case "table":
            return action.existing ? "Sửa bàn" : "Thêm bàn";
        case "transfer":
            return `Chuyển bàn từ ${action.from.name}`;
        case "confirm":
            return action.title;
    }
}

export function SeatingEditor({
    action,
    board,
    pending,
    error,
    onClose,
    onSubmit,
}: {
    action: SeatingEditorAction;
    board: SeatingBoard;
    pending: boolean;
    error: string | null;
    onClose: () => void;
    onSubmit: (change: SeatingChange) => void;
}) {
    const titleId = useId();
    const dialogRef = useRef<HTMLDialogElement>(null);
    useEffect(() => {
        const dialog = dialogRef.current;
        dialog?.showModal();
        return () => dialog?.close();
    }, []);

    function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const fields = new FormData(event.currentTarget);
        const value = (name: string) => String(fields.get(name) ?? "").trim();
        switch (action.kind) {
            case "area":
                onSubmit({
                    kind: "saveArea",
                    existing: action.existing,
                    input: { code: value("code"), name: value("name") },
                });
                return;
            case "table":
                onSubmit({
                    kind: "saveTable",
                    existing: action.existing,
                    input: {
                        code: value("code"),
                        name: value("name"),
                        areaId: value("areaId"),
                    },
                });
                return;
            case "transfer":
                onSubmit({
                    kind: "transferSession",
                    session: action.session,
                    targetTableId: value("targetTableId"),
                    reason: value("reason"),
                });
                return;
            case "confirm":
                onSubmit(action.change);
        }
    }

    const isCatalog = action.kind === "area" || action.kind === "table";
    const freeTables = board.tables.filter((table) => !table.session);
    const areaName = new Map(board.areas.map((area) => [area.id, area.name]));
    const submitLabel =
        action.kind === "confirm"
            ? action.label
            : action.kind === "transfer"
              ? "Chuyển bàn"
              : "Lưu";

    return (
        <dialog
            ref={dialogRef}
            aria-labelledby={titleId}
            className="m-auto max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] max-w-md overflow-y-auto rounded-xl border bg-card p-5 text-card-foreground shadow-xl backdrop:bg-black/50"
            onCancel={(event) => {
                event.preventDefault();
                if (!pending) onClose();
            }}
        >
            <h2 id={titleId} className="text-lg font-semibold">
                {editorTitle(action)}
            </h2>
            <form className="mt-4 space-y-4" onSubmit={submit}>
                <fieldset disabled={pending} className="space-y-4">
                    {isCatalog && (
                        <>
                            <label className="block space-y-1 text-sm">
                                <span>Mã</span>
                                <Input
                                    name="code"
                                    readOnly={!!action.existing}
                                    defaultValue={action.existing?.code}
                                    maxLength={32}
                                    pattern=".*\S.*"
                                    required
                                />
                            </label>
                            <label className="block space-y-1 text-sm">
                                <span>Tên</span>
                                <Input
                                    name="name"
                                    defaultValue={action.existing?.name}
                                    maxLength={150}
                                    pattern=".*\S.*"
                                    required
                                />
                            </label>
                        </>
                    )}
                    {action.kind === "table" && (
                        <label className="block space-y-1 text-sm">
                            <span>Khu vực</span>
                            <select
                                className={selectClass}
                                name="areaId"
                                defaultValue={
                                    action.existing?.areaId ??
                                    action.areaId ??
                                    ""
                                }
                                required
                            >
                                <option value="">Chọn khu vực</option>
                                {board.areas.map((area) => (
                                    <option key={area.id} value={area.id}>
                                        {area.name}
                                    </option>
                                ))}
                            </select>
                        </label>
                    )}
                    {action.kind === "transfer" && (
                        <>
                            <label className="block space-y-1 text-sm">
                                <span>Bàn đích</span>
                                <select
                                    className={selectClass}
                                    name="targetTableId"
                                    defaultValue=""
                                    required
                                >
                                    <option value="">Chọn bàn trống</option>
                                    {freeTables.map((table) => (
                                        <option key={table.id} value={table.id}>
                                            {table.name} ({table.code}) ·{" "}
                                            {areaName.get(table.areaId)}
                                        </option>
                                    ))}
                                </select>
                            </label>
                            <label className="block space-y-1 text-sm">
                                <span>Lý do</span>
                                <Input
                                    name="reason"
                                    maxLength={1000}
                                    pattern=".*\S.*"
                                    required
                                />
                            </label>
                        </>
                    )}
                    {action.kind === "confirm" && (
                        <p className="text-sm text-muted-foreground">
                            {action.description}
                        </p>
                    )}
                </fieldset>
                {error && (
                    <p
                        role="alert"
                        className="rounded-md bg-red-50 p-3 text-sm text-red-800"
                    >
                        {error}
                    </p>
                )}
                <div className="flex flex-wrap justify-end gap-2">
                    <Button
                        type="button"
                        variant="outline"
                        onClick={onClose}
                        disabled={pending}
                    >
                        Hủy
                    </Button>
                    <Button
                        type="submit"
                        variant={
                            action.kind === "confirm" && action.destructive
                                ? "destructive"
                                : "default"
                        }
                        disabled={pending}
                    >
                        {pending ? "Đang lưu…" : submitLabel}
                    </Button>
                </div>
            </form>
        </dialog>
    );
}
