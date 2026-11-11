"use client";

import { useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { useAuth } from "@/components/auth/auth-provider";
import { Button } from "@/components/ui/button";
import { FormField } from "@/components/ui/form-field";
import { AuthRequestError } from "@/lib/auth/auth-client";
import {
    changePasswordErrorMessage,
    changePasswordSchema,
    PASSWORD_MIN_LENGTH,
    type ChangePasswordValues,
} from "./change-password-schema";

const RATE_LIMIT_COOLDOWN_MS = 60_000;

export function ChangePasswordForm() {
    const { changePassword } = useAuth();
    const [serverError, setServerError] = useState<string | null>(null);
    const [coolingDown, setCoolingDown] = useState(false);

    const form = useForm<ChangePasswordValues>({
        resolver: zodResolver(changePasswordSchema),
        defaultValues: {
            currentPassword: "",
            newPassword: "",
            confirmPassword: "",
        },
    });

    // Bị giới hạn 5 lần/phút: tạm khóa nút gửi một phút (identity-contract.md).
    useEffect(() => {
        if (!coolingDown) return;
        const timer = window.setTimeout(
            () => setCoolingDown(false),
            RATE_LIMIT_COOLDOWN_MS,
        );
        return () => window.clearTimeout(timer);
    }, [coolingDown]);

    async function onSubmit(values: ChangePasswordValues) {
        setServerError(null);
        try {
            await changePassword(values.currentPassword, values.newPassword);
            toast.success("Đã đổi mật khẩu. Vui lòng đăng nhập lại.");
        } catch (error) {
            const status =
                error instanceof AuthRequestError ? error.status : undefined;
            if (status === 429) setCoolingDown(true);
            setServerError(changePasswordErrorMessage(status));
            form.resetField("currentPassword");
        }
    }

    const submitting = form.formState.isSubmitting;

    return (
        <form
            className="grid gap-4"
            onSubmit={form.handleSubmit(onSubmit)}
            noValidate
        >
            <FormField
                autoComplete="current-password"
                control={form.control}
                label="Mật khẩu hiện tại"
                name="currentPassword"
                type="password"
            />
            <FormField
                autoComplete="new-password"
                control={form.control}
                label="Mật khẩu mới"
                name="newPassword"
                type="password"
            />
            <FormField
                autoComplete="new-password"
                control={form.control}
                label="Nhập lại mật khẩu mới"
                name="confirmPassword"
                type="password"
            />
            <p className="text-xs text-muted-foreground">
                Ít nhất {PASSWORD_MIN_LENGTH} ký tự, có chữ hoa, chữ thường, chữ
                số và ký tự đặc biệt. Sau khi đổi, mọi thiết bị đang đăng nhập
                tài khoản này sẽ bị đăng xuất.
            </p>
            {serverError && (
                <p
                    role="alert"
                    className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700"
                >
                    {serverError}
                </p>
            )}
            <Button
                className="w-full sm:w-fit"
                disabled={submitting || coolingDown}
                type="submit"
            >
                {submitting ? "Đang đổi mật khẩu..." : "Đổi mật khẩu"}
            </Button>
        </form>
    );
}
