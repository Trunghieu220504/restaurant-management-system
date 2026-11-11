"use client";

import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { ChangePasswordForm } from "@/features/auth/change-password-form";

export default function ChangePasswordPage() {
    return (
        <div className="min-h-screen bg-gray-50">
            <div className="bg-white shadow-sm flex items-center gap-3 px-4 py-4 sticky top-0 z-10">
                <Link
                    href="/profile"
                    className="p-1 rounded-full hover:bg-gray-100 transition-colors"
                    aria-label="Quay lại hồ sơ"
                >
                    <ArrowLeft className="w-5 h-5 text-gray-700" />
                </Link>
                <h1 className="text-lg font-semibold text-gray-800">
                    Đổi mật khẩu
                </h1>
            </div>

            <div className="max-w-lg mx-auto px-4 py-6">
                <div className="bg-white rounded-lg shadow-sm p-4 sm:p-6">
                    <ChangePasswordForm />
                </div>
            </div>
        </div>
    );
}
