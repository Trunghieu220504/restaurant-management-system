"use client";
import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { catalogApi, type CatalogMenu } from "./api";
import { isSoldOut, useAvailability, type TargetKind } from "./availability";

export function useAvailabilityEditor() {
    const stock = useAvailability();
    const write = useMutation({
        mutationFn: (body: {
            businessDayId: string;
            kind: TargetKind;
            targetId: string;
            soldOut: boolean;
        }) => catalogApi.apply({ method: "PUT", path: "availability", body }),
        onSettled: () => stock.refetch(),
    });
    return { stock, write };
}

export function AvailabilityEditor({
    menu,
    stock,
    write,
}: { menu: CatalogMenu } & ReturnType<typeof useAvailabilityEditor>) {
    const [kind, setKind] = useState<TargetKind>("VARIANT");
    const [id, select] = useState("");
    const targets =
        kind === "VARIANT"
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
        </section>
    );
}
