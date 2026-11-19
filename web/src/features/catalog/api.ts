import { z } from "zod";
import { authFetch } from "@/lib/auth/auth-client";

const money = z
    .union([z.number(), z.string().regex(/^\d+$/)])
    .transform(Number)
    .pipe(z.number().int().nonnegative().max(Number.MAX_SAFE_INTEGER));
const entry = z.object({
    id: z.string(),
    name: z.string(),
    active: z.boolean(),
    version: z.string(),
});
const groupSchema = entry.extend({ sortOrder: z.number().int() });
const variantSchema = entry.extend({
    itemId: z.string(),
    code: z.string(),
    price: money,
});
const optionSchema = entry.extend({
    optionGroupId: z.string(),
    extraPrice: money,
});
const assignmentSchema = z.object({
    optionGroupId: z.string(),
    minSelections: z.number().int().nonnegative(),
    maxSelections: z.number().int().nonnegative(),
});
const itemSchema = entry.extend({
    groupId: z.string(),
    code: z.string(),
    station: z.enum(["BEP", "BAR"]),
    imageObjectKey: z.string().nullable(),
    variants: z.array(variantSchema),
    optionGroups: z.array(assignmentSchema),
});
const optionGroupSchema = entry.extend({
    type: z.enum(["TOPPING", "TUY_CHON"]),
    options: z.array(optionSchema),
});
const menuSchema = z.object({
    groups: z.array(groupSchema),
    items: z.array(itemSchema),
    optionGroups: z.array(optionGroupSchema),
});

export type CatalogMenu = z.infer<typeof menuSchema>;
export type MenuGroup = CatalogMenu["groups"][number];
export type MenuItem = CatalogMenu["items"][number];
export type MenuVariant = MenuItem["variants"][number];
export type OptionGroup = CatalogMenu["optionGroups"][number];
export type MenuOption = OptionGroup["options"][number];
export type Assignment = MenuItem["optionGroups"][number];
export type CatalogChange = {
    method: "POST" | "PUT" | "DELETE";
    path: string;
    body?: Record<string, unknown>;
};

export class CatalogError extends Error {
    constructor(
        readonly status: number,
        readonly code: string,
    ) {
        super(code);
        this.name = "CatalogError";
    }
}

export function describeCatalogError(error: unknown): string {
    if (error instanceof CatalogError) {
        if (error.status === 401)
            return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
        if (error.status === 403)
            return "Bạn không có quyền quản lý thực đơn. Vui lòng liên hệ quản lý.";
        if (error.status === 400)
            return "Thông tin chưa hợp lệ. Kiểm tra các trường và giới hạn lựa chọn.";
        if (error.status === 404)
            return "Dữ liệu không còn tồn tại. Đóng biểu mẫu và tải lại thực đơn.";
        if (
            error.code.includes("code_conflict") ||
            error.code.includes("duplicate") ||
            error.code.includes("code_exists")
        )
            return "Mã đã được sử dụng. Vui lòng nhập mã khác.";
        if (
            error.code.includes("concurrency") ||
            error.code.includes("version") ||
            error.code.includes("stale")
        )
            return "Dữ liệu đã thay đổi ở nơi khác. Đóng biểu mẫu, tải lại và thử lại.";
        if (error.status === 409)
            return "Dữ liệu đã thay đổi hoặc không thể dùng với trạng thái hiện tại. Đóng biểu mẫu và tải lại thực đơn.";
        return "Máy chủ chưa xử lý được yêu cầu. Vui lòng thử lại sau.";
    }
    if (error instanceof z.ZodError)
        return "Dữ liệu trả về không đúng định dạng. Vui lòng tải lại hoặc liên hệ quản lý.";
    return "Không kết nối được máy chủ. Tải lại thực đơn để kiểm tra dữ liệu trước khi lưu lại.";
}

export function parseMenu(raw: unknown): CatalogMenu {
    return menuSchema.parse(raw);
}

export function createCatalogApi(request: typeof fetch = authFetch) {
    async function send(path: string, init: RequestInit = {}) {
        const response = await request(`/api/catalog/${path}`, {
            ...init,
            cache: "no-store",
        });
        if (!response.ok) {
            const problem = await response.json().catch(() => ({}));
            throw new CatalogError(
                response.status,
                typeof problem.code === "string" ? problem.code : "unknown",
            );
        }
        return response;
    }
    return {
        async load(signal?: AbortSignal) {
            return parseMenu(await (await send("menu", { signal })).json());
        },
        async apply(change: CatalogChange): Promise<{ id?: string }> {
            const response = await send(change.path, {
                method: change.method,
                headers: change.body
                    ? { "Content-Type": "application/json" }
                    : undefined,
                body: change.body ? JSON.stringify(change.body) : undefined,
            });
            if (response.status === 204) return {};
            return z
                .object({ id: z.string().optional() })
                .parse(await response.json());
        },
    };
}

export const catalogApi = createCatalogApi();
export const formatPrice = (value: number) =>
    `${value.toLocaleString("vi-VN")} đ`;
