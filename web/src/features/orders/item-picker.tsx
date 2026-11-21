import { useEffect, useRef, useState } from "react";
import {
    formatPrice,
    type CatalogMenu,
    type MenuItem,
} from "@/features/catalog/api";
import { quote, type Line } from "./batches";
export function ItemPicker({
    menu,
    item,
    add,
    close,
}: {
    menu: CatalogMenu;
    item: MenuItem;
    add: (line: Line) => void;
    close: () => void;
}) {
    const dialog = useRef<HTMLDialogElement>(null);
    useEffect(() => {
        dialog.current?.showModal();
    }, []);
    const variants = item.variants.filter((v) => v.active);
    const [variantId, variant] = useState(variants[0]?.id ?? "");
    const [options, select] = useState<string[]>([]);
    const [error, fail] = useState("");
    return (
        <dialog
            ref={dialog}
            aria-label={item.name}
            onCancel={close}
            className="m-auto w-96 max-w-[94vw] rounded-xl p-5 shadow-xl backdrop:bg-black/40"
        >
            <form
                className="space-y-4"
                onSubmit={(e) => {
                    e.preventDefault();
                    try {
                        const choice = { itemId: item.id, variantId, options };
                        add({
                            ...choice,
                            ...quote(menu, choice),
                            id: crypto.randomUUID(),
                            quantity: 1,
                        });
                        close();
                    } catch (err) {
                        fail((err as Error).message);
                    }
                }}
            >
                <h2>{item.name}</h2>
                <label>
                    Biến thể
                    <select
                        value={variantId}
                        onChange={(e) => variant(e.target.value)}
                        required
                    >
                        {variants.map((v) => (
                            <option key={v.id} value={v.id}>
                                {v.name} · {formatPrice(v.price)}
                            </option>
                        ))}
                    </select>
                </label>
                {item.optionGroups.map((a) => {
                    const g = menu.optionGroups.find(
                        (g) => g.id === a.optionGroupId && g.active,
                    );
                    return (
                        g && (
                            <fieldset key={g.id}>
                                <legend>
                                    {g.name} (chọn {a.minSelections}–
                                    {a.maxSelections})
                                </legend>
                                {g.options
                                    .filter((o) => o.active)
                                    .map((o) => (
                                        <label key={o.id} className="block">
                                            <input
                                                type="checkbox"
                                                checked={options.includes(o.id)}
                                                onChange={(e) =>
                                                    select(
                                                        e.target.checked
                                                            ? [...options, o.id]
                                                            : options.filter(
                                                                  (id) =>
                                                                      id !==
                                                                      o.id,
                                                              ),
                                                    )
                                                }
                                            />{" "}
                                            {o.name} ·{" "}
                                            {formatPrice(o.extraPrice)}
                                        </label>
                                    ))}
                            </fieldset>
                        )
                    );
                })}
                {error && <p role="alert">{error}</p>}
                <button type="button" onClick={close}>
                    Đóng
                </button>{" "}
                <button type="submit">Thêm vào giỏ</button>
            </form>
        </dialog>
    );
}
