"use client";

import { useEffect } from "react";
import { Button } from "@/components/ui/button";
import "./globals.css";

export default function GlobalError({
    error,
    reset,
}: {
    error: Error & { digest?: string };
    reset: () => void;
}) {
    useEffect(() => {
        console.error("Application error:", error);
    }, [error]);

    return (
        <html lang="vi">
            <body className="grid min-h-screen place-items-center p-6">
                <main className="max-w-md space-y-4 text-center">
                    <h1 className="text-2xl font-semibold">
                        Ứng dụng gặp sự cố
                    </h1>
                    <p>Vui lòng thử tải lại nội dung.</p>
                    <Button onClick={reset}>Thử lại</Button>
                </main>
            </body>
        </html>
    );
}
