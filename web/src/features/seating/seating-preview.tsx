"use client";

import { useState } from "react";
import { SeatingBoardView } from "./seating-board";
import { previewGateway } from "./preview-gateway";

export function SeatingPreview() {
    const [role, setRole] = useState("manager");
    return (
        <main className="mx-auto min-h-screen max-w-6xl space-y-5 p-4 sm:p-6">
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                <p>Bản xem thử BAO-2.1 với dữ liệu mẫu trong tab này.</p>
                <label className="flex items-center gap-2">
                    Quyền xem thử
                    <select
                        className="h-9 rounded-md border bg-white px-2"
                        value={role}
                        onChange={(event) => setRole(event.target.value)}
                    >
                        <option value="manager">Quản lý</option>
                        <option value="staff">Nhân viên</option>
                    </select>
                </label>
            </div>
            <SeatingBoardView
                key={role}
                gateway={previewGateway}
                canManageCatalog={role === "manager"}
                canManageSessions
            />
        </main>
    );
}
