"use client";

import {
    cloneElement,
    useEffect,
    useId,
    useRef,
    useState,
    type ReactElement,
} from "react";
import { X } from "lucide-react";
import {
    CatalogError,
    describeCatalogError,
    type Assignment,
    type CatalogChange,
    type CatalogMenu,
    type MenuGroup,
    type MenuItem,
    type MenuOption,
    type MenuVariant,
    type OptionGroup,
} from "./api";
import { integerValue, requiredText, selectionLimits } from "./validation";
import styles from "./catalog.module.css";

export type Editor =
    | { kind: "group"; existing?: MenuGroup }
    | { kind: "item"; existing?: MenuItem; groupId?: string }
    | { kind: "variant"; itemId: string; existing?: MenuVariant }
    | { kind: "optionGroup"; existing?: OptionGroup }
    | { kind: "option"; groupId: string; existing?: MenuOption }
    | { kind: "assignment"; itemId: string; existing?: Assignment };

const titles = {
    group: "nhóm món",
    item: "món ăn",
    variant: "biến thể",
    optionGroup: "nhóm tùy chọn",
    option: "lựa chọn",
    assignment: "nhóm cho món",
};

function Field({
    label,
    children,
    hint,
}: {
    label: string;
    children: ReactElement<{
        id?: string;
        "aria-label"?: string;
        "aria-describedby"?: string;
    }>;
    hint?: string;
}) {
    const id = useId();
    return (
        <div className={styles.field}>
            <label htmlFor={id}>{label}</label>
            {cloneElement(children, {
                id,
                "aria-label": label,
                "aria-describedby": hint ? `${id}-hint` : undefined,
            })}
            {hint && <small id={`${id}-hint`}>{hint}</small>}
        </div>
    );
}

export function CatalogEditor({
    editor,
    menu,
    onClose,
    onSave,
}: {
    editor: Editor;
    menu: CatalogMenu;
    onClose: () => void;
    onSave: (change: CatalogChange, editor: Editor) => Promise<void>;
}) {
    const dialog = useRef<HTMLDialogElement>(null);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");
    const assigned =
        editor.kind === "assignment"
            ? (menu.items.find((item) => item.id === editor.itemId)
                  ?.optionGroups ?? [])
            : [];
    const availableOptionGroups = menu.optionGroups.filter(
        (group) =>
            (editor.kind === "assignment" &&
                editor.existing?.optionGroupId === group.id) ||
            (group.active &&
                group.options.some((option) => option.active) &&
                !assigned.some((link) => link.optionGroupId === group.id)),
    );
    const [optionGroupId, setOptionGroupId] = useState(
        editor.kind === "assignment"
            ? (editor.existing?.optionGroupId ??
                  availableOptionGroups[0]?.id ??
                  "")
            : "",
    );
    const selectedOptionGroup = menu.optionGroups.find(
        (group) => group.id === optionGroupId,
    );
    const availableOptions =
        selectedOptionGroup?.options.filter((option) => option.active).length ??
        0;
    const existing = editor.existing;
    const title = `${existing ? "Chỉnh sửa" : editor.kind === "assignment" ? "Gắn" : "Thêm"} ${titles[editor.kind]}`;

    useEffect(() => {
        const element = dialog.current;
        if (element && !element.open) element.showModal();
        return () => {
            element?.close();
        };
    }, []);

    function buildChange(data: FormData): CatalogChange {
        const name = () => requiredText(data.get("name"), "Tên");
        const code = () => {
            const value = requiredText(data.get("code"), "Mã", 50);
            if (!/^[A-Za-z0-9_-]+$/.test(value))
                throw new Error(
                    "Mã chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc gạch dưới.",
                );
            return value;
        };
        const lifecycle =
            existing && "version" in existing
                ? {
                      version: existing.version,
                      active: data.get("active") === "on",
                  }
                : {};
        const method = existing ? "PUT" : "POST";
        switch (editor.kind) {
            case "group":
                return {
                    method,
                    path: `groups${editor.existing ? `/${editor.existing.id}` : ""}`,
                    body: {
                        name: name(),
                        sortOrder: integerValue(
                            data.get("sortOrder"),
                            "Thứ tự",
                            0,
                            2147483647,
                        ),
                        ...lifecycle,
                    },
                };
            case "item":
                return {
                    method,
                    path: `items${editor.existing ? `/${editor.existing.id}` : ""}`,
                    body: {
                        name: name(),
                        code: code(),
                        groupId: requiredText(data.get("groupId"), "Nhóm món"),
                        station: requiredText(
                            data.get("station"),
                            "Trạm chế biến",
                        ),
                        ...(!existing && {
                            initialVariant: {
                                code: "STD",
                                name: "Tiêu chuẩn",
                                price: integerValue(
                                    data.get("price"),
                                    "Giá bán",
                                ),
                            },
                        }),
                        ...lifecycle,
                    },
                };
            case "variant":
                return {
                    method,
                    path: `items/${editor.itemId}/variants${editor.existing ? `/${editor.existing.id}` : ""}`,
                    body: {
                        name: name(),
                        code: code(),
                        price: integerValue(data.get("price"), "Giá bán"),
                        ...lifecycle,
                    },
                };
            case "optionGroup":
                return {
                    method,
                    path: `option-groups${editor.existing ? `/${editor.existing.id}` : ""}`,
                    body: {
                        name: name(),
                        type: requiredText(data.get("type"), "Loại nhóm"),
                        ...lifecycle,
                    },
                };
            case "option":
                return {
                    method,
                    path: `option-groups/${editor.groupId}/options${editor.existing ? `/${editor.existing.id}` : ""}`,
                    body: {
                        name: name(),
                        extraPrice: integerValue(
                            data.get("extraPrice"),
                            "Giá cộng thêm",
                        ),
                        ...lifecycle,
                    },
                };
            case "assignment": {
                if (!optionGroupId)
                    throw new Error(
                        "Hãy tạo nhóm và ít nhất một lựa chọn trước khi gắn vào món.",
                    );
                return {
                    method: "PUT",
                    path: `items/${editor.itemId}/option-groups/${optionGroupId}`,
                    body: selectionLimits(
                        integerValue(data.get("minSelections"), "Tối thiểu"),
                        integerValue(data.get("maxSelections"), "Tối đa", 1),
                        availableOptions,
                    ),
                };
            }
        }
    }

    return (
        <dialog
            ref={dialog}
            className={styles.dialog}
            aria-labelledby="catalog-editor-title"
            onCancel={(event) => {
                event.preventDefault();
                if (!saving) onClose();
            }}
        >
            <div className={styles.dialogHeader}>
                <h2 id="catalog-editor-title">{title}</h2>
                <button
                    type="button"
                    className={styles.iconButton}
                    onClick={onClose}
                    disabled={saving}
                    aria-label="Đóng biểu mẫu"
                >
                    <X size={20} />
                </button>
            </div>
            <form
                onSubmit={async (event) => {
                    event.preventDefault();
                    setError("");
                    let change: CatalogChange;
                    try {
                        change = buildChange(new FormData(event.currentTarget));
                    } catch (failure) {
                        setError(
                            failure instanceof Error
                                ? failure.message
                                : "Kiểm tra thông tin đã nhập.",
                        );
                        return;
                    }
                    setSaving(true);
                    try {
                        await onSave(change, editor);
                    } catch (failure) {
                        setError(
                            failure instanceof CatalogError
                                ? describeCatalogError(failure)
                                : describeCatalogError(failure),
                        );
                        setSaving(false);
                    }
                }}
            >
                <fieldset className={styles.formFields} disabled={saving}>
                    {editor.kind !== "assignment" && (
                        <Field label={`Tên ${titles[editor.kind]}`}>
                            <input
                                name="name"
                                required
                                maxLength={200}
                                defaultValue={
                                    existing && "name" in existing
                                        ? existing.name
                                        : ""
                                }
                                autoFocus
                            />
                        </Field>
                    )}
                    {(editor.kind === "item" || editor.kind === "variant") && (
                        <Field
                            label={
                                editor.kind === "item"
                                    ? "Mã món"
                                    : "Mã biến thể"
                            }
                            hint="Chữ không dấu, số, gạch ngang hoặc gạch dưới. Tối đa 50 ký tự."
                        >
                            <input
                                name="code"
                                required
                                maxLength={50}
                                pattern="[A-Za-z0-9_\-]+"
                                defaultValue={editor.existing?.code ?? ""}
                            />
                        </Field>
                    )}
                    {editor.kind === "group" && (
                        <Field label="Thứ tự hiển thị">
                            <input
                                name="sortOrder"
                                type="number"
                                min={0}
                                max={2147483647}
                                step={1}
                                required
                                defaultValue={editor.existing?.sortOrder ?? 0}
                            />
                        </Field>
                    )}
                    {editor.kind === "item" && (
                        <>
                            <Field label="Nhóm món">
                                <select
                                    name="groupId"
                                    required
                                    defaultValue={
                                        editor.existing?.groupId ??
                                        editor.groupId ??
                                        menu.groups.find(
                                            (group) => group.active,
                                        )?.id ??
                                        ""
                                    }
                                >
                                    {menu.groups
                                        .filter(
                                            (group) =>
                                                group.active ||
                                                group.id ===
                                                    editor.existing?.groupId,
                                        )
                                        .map((group) => (
                                            <option
                                                key={group.id}
                                                value={group.id}
                                            >
                                                {group.name}
                                                {!group.active
                                                    ? " (ngừng sử dụng)"
                                                    : ""}
                                            </option>
                                        ))}
                                </select>
                            </Field>
                            <Field label="Trạm chế biến">
                                <select
                                    name="station"
                                    defaultValue={
                                        editor.existing?.station ?? "BEP"
                                    }
                                >
                                    <option value="BEP">Bếp</option>
                                    <option value="BAR">Bar</option>
                                </select>
                            </Field>
                            {!existing && (
                                <Field label="Giá biến thể tiêu chuẩn (đ)">
                                    <input
                                        type="number"
                                        name="price"
                                        min="0"
                                        step="1"
                                        required
                                    />
                                </Field>
                            )}
                        </>
                    )}
                    {editor.kind === "variant" && (
                        <Field
                            label="Giá bán (đ)"
                            hint="Giá đầy đủ của biến thể, đơn vị đồng."
                        >
                            <input
                                name="price"
                                type="number"
                                min={0}
                                max={Number.MAX_SAFE_INTEGER}
                                step={1}
                                required
                                defaultValue={editor.existing?.price ?? ""}
                            />
                        </Field>
                    )}
                    {editor.kind === "optionGroup" && (
                        <Field label="Loại nhóm">
                            <select
                                name="type"
                                defaultValue={
                                    editor.existing?.type ?? "TOPPING"
                                }
                            >
                                <option value="TOPPING">Topping</option>
                                <option value="TUY_CHON">Tùy chọn</option>
                            </select>
                        </Field>
                    )}
                    {editor.kind === "option" && (
                        <Field
                            label="Giá cộng thêm (đ)"
                            hint="Nhập 0 cho lựa chọn không tính thêm tiền."
                        >
                            <input
                                name="extraPrice"
                                type="number"
                                min={0}
                                max={Number.MAX_SAFE_INTEGER}
                                step={1}
                                required
                                defaultValue={editor.existing?.extraPrice ?? 0}
                            />
                        </Field>
                    )}
                    {editor.kind === "assignment" && (
                        <>
                            <Field label="Nhóm tùy chọn / topping">
                                <select
                                    value={optionGroupId}
                                    onChange={(event) =>
                                        setOptionGroupId(event.target.value)
                                    }
                                    required
                                    disabled={!!existing}
                                    autoFocus
                                >
                                    {!availableOptionGroups.length && (
                                        <option value="">
                                            Chưa có nhóm phù hợp
                                        </option>
                                    )}
                                    {availableOptionGroups.map((group) => (
                                        <option key={group.id} value={group.id}>
                                            {group.name}
                                        </option>
                                    ))}
                                </select>
                            </Field>
                            <p className={styles.hint}>
                                {availableOptionGroups.length
                                    ? `Nhóm có ${availableOptions} lựa chọn đang sử dụng. Nhập tối thiểu 0 nếu khách không bắt buộc chọn.`
                                    : "Tạo nhóm và thêm lựa chọn tại mục Tùy chọn & topping trước khi gắn vào món."}
                            </p>
                            <div className={styles.twoColumns}>
                                <Field label="Số lựa chọn tối thiểu">
                                    <input
                                        name="minSelections"
                                        type="number"
                                        required
                                        min={0}
                                        max={availableOptions}
                                        step={1}
                                        defaultValue={
                                            editor.existing?.minSelections ?? 0
                                        }
                                    />
                                </Field>
                                <Field label="Số lựa chọn tối đa">
                                    <input
                                        name="maxSelections"
                                        type="number"
                                        required
                                        min={1}
                                        max={availableOptions || 1}
                                        step={1}
                                        defaultValue={
                                            editor.existing?.maxSelections ?? 1
                                        }
                                    />
                                </Field>
                            </div>
                        </>
                    )}
                    {existing && "active" in existing && (
                        <label className={styles.checkbox}>
                            <input
                                name="active"
                                type="checkbox"
                                defaultChecked={existing.active}
                            />
                            <span>
                                Đang sử dụng
                                <small>
                                    Bỏ chọn để ngừng sử dụng trong thực đơn.
                                </small>
                            </span>
                        </label>
                    )}
                </fieldset>
                {error && (
                    <p role="alert" className={styles.error}>
                        {error}
                    </p>
                )}
                <div className={styles.dialogFooter}>
                    <button
                        type="button"
                        className={styles.secondary}
                        disabled={saving}
                        onClick={onClose}
                    >
                        Hủy
                    </button>
                    <button
                        type="submit"
                        className={styles.primary}
                        disabled={
                            saving ||
                            (editor.kind === "assignment" && !optionGroupId)
                        }
                    >
                        {saving ? "Đang lưu…" : "Lưu"}
                    </button>
                </div>
            </form>
        </dialog>
    );
}
