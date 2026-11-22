import { useState } from "react";
import { ClipboardList, PencilLine, X } from "lucide-react";
import styles from "./orders.module.css";
import { useMutation, useQuery } from "@tanstack/react-query";
import { authFetch } from "@/lib/auth/auth-client";
import { liveInterval } from "@/lib/realtime/live-query";
import type { components } from "@/lib/api/generated";

type Portion = components["schemas"]["StaffPortion"];
export function LivePortions() {
    const portions = useQuery({
        queryKey: ["sales", "portions"],
        queryFn: async ({ signal }): Promise<Portion[]> => {
            const res = await authFetch("/api/sales/portions", {
                signal,
                cache: "no-store",
            });
            if (!res.ok) throw Error("Không thể tải phần món.");
            return res.json();
        },
        refetchInterval: liveInterval,
    });
    const [editor, edit] = useState<{ row: Portion; cancel: boolean } | null>(
        null,
    );
    const [value, setValue] = useState("");
    const change = useMutation({
        mutationFn: async ({
            row,
            cancel,
            text,
        }: {
            row: Portion;
            cancel: boolean;
            text: string;
        }) => {
            const res = await authFetch(
                `/api/sales/portions/${encodeURIComponent(row.id)}/${cancel ? "cancel" : "note"}`,
                {
                    method: cancel ? "POST" : "PUT",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        version: row.version,
                        reason: cancel ? text : null,
                        note: cancel ? null : text,
                    } satisfies components["schemas"]["PortionChange"]),
                },
            );
            if (!res.ok)
                throw Error(
                    res.status === 403
                        ? "Bạn không có quyền thực hiện thao tác này."
                        : res.status === 409
                          ? "Phần món đã thay đổi. Hãy chọn lại phần món."
                          : "Không thể lưu thay đổi.",
                );
        },
        onSuccess: async () => {
            edit(null);
            await portions.refetch();
        },
    });
    function open(row: Portion, cancel: boolean) {
        edit({ row, cancel });
        setValue(cancel ? "" : (row.note ?? ""));
        change.reset();
    }
    const current = portions.data?.find((p) => p.id === editor?.row.id);
    return (
        <section
            aria-label="Phần món trên đơn đang mở"
            className={styles.panel}
        >
            <h2>
                <ClipboardList />
                Phần món trên đơn đang mở
            </h2>
            {portions.isError && (
                <p role="alert">Không thể tải phần món trên máy chủ.</p>
            )}
            {portions.data?.length === 0 && <p>Chưa có phần món.</p>}
            {portions.data?.map((row) => (
                <div key={row.id} className={styles.portion}>
                    <span className={styles.code}>{row.orderCode}</span>
                    <div className={styles.portionInfo}>
                        <strong>{row.dish}</strong>
                        <small>
                            {row.variant}
                            {row.note && (
                                <>
                                    {" "}
                                    · <span>{row.note}</span>
                                </>
                            )}
                        </small>
                    </div>
                    {row.status === "CHO_XAC_NHAN" ? (
                        <>
                            <button
                                disabled={change.isPending || portions.isError}
                                onClick={() => open(row, false)}
                            >
                                <PencilLine size={14} /> Sửa ghi chú
                            </button>
                            <button
                                disabled={change.isPending || portions.isError}
                                onClick={() => open(row, true)}
                                className={styles.cancel}
                            >
                                <X size={14} /> Hủy phần món
                            </button>
                        </>
                    ) : (
                        <span>
                            Đã xác nhận · cần kiểm tra trạng thái chế biến
                        </span>
                    )}
                </div>
            ))}
            {editor && (
                <form
                    className={styles.editForm}
                    onSubmit={(e) => {
                        e.preventDefault();
                        if (
                            current?.version === editor.row.version &&
                            !change.isPending
                        )
                            change.mutate({ ...editor, text: value.trim() });
                    }}
                >
                    <label>
                        {editor.cancel ? "Lý do hủy" : "Ghi chú chế biến"}{" "}
                        <input
                            value={value}
                            maxLength={editor.cancel ? 512 : 2000}
                            onChange={(e) => setValue(e.target.value)}
                            required={editor.cancel}
                        />
                    </label>
                    <button
                        disabled={
                            portions.isError ||
                            change.isPending ||
                            current?.version !== editor.row.version ||
                            (editor.cancel && !value.trim())
                        }
                    >
                        Lưu thay đổi
                    </button>
                    <button
                        type="button"
                        disabled={change.isPending}
                        onClick={() => edit(null)}
                    >
                        Đóng
                    </button>
                    {current?.version !== editor.row.version && (
                        <span role="alert">
                            Phần món đã thay đổi. Hãy chọn lại.
                        </span>
                    )}
                    {change.isError && (
                        <span role="alert">{change.error.message}</span>
                    )}
                </form>
            )}
        </section>
    );
}
