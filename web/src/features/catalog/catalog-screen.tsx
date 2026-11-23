"use client";
import { useEffect, useState } from "react";
import { usePermissions } from "@/components/permission-provider";
import {
    catalogApi,
    describeCatalogError,
    formatPrice,
    type CatalogMenu,
    type CatalogChange,
} from "./api";
import { CatalogEditor, type Editor } from "./catalog-editor";
import { AvailabilityEditor } from "./availability-editor";
import { MenuPhoto, PhotoUpload } from "./menu-photo";
import styles from "./catalog.module.css";

export function CatalogScreen() {
    const { can } = usePermissions();
    const manage = can("catalog.menu.manage");
    const [menu, setMenu] = useState<CatalogMenu | null>(null),
        [error, setError] = useState(""),
        [tab, setTab] = useState("Món ăn"),
        [selected, setSelected] = useState(""),
        [optionSelected, setOptionSelected] = useState(""),
        [search, setSearch] = useState(""),
        [editor, setEditor] = useState<Editor | null>(null),
        [notice, setNotice] = useState("");
    async function load(signal?: AbortSignal) {
        setError("");
        try {
            setMenu(await catalogApi.load(signal));
        } catch (e) {
            if (!signal?.aborted) setError(describeCatalogError(e));
        }
    }
    useEffect(() => {
        const c = new AbortController();
        void catalogApi
            .load(c.signal)
            .then(setMenu)
            .catch((failure) => {
                if (!c.signal.aborted) setError(describeCatalogError(failure));
            });
        return () => c.abort();
    }, []);
    async function save(change: CatalogChange, context: Editor) {
        const result = await catalogApi.apply(change);
        setEditor(null);
        if (context.kind === "item" && result.id) setSelected(result.id);
        if (context.kind === "optionGroup" && result.id)
            setOptionSelected(result.id);
        setNotice("Đã lưu. Đang tải lại…");
        try {
            setMenu(await catalogApi.load());
            setNotice("Đã lưu và đọc lại thực đơn từ máy chủ.");
        } catch (e) {
            setError(describeCatalogError(e));
            setNotice("Đã lưu; tải lại để xem dữ liệu mới nhất.");
        }
    }
    const item = menu?.items.find((x) => x.id === selected),
        og = menu?.optionGroups.find((x) => x.id === optionSelected);
    const edit = (value: Editor, label: string) =>
        manage ? (
            <button
                className={styles.secondary}
                onClick={() => setEditor(value)}
            >
                {label}
            </button>
        ) : null;
    return (
        <section className={styles.page}>
            <header className={styles.header}>
                <div>
                    <p className={styles.eyebrow}>THỰC ĐƠN NHÀ HÀNG</p>
                    <h1>QL món ăn</h1>
                    <p>Quản lý món, giá bán, biến thể và topping.</p>
                </div>
                <button
                    className={styles.secondary}
                    onClick={() => void load()}
                >
                    Tải lại
                </button>
            </header>
            {menu && can("catalog.availability.manage") && (
                <AvailabilityEditor menu={menu} />
            )}
            <nav className={styles.tabs} aria-label="Danh mục thực đơn">
                {["Món ăn", "Nhóm món", "Tùy chọn & topping"].map((t) => (
                    <button
                        key={t}
                        aria-pressed={tab === t}
                        onClick={() => setTab(t)}
                    >
                        {t}
                    </button>
                ))}
            </nav>
            {notice && (
                <p role="status" className={styles.notice}>
                    {notice}
                </p>
            )}
            {error && (
                <div role="alert" className={styles.error}>
                    {error} <button onClick={() => void load()}>Thử lại</button>
                </div>
            )}
            {!menu && !error && <p role="status">Đang tải thực đơn…</p>}
            {menu && (
                <>
                    {tab === "Nhóm món" && (
                        <>
                            <div className={styles.toolbar}>
                                <h2>Nhóm món</h2>
                                {edit({ kind: "group" }, "Thêm nhóm món")}
                            </div>
                            <div className={styles.grid}>
                                {menu.groups.map((g) => (
                                    <article key={g.id} className={styles.card}>
                                        <h3>{g.name}</h3>
                                        <p>
                                            Thứ tự: {g.sortOrder} ·{" "}
                                            {g.active
                                                ? "Đang sử dụng"
                                                : "Ngừng sử dụng"}
                                        </p>
                                        {edit(
                                            { kind: "group", existing: g },
                                            `Sửa nhóm ${g.name}`,
                                        )}
                                    </article>
                                ))}
                            </div>
                            {!menu.groups.length && (
                                <p>
                                    Chưa có nhóm món. Tạo nhóm trước khi thêm
                                    món.
                                </p>
                            )}
                        </>
                    )}
                    {tab === "Món ăn" && (
                        <>
                            <div className={styles.toolbar}>
                                <label>
                                    Tìm món
                                    <input
                                        value={search}
                                        onChange={(e) =>
                                            setSearch(e.target.value)
                                        }
                                        placeholder="Tên hoặc mã món"
                                    />
                                </label>
                                {edit({ kind: "item" }, "Thêm món")}
                            </div>
                            {!menu.groups.some((g) => g.active) && (
                                <p>
                                    Tạo nhóm món đang sử dụng trước khi thêm
                                    món.
                                </p>
                            )}
                            <div className={styles.grid}>
                                {menu.items
                                    .filter((x) =>
                                        `${x.name} ${x.code}`
                                            .toLocaleLowerCase()
                                            .includes(
                                                search.toLocaleLowerCase(),
                                            ),
                                    )
                                    .map((x) => (
                                        <article
                                            key={x.id}
                                            className={styles.card}
                                        >
                                            {x.imageObjectKey ? (
                                                <MenuPhoto
                                                    objectKey={x.imageObjectKey}
                                                />
                                            ) : (
                                                <div
                                                    className={
                                                        styles.placeholder
                                                    }
                                                >
                                                    Chưa có ảnh
                                                </div>
                                            )}
                                            <h3>{x.name}</h3>
                                            {manage && (
                                                <PhotoUpload
                                                    item={x}
                                                    refresh={load}
                                                />
                                            )}
                                            <p>
                                                {
                                                    menu.groups.find(
                                                        (g) =>
                                                            g.id === x.groupId,
                                                    )?.name
                                                }{" "}
                                                ·{" "}
                                                {x.station === "BEP"
                                                    ? "Bếp"
                                                    : "Bar"}
                                            </p>
                                            <p>
                                                {x.variants
                                                    .filter((v) => v.active)
                                                    .map((v) =>
                                                        formatPrice(v.price),
                                                    )
                                                    .join(" / ") ||
                                                    "Chưa có biến thể / giá bán"}
                                            </p>
                                            <small>
                                                {x.active
                                                    ? "Đang sử dụng"
                                                    : "Ngừng sử dụng"}
                                            </small>
                                            <button
                                                className={styles.secondary}
                                                aria-label={`Chi tiết món ${x.name}`}
                                                onClick={() =>
                                                    setSelected(x.id)
                                                }
                                            >
                                                Chi tiết món
                                            </button>
                                        </article>
                                    ))}
                            </div>
                            {!menu.items.length && (
                                <p>
                                    Chưa có món ăn. Thêm món và biến thể để đặt
                                    giá bán.
                                </p>
                            )}
                            {item && (
                                <article className={styles.detail}>
                                    <div className={styles.toolbar}>
                                        <h2>{item.name}</h2>
                                        {edit(
                                            { kind: "item", existing: item },
                                            "Sửa món",
                                        )}
                                    </div>
                                    <p>Mã: {item.code}</p>
                                    <div className={styles.toolbar}>
                                        <h3>Biến thể & giá bán</h3>
                                        {edit(
                                            {
                                                kind: "variant",
                                                itemId: item.id,
                                            },
                                            "Thêm biến thể",
                                        )}
                                    </div>
                                    {item.variants.map((v) => (
                                        <div className={styles.row} key={v.id}>
                                            <span>{v.name}</span>
                                            <span>
                                                {formatPrice(v.price)} ·{" "}
                                                {v.active
                                                    ? "Đang sử dụng"
                                                    : "Ngừng sử dụng"}
                                            </span>
                                            {edit(
                                                {
                                                    kind: "variant",
                                                    itemId: item.id,
                                                    existing: v,
                                                },
                                                `Sửa biến thể ${v.name}`,
                                            )}
                                        </div>
                                    ))}
                                    <div className={styles.toolbar}>
                                        <h3>Tùy chọn & topping của món</h3>
                                        {edit(
                                            {
                                                kind: "assignment",
                                                itemId: item.id,
                                            },
                                            "Gắn nhóm tùy chọn",
                                        )}
                                    </div>
                                    {item.optionGroups.map((link) => {
                                        const g = menu.optionGroups.find(
                                            (x) => x.id === link.optionGroupId,
                                        );
                                        return (
                                            <div
                                                className={styles.row}
                                                key={link.optionGroupId}
                                            >
                                                <div>
                                                    <strong>{g?.name}</strong>
                                                    <p>
                                                        Chọn{" "}
                                                        {link.minSelections}–
                                                        {link.maxSelections} ·{" "}
                                                        {g?.options
                                                            .filter(
                                                                (o) => o.active,
                                                            )
                                                            .map(
                                                                (o) =>
                                                                    `${o.name} (+${formatPrice(o.extraPrice)})`,
                                                            )
                                                            .join(", ")}
                                                    </p>
                                                </div>
                                                {edit(
                                                    {
                                                        kind: "assignment",
                                                        itemId: item.id,
                                                        existing: link,
                                                    },
                                                    "Sửa giới hạn",
                                                )}
                                                {manage && (
                                                    <button
                                                        onClick={() => {
                                                            void save(
                                                                {
                                                                    method: "DELETE",
                                                                    path: `items/${item.id}/option-groups/${link.optionGroupId}`,
                                                                },
                                                                {
                                                                    kind: "assignment",
                                                                    itemId: item.id,
                                                                },
                                                            ).catch((e) =>
                                                                setError(
                                                                    describeCatalogError(
                                                                        e,
                                                                    ),
                                                                ),
                                                            );
                                                        }}
                                                    >
                                                        Gỡ nhóm
                                                    </button>
                                                )}
                                            </div>
                                        );
                                    })}
                                </article>
                            )}
                        </>
                    )}
                    {tab === "Tùy chọn & topping" && (
                        <>
                            <div className={styles.toolbar}>
                                <h2>Nhóm tùy chọn & topping</h2>
                                {edit(
                                    { kind: "optionGroup" },
                                    "Thêm nhóm tùy chọn",
                                )}
                            </div>
                            <div className={styles.grid}>
                                {menu.optionGroups.map((g) => (
                                    <article key={g.id} className={styles.card}>
                                        <h3>{g.name}</h3>
                                        <p>
                                            {g.type === "TOPPING"
                                                ? "Topping"
                                                : "Tùy chọn"}{" "}
                                            · {g.options.length} lựa chọn
                                        </p>
                                        <button
                                            className={styles.secondary}
                                            onClick={() =>
                                                setOptionSelected(g.id)
                                            }
                                        >
                                            Chi tiết nhóm {g.name}
                                        </button>
                                        {edit(
                                            {
                                                kind: "optionGroup",
                                                existing: g,
                                            },
                                            `Sửa nhóm ${g.name}`,
                                        )}
                                    </article>
                                ))}
                            </div>
                            {og && (
                                <article className={styles.detail}>
                                    <div className={styles.toolbar}>
                                        <h2>{og.name}</h2>
                                        {edit(
                                            { kind: "option", groupId: og.id },
                                            "Thêm lựa chọn",
                                        )}
                                    </div>
                                    {og.options.map((o) => (
                                        <div className={styles.row} key={o.id}>
                                            <span>{o.name}</span>
                                            <span>
                                                +{formatPrice(o.extraPrice)} ·{" "}
                                                {o.active
                                                    ? "Đang sử dụng"
                                                    : "Ngừng sử dụng"}
                                            </span>
                                            {edit(
                                                {
                                                    kind: "option",
                                                    groupId: og.id,
                                                    existing: o,
                                                },
                                                `Sửa lựa chọn ${o.name}`,
                                            )}
                                        </div>
                                    ))}
                                </article>
                            )}
                        </>
                    )}
                </>
            )}
            {editor && menu && (
                <CatalogEditor
                    key={JSON.stringify(editor)}
                    editor={editor}
                    menu={menu}
                    onClose={() => setEditor(null)}
                    onSave={save}
                />
            )}
        </section>
    );
}
