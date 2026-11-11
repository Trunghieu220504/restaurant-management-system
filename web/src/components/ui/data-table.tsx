"use client";

import {
    flexRender,
    getCoreRowModel,
    getFilteredRowModel,
    useReactTable,
    type ColumnDef,
} from "@tanstack/react-table";
import { useState } from "react";
import { Input } from "@/components/ui/input";

function DataTable<TData, TValue>({
    columns,
    data,
    searchPlaceholder = "Tìm kiếm...",
}: {
    columns: ColumnDef<TData, TValue>[];
    data: TData[];
    searchPlaceholder?: string;
}) {
    const [globalFilter, setGlobalFilter] = useState("");
    const table = useReactTable({
        data,
        columns,
        state: { globalFilter },
        onGlobalFilterChange: setGlobalFilter,
        getCoreRowModel: getCoreRowModel(),
        getFilteredRowModel: getFilteredRowModel(),
    });

    return (
        <div className="space-y-4">
            <Input
                aria-label={searchPlaceholder}
                className="max-w-sm"
                onChange={(event) => setGlobalFilter(event.target.value)}
                placeholder={searchPlaceholder}
                value={globalFilter}
            />
            <div className="overflow-hidden rounded-lg border bg-card">
                <table className="w-full text-left text-sm">
                    <thead className="bg-muted/70 text-muted-foreground">
                        {table.getHeaderGroups().map((group) => (
                            <tr key={group.id}>
                                {group.headers.map((header) => (
                                    <th
                                        key={header.id}
                                        className="h-11 px-4 font-medium"
                                    >
                                        {header.isPlaceholder
                                            ? null
                                            : flexRender(
                                                  header.column.columnDef
                                                      .header,
                                                  header.getContext(),
                                              )}
                                    </th>
                                ))}
                            </tr>
                        ))}
                    </thead>
                    <tbody className="divide-y">
                        {table.getRowModel().rows.length ? (
                            table.getRowModel().rows.map((row) => (
                                <tr key={row.id} className="hover:bg-muted/40">
                                    {row.getVisibleCells().map((cell) => (
                                        <td key={cell.id} className="px-4 py-3">
                                            {flexRender(
                                                cell.column.columnDef.cell,
                                                cell.getContext(),
                                            )}
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : (
                            <tr>
                                <td
                                    colSpan={columns.length}
                                    className="h-24 px-4 text-center text-muted-foreground"
                                >
                                    Không có dữ liệu.
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
}

export { DataTable };
