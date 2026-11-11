import { z } from "zod";

// Khớp PasswordValidator mặc định của ASP.NET Identity mà backend đang dùng
// (IdentityModule.cs: RequiredLength = 12, còn lại để mặc định). Validator đó
// xét chữ hoa/thường/số theo ASCII, nên chữ có dấu (ă, ơ...) được tính là
// ký tự đặc biệt.
export const PASSWORD_MIN_LENGTH = 12;
const PASSWORD_MAX_LENGTH = 1024;

export const newPasswordSchema = z
    .string()
    .min(
        PASSWORD_MIN_LENGTH,
        `Mật khẩu mới cần ít nhất ${PASSWORD_MIN_LENGTH} ký tự.`,
    )
    .max(PASSWORD_MAX_LENGTH, "Mật khẩu quá dài.")
    .regex(/[a-z]/, "Cần ít nhất một chữ thường (a-z).")
    .regex(/[A-Z]/, "Cần ít nhất một chữ hoa (A-Z).")
    .regex(/[0-9]/, "Cần ít nhất một chữ số.")
    .regex(/[^a-zA-Z0-9]/, "Cần ít nhất một ký tự đặc biệt.");

export const changePasswordSchema = z
    .object({
        currentPassword: z
            .string()
            .min(1, "Vui lòng nhập mật khẩu hiện tại.")
            .max(PASSWORD_MAX_LENGTH, "Mật khẩu quá dài."),
        newPassword: newPasswordSchema,
        confirmPassword: z.string().min(1, "Vui lòng nhập lại mật khẩu mới."),
    })
    .refine((values) => values.newPassword === values.confirmPassword, {
        message: "Mật khẩu nhập lại không khớp.",
        path: ["confirmPassword"],
    })
    .refine((values) => values.newPassword !== values.currentPassword, {
        message: "Mật khẩu mới phải khác mật khẩu hiện tại.",
        path: ["newPassword"],
    });

export type ChangePasswordValues = z.infer<typeof changePasswordSchema>;

/**
 * Thông báo theo mã HTTP (identity-contract.md mục 1.3): 400 không được nói
 * rõ trường nào sai.
 */
export function changePasswordErrorMessage(status: number | undefined) {
    switch (status) {
        case 400:
            return "Không đổi được mật khẩu. Hãy kiểm tra mật khẩu hiện tại và yêu cầu của mật khẩu mới.";
        case 401:
            return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
        case 429:
            return "Bạn đã thử quá nhiều lần. Vui lòng đợi một phút rồi thử lại.";
        default:
            return "Không kết nối được máy chủ. Vui lòng thử lại.";
    }
}
