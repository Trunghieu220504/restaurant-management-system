"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import {
    readOperation,
    useOperationCommand,
} from "@/features/operations/operation-request";
import type { components } from "@/lib/api/generated";

type Schema = components["schemas"];
const field = "min-h-11 rounded-md border bg-background px-3 py-2";
const decimal = /^-?[0-9]{1,20}(?:[.,][0-9]{1,8})?$/;

export function StockAdjustment() {
    const [ingredientId, select] = useState("");
    const [quantity, setQuantity] = useState("");
    const [value, setValue] = useState("");
    const [reason, setReason] = useState("");
    const [notice, setNotice] = useState("");
    const catalog = useQuery({
        queryKey: ["stock-adjustment", "catalog"],
        queryFn: ({ signal }) =>
            readOperation<Schema["StockCatalog"]>(
                "/api/inventory/catalog",
                signal,
            ),
    });
    const day = useQuery({
        queryKey: ["stock-adjustment", "day"],
        queryFn: ({ signal }) =>
            readOperation<Schema["BusinessDayResponse"] | null>(
                "/api/salesday/current",
                signal,
            ),
    });
    const refresh = () => Promise.all([catalog.refetch(), day.refetch()]);
    const command = useOperationCommand(async () => {
        setQuantity("");
        setValue("");
        setReason("");
        setNotice("Đã lưu điều chỉnh tồn kho.");
        await refresh();
    }, refresh);
    const ingredients =
        catalog.data?.ingredients.filter(
            (item) =>
                item.id != null &&
                !item.luuTru &&
                item.cheDo === "THEO_DOI_TON",
        ) ?? [];
    const item = ingredients.find((row) => String(row.id) === ingredientId);
    const unit = catalog.data?.units.find(
        (row) => String(row.id) === String(item?.donViChuanId),
    );
    const active =
        day.data?.status === "DANG_BAN" && !day.isError ? day.data : null;
    const validNumbers =
        decimal.test(quantity) &&
        decimal.test(value) &&
        Number(quantity.replace(",", ".")) !== 0 &&
        (Number(value.replace(",", ".")) === 0 ||
            quantity.startsWith("-") === value.startsWith("-"));
    const error = command.error ?? day.error ?? catalog.error;

    return (
        <section className="grid max-w-2xl gap-4 border-t pt-5">
            <div className="flex flex-wrap items-center justify-between gap-2">
                <h3 className="font-semibold">Điều chỉnh tồn kho</h3>
                <Button
                    variant="outline"
                    disabled={
                        command.isPending ||
                        day.isFetching ||
                        catalog.isFetching
                    }
                    onClick={() => {
                        command.reset();
                        void refresh();
                    }}
                >
                    Tải lại
                </Button>
            </div>
            {(day.isPending || catalog.isPending) && (
                <p role="status">Đang tải nguyên liệu và ngày bán…</p>
            )}
            {error && (
                <p role="alert" className="text-sm text-red-700">
                    {error.message}
                </p>
            )}
            {notice && <p role="status">{notice}</p>}
            {!active && !day.isPending && !day.isError && (
                <p>Chỉ ghi điều chỉnh khi ngày bán đang mở.</p>
            )}
            {catalog.data && !catalog.isError && (
                <form
                    className="grid gap-3"
                    onSubmit={(event) => {
                        event.preventDefault();
                        if (
                            !active ||
                            !item ||
                            !validNumbers ||
                            !reason.trim() ||
                            command.isPending
                        )
                            return;
                        setNotice("");
                        command.mutate({
                            path: "/api/inventory/adjustments",
                            body: {
                                dayId: active.id,
                                ingredientId,
                                quantity: quantity.replace(",", "."),
                                value: value.replace(",", "."),
                                reason: reason.trim(),
                            } satisfies Schema["StockAdjustment"],
                        });
                    }}
                >
                    <fieldset
                        className="grid gap-3"
                        disabled={!active || command.isPending}
                    >
                        <label className="grid gap-1">
                            Nguyên liệu
                            <select
                                className={field}
                                value={ingredientId}
                                onChange={(e) => select(e.target.value)}
                                required
                            >
                                <option value="">
                                    Chọn nguyên liệu theo dõi tồn
                                </option>
                                {ingredients.map((row) => (
                                    <option key={row.id} value={row.id}>
                                        {row.ma} · {row.ten}
                                    </option>
                                ))}
                            </select>
                        </label>
                        <div className="grid gap-3 sm:grid-cols-2">
                            <label className="grid gap-1">
                                Số lượng thay đổi
                                {unit?.ten ? " (" + unit.ten + ")" : ""}
                                <input
                                    className={field}
                                    value={quantity}
                                    inputMode="text"
                                    maxLength={30}
                                    required
                                    onChange={(e) =>
                                        setQuantity(e.target.value)
                                    }
                                />
                            </label>
                            <label className="grid gap-1">
                                Giá trị thay đổi (VND)
                                <input
                                    className={field}
                                    value={value}
                                    inputMode="text"
                                    maxLength={30}
                                    required
                                    onChange={(e) => setValue(e.target.value)}
                                />
                            </label>
                        </div>
                        {quantity && value && !validNumbers && (
                            <p className="text-sm text-red-700">
                                Số lượng phải khác 0. Giá trị phải cùng dấu với
                                số lượng hoặc bằng 0. Tối đa 8 chữ số thập phân.
                            </p>
                        )}
                        <label className="grid gap-1">
                            Lý do
                            <input
                                className={field}
                                value={reason}
                                required
                                maxLength={512}
                                onChange={(e) => setReason(e.target.value)}
                            />
                        </label>
                        <Button
                            className="min-h-11 justify-self-start"
                            disabled={!item || !validNumbers || !reason.trim()}
                        >
                            {command.isPending ? "Đang lưu…" : "Lưu điều chỉnh"}
                        </Button>
                    </fieldset>
                </form>
            )}
        </section>
    );
}
