"use client";
import Link from "next/link";
import { useRef, useState } from "react";
import { useAuth } from "@/components/auth/auth-provider";
import { usePermissions } from "@/components/permission-provider";
import type { components } from "@/lib/api/generated";
import { request, ReadState, useRead } from "@/features/management/api";
import s from "@/features/management/management.module.css";
type Account = components["schemas"]["AccountDto"];
type Action = "create" | "roles" | "reset-password" | "activate" | "deactivate";
type Editor = { action: Action; account?: Account };
const labels: Record<Action, string> = {
    create: "Tạo tài khoản",
    roles: "Đổi vai trò",
    "reset-password": "Đặt lại mật khẩu",
    activate: "Mở lại tài khoản",
    deactivate: "Khóa tài khoản",
};
const roles: Record<string, string> = {
    Staff: "Nhân viên",
    Kitchen: "Bếp",
    Bar: "Bar",
    Manager: "Quản lý",
    Admin: "Quản trị",
};
const root = "/api/identity/accounts";
function AccountForm({
    editor,
    all,
    onClose,
    onSaved,
}: {
    editor: Editor;
    all: boolean;
    onClose: () => void;
    onSaved: () => void;
}) {
    const { action, account } = editor;
    const [error, setError] = useState(""),
        [busy, setBusy] = useState(false);
    const lock = useRef(false);
    const hasRoles = action === "create" || action === "roles";
    return (
        <form
            className={s.card}
            onSubmit={async (e) => {
                e.preventDefault();
                if (lock.current) return;
                const fields = new FormData(e.currentTarget);
                const chosen = fields.getAll("role");
                if (hasRoles && !chosen.length) {
                    setError("Chọn ít nhất một vai trò.");
                    return;
                }
                lock.current = true;
                setBusy(true);
                setError("");
                const payload =
                    action === "create"
                        ? {
                              userName: String(fields.get("name")).trim(),
                              email: String(fields.get("email")).trim(),
                              password: fields.get("password"),
                              roles: chosen,
                          }
                        : action === "roles"
                          ? { roles: chosen }
                          : action === "reset-password"
                            ? { newPassword: fields.get("password") }
                            : undefined;
                try {
                    await request(
                        action === "create"
                            ? root
                            : root + "/" + account!.id + "/" + action,
                        {
                            method: action === "roles" ? "PUT" : "POST",
                            headers: payload
                                ? { "Content-Type": "application/json" }
                                : undefined,
                            body: JSON.stringify(payload),
                        },
                    );
                    onSaved();
                } catch (e) {
                    setError(
                        e instanceof Error
                            ? e.message
                            : "Không thể lưu thay đổi.",
                    );
                } finally {
                    lock.current = false;
                    setBusy(false);
                }
            }}
        >
            <h2>
                {labels[action]} {account?.userName}
            </h2>
            <fieldset disabled={busy} className={s.list}>
                {action === "create" && (
                    <div className={s.toolbar}>
                        <label>
                            Tên đăng nhập
                            <input name="name" required autoComplete="off" />
                        </label>
                        <label>
                            Email
                            <input type="email" name="email" required />
                        </label>
                    </div>
                )}
                {(action === "create" || action === "reset-password") && (
                    <label>
                        Mật khẩu mới
                        <input
                            type="password"
                            name="password"
                            required
                            minLength={12}
                            autoComplete="new-password"
                        />
                    </label>
                )}
                {hasRoles && (
                    <fieldset>
                        <legend>Vai trò</legend>
                        <div className={s.roles}>
                            {Object.entries(roles)
                                .filter(
                                    ([key]) =>
                                        all ||
                                        ["Staff", "Kitchen", "Bar"].includes(
                                            key,
                                        ),
                                )
                                .map(([key, name]) => (
                                    <label key={key}>
                                        <input
                                            type="checkbox"
                                            name="role"
                                            value={key}
                                            defaultChecked={(
                                                account?.roles ?? ["Staff"]
                                            ).includes(key)}
                                        />
                                        {name}
                                    </label>
                                ))}
                        </div>
                    </fieldset>
                )}
                {action === "deactivate" && (
                    <p>
                        Tài khoản này sẽ mất quyền đăng nhập khi bạn xác nhận.
                    </p>
                )}
                {error && <p role="alert">{error}</p>}
                <div className={s.actions}>
                    <button type="submit">
                        {busy ? "Đang lưu…" : "Xác nhận"}
                    </button>
                    <button type="button" onClick={onClose}>
                        Hủy
                    </button>
                </div>
            </fieldset>
        </form>
    );
}
export default function Staff() {
    const { user } = useAuth();
    const { can } = usePermissions();
    const [editor, setEditor] = useState<Editor | null>(null),
        [notice, setNotice] = useState(""),
        [search, setSearch] = useState(""),
        [filter, setFilter] = useState(""),
        [offset, setOffset] = useState(0);
    const result = useRead<components["schemas"]["AccountPage"]>(
        root +
            "?" +
            new URLSearchParams({
                offset: String(offset),
                limit: "20",
                search: filter,
            }),
    );
    const data = result.data,
        all = can("identity.accounts.manage");
    function open(next: Editor) {
        setNotice("");
        setEditor(next);
    }
    return (
        <div className={s.page}>
            <header>
                <h1>Tài khoản nhân viên</h1>
                <Link href="/more">Về mục Thêm</Link>
            </header>
            {notice && <p role="status">{notice}</p>}
            {editor ? (
                <AccountForm
                    key={(editor.account?.id ?? "new") + editor.action}
                    editor={editor}
                    all={all}
                    onClose={() => setEditor(null)}
                    onSaved={() => {
                        setNotice("Đã lưu thay đổi tài khoản.");
                        setEditor(null);
                        if (editor.action === "create") {
                            setOffset(0);
                            setFilter("");
                            setSearch("");
                        }
                        result.reload();
                    }}
                />
            ) : (
                <section className={s.card}>
                    <div className={s.actions}>
                        <form
                            className={s.toolbar}
                            onSubmit={(e) => {
                                e.preventDefault();
                                setOffset(0);
                                setFilter(search.trim());
                            }}
                        >
                            <label>
                                Tìm tài khoản
                                <input
                                    type="search"
                                    placeholder="Tên hoặc email"
                                    value={search}
                                    onChange={(e) => setSearch(e.target.value)}
                                />
                            </label>
                            <button type="submit">Tìm</button>
                        </form>
                        <button
                            className={s.primary}
                            onClick={() => open({ action: "create" })}
                        >
                            Tạo tài khoản
                        </button>
                    </div>
                    <ReadState {...result} />
                    {data && (
                        <>
                            <h2>Danh sách tài khoản ({data.total})</h2>
                            {!data.items.length && (
                                <p>Không có tài khoản phù hợp.</p>
                            )}
                            <ul className={s.list}>
                                {data.items.map((a) => (
                                    <li className={s.row} key={a.id}>
                                        <div>
                                            <strong>{a.userName}</strong>
                                            <small>
                                                {a.email} ·{" "}
                                                {a.roles
                                                    .map((r) => roles[r] ?? r)
                                                    .join(", ")}
                                            </small>
                                            <span>
                                                {!a.active
                                                    ? "Đã khóa"
                                                    : a.lockedOut
                                                      ? "Tạm khóa đăng nhập"
                                                      : "Đang hoạt động"}
                                            </span>
                                        </div>
                                        {a.id !== user?.id && (
                                            <div className={s.actions}>
                                                {(
                                                    [
                                                        "roles",
                                                        a.active
                                                            ? "deactivate"
                                                            : "activate",
                                                        "reset-password",
                                                    ] as Action[]
                                                ).map((action) => (
                                                    <button
                                                        key={action}
                                                        onClick={() =>
                                                            open({
                                                                action,
                                                                account: a,
                                                            })
                                                        }
                                                    >
                                                        {labels[action]}
                                                    </button>
                                                ))}
                                            </div>
                                        )}
                                    </li>
                                ))}
                            </ul>
                            {(offset > 0 ||
                                offset + 20 < Number(data.total)) && (
                                <div className={s.actions}>
                                    <button
                                        disabled={!offset}
                                        onClick={() =>
                                            setOffset((n) =>
                                                Math.max(0, n - 20),
                                            )
                                        }
                                    >
                                        Trang trước
                                    </button>
                                    <button
                                        disabled={
                                            offset + 20 >= Number(data.total)
                                        }
                                        onClick={() => setOffset((n) => n + 20)}
                                    >
                                        Trang sau
                                    </button>
                                </div>
                            )}
                        </>
                    )}
                </section>
            )}
        </div>
    );
}
