import { useState } from "react";
import { Utensils } from "lucide-react";
import { formatPrice, type CatalogMenu } from "./api";
import { isSoldOut, unavailableItem } from "./availability";
import {
    AvailabilityEditor,
    useAvailabilityEditor,
} from "./availability-editor";
import { MenuPhoto, PhotoUpload } from "./menu-photo";
import s from "./catalog.module.css";

export function DishBrowser({
    menu,
    manage,
    canChangeStock,
    refresh,
    onAdd,
    onSelect,
}: {
    menu: CatalogMenu;
    manage: boolean;
    canChangeStock: boolean;
    refresh: () => Promise<void>;
    onAdd: () => void;
    onSelect: (id: string) => void;
}) {
    const [groupId, setGroup] = useState("");
    const [search, setSearch] = useState("");
    const { stock, write } = useAvailabilityEditor();
    const groups = new Map(menu.groups.map((g) => [g.id, g]));
    const currentGroup = groups.get(groupId);
    const query = search.trim().toLocaleLowerCase("vi");
    const items = menu.items.filter(
        (item) =>
            (!currentGroup || item.groupId === currentGroup.id) &&
            (item.name + " " + item.code)
                .toLocaleLowerCase("vi")
                .includes(query),
    );
    const snapshot = stock.isError ? undefined : stock.data;
    const day = snapshot?.businessDayId;
    return (
        <div className={s.browser}>
            <nav className={s.categories} aria-label="Lọc theo nhóm món">
                <h2>Danh mục</h2>
                <div>
                    {[{ id: "", name: "Tất cả món" }, ...menu.groups].map(
                        (group) => (
                            <button
                                key={group.id}
                                aria-pressed={
                                    group.id === (currentGroup?.id ?? "")
                                }
                                onClick={() => {
                                    setGroup(group.id);
                                    onSelect("");
                                }}
                            >
                                {group.name}
                            </button>
                        ),
                    )}
                </div>
            </nav>
            <section className={s.dishPanel} aria-label="Danh sách món">
                <div className={s.dishHeader}>
                    <h2>{currentGroup?.name ?? "Tất cả món"}</h2>
                    {manage && (
                        <button className={s.primary} onClick={onAdd}>
                            Thêm món
                        </button>
                    )}
                </div>
                <label className={s.field}>
                    <span>Tìm món</span>
                    <input
                        type="search"
                        placeholder="Tên hoặc mã món"
                        value={search}
                        onChange={(e) => {
                            setSearch(e.target.value);
                            onSelect("");
                        }}
                    />
                </label>
                {stock.isError && (
                    <p role="alert" className={s.error}>
                        Không thể đọc tình trạng bán.{" "}
                        <button onClick={() => void stock.refetch()}>
                            Thử lại
                        </button>
                    </p>
                )}
                {write.isError && (
                    <p role="alert" className={s.error}>
                        Chưa cập nhật được tình trạng bán. Kiểm tra kết nối và
                        quyền thao tác rồi thử lại.
                    </p>
                )}
                {day === null && <p>Chưa mở ngày bán.</p>}
                <div className={s.grid}>
                    {items.map((item) => {
                        const group = groups.get(item.groupId);
                        const active = item.active && group?.active;
                        const sold =
                            !!snapshot && isSoldOut(snapshot, "ITEM", item.id);
                        const unavailable =
                            !!snapshot && unavailableItem(snapshot, menu, item);
                        const selling = !!day && active && !unavailable;
                        const pending =
                            write.isPending &&
                            write.variables?.kind === "ITEM" &&
                            write.variables.targetId === item.id;
                        let state = "Đang bán";
                        if (!active) state = "Ngừng sử dụng";
                        else if (!snapshot)
                            state = stock.isError
                                ? "Chưa rõ trạng thái"
                                : "Đang tải…";
                        else if (!day) state = "Chưa mở bán";
                        else if (sold) state = "Đã hết";
                        else if (unavailable)
                            state = "Đã hết · biến thể hoặc tùy chọn";
                        return (
                            <article
                                key={item.id}
                                className={s.dishCard}
                                data-muted={!active || unavailable}
                            >
                                <div className={s.dishPhoto}>
                                    {item.imageObjectKey ? (
                                        <MenuPhoto
                                            objectKey={item.imageObjectKey}
                                        />
                                    ) : (
                                        <Utensils
                                            size={30}
                                            aria-label="Chưa có ảnh"
                                        />
                                    )}
                                </div>
                                <div className={s.dishBody}>
                                    <div className={s.dishMeta}>
                                        {group?.name} ·{" "}
                                        {item.station === "BEP" ? "Bếp" : "Bar"}
                                    </div>
                                    <h3>{item.name}</h3>
                                    <p>
                                        {item.variants
                                            .filter((v) => v.active)
                                            .map((v) => formatPrice(v.price))
                                            .join(" / ") || "Chưa có giá bán"}
                                    </p>
                                    <div className={s.dishStock}>
                                        <strong
                                            data-selling={!!selling}
                                            role="status"
                                        >
                                            {pending ? "Đang cập nhật…" : state}
                                        </strong>
                                        {canChangeStock && (
                                            <button
                                                type="button"
                                                className={s.stockSwitch}
                                                role="switch"
                                                aria-label={`Bán ${item.name}`}
                                                aria-checked={!!selling}
                                                disabled={
                                                    !day ||
                                                    !active ||
                                                    write.isPending ||
                                                    (unavailable && !sold)
                                                }
                                                onClick={() => {
                                                    if (day)
                                                        write.mutate({
                                                            businessDayId: day,
                                                            kind: "ITEM",
                                                            targetId: item.id,
                                                            soldOut: !sold,
                                                        });
                                                }}
                                            >
                                                <span aria-hidden="true" />
                                            </button>
                                        )}
                                    </div>
                                    <button
                                        className={s.secondary}
                                        aria-label={`Chi tiết món ${item.name}`}
                                        onClick={() => onSelect(item.id)}
                                    >
                                        Chi tiết món
                                    </button>
                                    {manage && (
                                        <PhotoUpload
                                            item={item}
                                            refresh={refresh}
                                        />
                                    )}
                                </div>
                            </article>
                        );
                    })}
                </div>
                {!items.length && (
                    <p>
                        {menu.items.length
                            ? "Không tìm thấy món phù hợp."
                            : "Chưa có món ăn."}
                    </p>
                )}
                {canChangeStock && (
                    <details className={s.advanced}>
                        <summary>
                            Trạng thái biến thể, topping và tùy chọn
                        </summary>
                        <AvailabilityEditor
                            menu={menu}
                            stock={stock}
                            write={write}
                        />
                    </details>
                )}
            </section>
        </div>
    );
}
