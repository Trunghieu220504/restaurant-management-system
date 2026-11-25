import Image from "next/image";
import { useState } from "react";
import { authFetch } from "@/lib/auth/auth-client";
import type { MenuItem } from "./api";
import styles from "./catalog.module.css";

export function MenuPhoto({ objectKey }: { objectKey?: string | null }) {
    const match = /^menu\/([a-f0-9]{32})\/([a-f0-9]{64}\.webp)$/.exec(
        objectKey ?? "",
    );
    if (!match) return null;
    return (
        <Image
            unoptimized
            loading="lazy"
            width={160}
            height={120}
            alt=""
            src={`/api/public/menu-images/${match[1]}/${match[2]}`}
            className="aspect-[4/3] w-24 shrink-0 rounded-md object-cover"
        />
    );
}

async function thumbnail(file: File): Promise<Blob> {
    if (
        !["image/jpeg", "image/png", "image/webp"].includes(file.type) ||
        file.size > 10 * 1024 * 1024
    )
        throw Error("Chọn ảnh JPEG, PNG hoặc WebP dưới 10 MB.");
    const bitmap = await createImageBitmap(file);
    try {
        const scale = Math.min(1, 960 / Math.max(bitmap.width, bitmap.height));
        const canvas = document.createElement("canvas");
        canvas.width = Math.max(1, Math.round(bitmap.width * scale));
        canvas.height = Math.max(1, Math.round(bitmap.height * scale));
        canvas
            .getContext("2d")!
            .drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        return await new Promise((resolve, reject) =>
            canvas.toBlob(
                (blob) =>
                    blob?.type === "image/webp" && blob.size <= 1048576
                        ? resolve(blob)
                        : reject(Error("Không thể tối ưu ảnh này.")),
                "image/webp",
                0.82,
            ),
        );
    } finally {
        bitmap.close();
    }
}
export function PhotoUpload({
    item,
    refresh,
}: {
    item: MenuItem;
    refresh: () => Promise<void>;
}) {
    const [busy, setBusy] = useState(false),
        [error, fail] = useState("");
    async function upload(file: File) {
        setBusy(true);
        fail("");
        try {
            const blob = await thumbnail(file);
            const path = `/api/catalog/items/${item.id}/image`;
            const headers = { "Content-Type": "application/json" };
            const signed = await authFetch(path + "/upload", {
                method: "POST",
                headers,
                body: JSON.stringify({ bytes: blob.size }),
            });
            if (!signed.ok)
                throw Error(
                    "Không thể cấp URL tải ảnh. Kiểm tra quyền và cấu hình R2.",
                );
            const { uploadId, uploadUrl } = await signed.json();
            const put = await fetch(uploadUrl, {
                method: "PUT",
                credentials: "omit",
                referrerPolicy: "no-referrer",
                headers: { "Content-Type": "image/webp" },
                body: blob,
            });
            if (!put.ok) throw Error("Tải lên R2 thất bại. Hãy chọn lại ảnh.");
            const attach = await authFetch(path, {
                method: "PUT",
                headers,
                body: JSON.stringify({ uploadId, version: item.version }),
            });
            if (attach.status === 409) {
                await refresh();
                throw Error("Món đã thay đổi. Kiểm tra lại rồi chọn ảnh.");
            }
            if (!attach.ok) throw Error("Không thể gắn ảnh vào món.");
            await refresh();
        } catch (e) {
            fail(e instanceof Error ? e.message : "Không thể tải ảnh.");
        } finally {
            setBusy(false);
        }
    }
    return (
        <div>
            <label className={styles.photoPicker}>
                {item.imageObjectKey ? "Đổi ảnh món" : "Thêm ảnh món"}
                <input
                    aria-label={`Ảnh món ${item.name}`}
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    disabled={busy}
                    onChange={(e) => {
                        const file = e.target.files?.[0];
                        e.target.value = "";
                        if (file) void upload(file);
                    }}
                />
            </label>
            {busy && <p role="status">Đang tải ảnh…</p>}
            {error && <p role="alert">{error}</p>}
        </div>
    );
}
