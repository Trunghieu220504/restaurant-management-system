"use client";
import { useState, type ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

// Poll committed snapshots while the operations hub is unavailable on develop.
export const liveInterval = 750;
export function LiveQueryScope({ children }: { children: ReactNode }) {
    const [client] = useState(
        () =>
            new QueryClient({ defaultOptions: { queries: { retry: false } } }),
    );
    return (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
}
