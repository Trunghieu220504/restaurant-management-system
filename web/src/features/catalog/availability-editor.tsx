"use client";
import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { catalogApi, type CatalogMenu } from "./api";
import { isSoldOut, useAvailability, type TargetKind } from "./availability";

export function AvailabilityEditor({ menu }: { menu: CatalogMenu }) {
    const stock = useAvailability();
    const [kind, setKind] = useState<TargetKind>("ITEM");
    const [id, select] = useState("");
    const write = useMutation({
        mutationFn: (body: {
            businessDayId: string;
            kind: TargetKind;
            targetId: string;
            soldOut: boolean;
        }) => catalogApi.apply({ method: "PUT", path: "availability", body }),
        onSuccess: () => stock.refetch(),
    });
    const targets =
        kind === "ITEM"
            ? menu.items
            : kind === "VARIANT"
              ? menu.items.flatMap((i) =>
                    i.variants.map((v) => ({
                        ...v,
                        name: i.name + " · " + v.name,
                    })),
                )
              : menu.optionGroups
                    .filter(
                        (g) =>
                            g.type ===
                            (kind === "TOPPING" ? "TOPPING" : "TUY_CHON"),
                    )
                    .flatMap((g) =>
                        g.options.map((o) => ({
                            ...o,
                            name: g.name + " · " + o.name,
                        })),
                    );
    const sold = !!stock.data && isSoldOut(stock.data, kind, id);
    return (
        <section
            aria-label="Tình trạng bán"
            className="my-4 space-y-2 rounded-xl border p-4"
        >
            <h2 className="font-semibold">Tình trạng bán trong ngày</h2>
            <div className="flex flex-wrap items-center gap-3">
                <label>
                    Loại mục{" "}
                    <select
                        value={kind}
                        onChange={(e) => {
                            setKind(e.target.value as TargetKind);
                            select("");
                            write.reset();
                        }}
                    >
                        <option value="ITEM">Món</option>
                        <option value="VARIANT">Biến thể</option>
                        <option value="TOPPING">Topping</option>
                        <option value="OPTION">Tùy chọn</option>
                    </select>
                </label>
                <label>
                    Mục cần cập nhật{" "}
                    <select
                        value={id}
                        onChange={(e) => {
                            select(e.target.value);
                            write.reset();
                        }}
                    >
                        <option value="">Chọn mục</option>
                        {targets
                            ?.filter((t) => t.active)
                            .map((t) => (
                                <option key={t.id} value={t.id}>
                                    {t.name}
                                </option>
                            ))}
                    </select>
                </label>
                {id && stock.data?.businessDayId && !stock.isError && (
                    <span>{sold ? "Hết món" : "Còn bán"}</span>
                )}
                <button
                    type="button"
                    className="rounded border px-3 py-2 disabled:opacity-50"
                    disabled={
                        !targets.some((t) => t.id === id && t.active) ||
                        !stock.data?.businessDayId ||
                        stock.isError ||
                        write.isPending
                    }
                    onClick={() => {
                        if (stock.data?.businessDayId)
                            write.mutate({
                                businessDayId: stock.data.businessDayId,
                                kind,
                                targetId: id,
                                soldOut: !sold,
                            });
                    }}
                >
                    {sold ? "Bán lại" : "Báo hết"}
                </button>
            </div>
            {stock.data?.businessDayId === null && <p>Chưa mở ngày bán.</p>}
            {(stock.isError || write.isError) && (
                <p role="alert">
                    Không thể cập nhật tình trạng bán. Kiểm tra kết nối và quyền
                    thao tác.
                </p>
            )}
        </section>
    );
}
