import type { Metadata } from "next";
import type { ReactNode } from "react";
import { AppProviders } from "@/components/app-providers";
import { Toaster } from "@/components/ui/sonner";
import "./globals.css";

export const metadata: Metadata = {
    title: "Restaurant Management",
    description: "Restaurant management web application",
};

export default function RootLayout({
    children,
}: Readonly<{
    children: ReactNode;
}>) {
    return (
        <html lang="vi">
            <body>
                <AppProviders>{children}</AppProviders>
                <Toaster richColors position="top-right" />
            </body>
        </html>
    );
}
