"use client";

import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { ChangePasswordForm } from "@/features/auth/change-password-form";

export default function ChangePasswordPage() {
    return (
        <div className="space-y-4">
            <div className="flex items-center gap-3 rounded-xl border bg-white px-4 py-3">
                <Link
                    href="/profile"
                    className="p-1 rounded-full hover:bg-gray-100 transition-colors"
                    aria-label="Quay lại hồ sơ"
                >
                    <ArrowLeft className="w-5 h-5 text-gray-700" />
                </Link>
                <h1 className="text-lg font-semibold text-gray-800 md:text-2xl">
                    Đổi mật khẩu
                </h1>
            </div>

            <div className="max-w-lg">
                <div className="bg-white rounded-lg shadow-sm p-4 sm:p-6">
                    <ChangePasswordForm />
                </div>
            </div>
        </div>
    );
}
