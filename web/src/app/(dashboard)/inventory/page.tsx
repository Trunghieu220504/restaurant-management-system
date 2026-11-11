"use client";

import { useState } from "react";
import {
    Package,
    Plus,
    Search,
    FileText,
    ClipboardCheck,
    Sliders,
    Building2,
    Calendar,
    AlertTriangle,
    CheckCircle2,
    ArrowUpRight,
    ChevronDown,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { toast } from "sonner";
import { useRestaurant } from "@/lib/store/restaurant-context";

interface Ingredient {
    id: string;
    name: string;
    unit: string;
    stockQty: number;
    minQty: number;
    unitPrice: number;
    supplier: string;
    lastUpdated: string;
}

const initialIngredients: Ingredient[] = [
    {
        id: "NL-01",
        name: "Sườn heo non tươi",
        unit: "kg",
        stockQty: 18.5,
        minQty: 10,
        unitPrice: 120000,
        supplier: "Công ty Thực Phẩm CP",
        lastUpdated: "11/11/2026",
    },
    {
        id: "NL-02",
        name: "Gạo tẻ ST25",
        unit: "kg",
        stockQty: 45.0,
        minQty: 20,
        unitPrice: 28000,
        supplier: "Đại lý gạo Miền Tây",
        lastUpdated: "11/11/2026",
    },
    {
        id: "NL-03",
        name: "Khoai tây củ vàng",
        unit: "kg",
        stockQty: 4.2,
        minQty: 8,
        unitPrice: 35000,
        supplier: "Nông sản Đà Lạt",
        lastUpdated: "10/11/2026",
    },
    {
        id: "NL-04",
        name: "Trà xanh Thái Nguyên",
        unit: "kg",
        stockQty: 3.0,
        minQty: 2,
        unitPrice: 180000,
        supplier: "Trà Việt JSC",
        lastUpdated: "09/11/2026",
    },
    {
        id: "NL-05",
        name: "Sữa đặc Ông Thọ",
        unit: "hộp",
        stockQty: 24,
        minQty: 12,
        unitPrice: 26000,
        supplier: "Vinamilk Store",
        lastUpdated: "11/11/2026",
    },
];

export default function InventoryPage() {
    const { salesDay } = useRestaurant();
    const [activeTab, setActiveTab] = useState<
        "STOCK" | "IMPORT" | "COUNT" | "ADJUST"
    >("STOCK");
    const [ingredients, setIngredients] =
        useState<Ingredient[]>(initialIngredients);
    const [search, setSearch] = useState("");

    // Modals
    const [importModalOpen, setImportModalOpen] = useState(false);
    const [selectedIngredient, setSelectedIngredient] = useState("");
    const [importQty, setImportQty] = useState(10);
    const [importPrice, setImportPrice] = useState(100000);

    const filtered = ingredients.filter(
        (i) =>
            i.name.toLowerCase().includes(search.toLowerCase()) ||
            i.supplier.toLowerCase().includes(search.toLowerCase()),
    );

    const lowStockCount = ingredients.filter(
        (i) => i.stockQty <= i.minQty,
    ).length;

    const handleCreateImport = (e: React.FormEvent) => {
        e.preventDefault();
        if (!selectedIngredient) {
            toast.error("Vui lòng chọn nguyên liệu!");
            return;
        }
        setIngredients((prev) =>
            prev.map((i) =>
                i.name === selectedIngredient
                    ? { ...i, stockQty: i.stockQty + Number(importQty) }
                    : i,
            ),
        );
        toast.success(
            `Đã tạo phiếu nhập kho: +${importQty} cho ${selectedIngredient} (UC22).`,
        );
        setImportModalOpen(false);
    };

    return (
        <div className="min-h-screen bg-gray-50 flex flex-col space-y-4 pb-20">
            {/* Header */}
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-white p-4 rounded-xl shadow-sm border border-gray-100">
                <div>
                    <h1 className="text-xl font-bold text-gray-900 flex items-center gap-2">
                        <Package className="size-5 text-red-500" />
                        Tab 4: Quản lý Kho nguyên liệu (Inventory)
                    </h1>
                    <p className="text-xs text-gray-500 mt-0.5">
                        Phiếu nhập kho (UC22) • Số đếm tồn cuối ngày (UC23) •
                        Điều chỉnh tồn (UC24)
                    </p>
                </div>

                <button
                    onClick={() => setImportModalOpen(true)}
                    className="flex items-center justify-center gap-1.5 bg-red-500 hover:bg-red-600 text-white text-xs font-semibold px-4 py-2.5 rounded-lg shadow-sm transition-colors"
                >
                    <Plus size={16} /> + Lập phiếu nhập kho (UC22)
                </button>
            </div>

            {/* Top Tabs per Specification */}
            <div className="flex border-b border-gray-200 bg-white rounded-t-xl px-2 pt-2 gap-2 text-sm overflow-x-auto shadow-sm">
                {[
                    {
                        key: "STOCK",
                        label: `Tồn kho nguyên liệu (${ingredients.length})`,
                    },
                    { key: "IMPORT", label: "Phiếu nhập hàng (UC22)" },
                    { key: "COUNT", label: "Số đếm tồn cuối ngày (UC23)" },
                    { key: "ADJUST", label: "Điều chỉnh tồn (UC24)" },
                ].map((tab) => (
                    <button
                        key={tab.key}
                        onClick={() =>
                            setActiveTab(tab.key as typeof activeTab)
                        }
                        className={cn(
                            "px-3 py-2.5 font-medium border-b-2 text-xs sm:text-sm whitespace-nowrap transition-colors",
                            activeTab === tab.key
                                ? "border-red-500 text-red-600 font-bold"
                                : "border-transparent text-gray-500 hover:text-gray-800",
                        )}
                    >
                        {tab.label}
                    </button>
                ))}
            </div>

            {/* Low stock alert */}
            {lowStockCount > 0 && (
                <div className="bg-amber-50 border border-amber-200 rounded-xl p-3.5 flex items-center justify-between text-xs text-amber-900">
                    <div className="flex items-center gap-2">
                        <AlertTriangle
                            size={16}
                            className="text-amber-600 shrink-0"
                        />
                        <span>
                            Có <strong>{lowStockCount}</strong> nguyên liệu dưới
                            định mức tối thiểu (cần nhập thêm sớm).
                        </span>
                    </div>
                    <button
                        onClick={() => setImportModalOpen(true)}
                        className="text-xs font-bold text-amber-800 underline hover:text-amber-900"
                    >
                        Nhập ngay
                    </button>
                </div>
            )}

            {/* Search */}
            <div className="relative">
                <Search
                    size={16}
                    className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400"
                />
                <input
                    type="text"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Tìm nguyên liệu, nhà cung cấp..."
                    className="w-full pl-9 pr-4 py-2.5 bg-white border border-gray-200 rounded-lg text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-red-500 shadow-sm"
                />
            </div>

            {/* Tab Content: Stock List */}
            {activeTab === "STOCK" && (
                <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                    <table className="w-full text-xs">
                        <thead>
                            <tr className="bg-gray-50 border-b text-gray-500 font-semibold text-left">
                                <th className="p-3">Mã NL</th>
                                <th className="p-3">Tên nguyên liệu</th>
                                <th className="p-3">Tồn hiện tại</th>
                                <th className="p-3">Đơn giá quy đổi</th>
                                <th className="p-3">Nhà cung cấp</th>
                                <th className="p-3 text-right">Thao tác</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100">
                            {filtered.map((item) => {
                                const isLow = item.stockQty <= item.minQty;
                                return (
                                    <tr
                                        key={item.id}
                                        className="hover:bg-gray-50/80 transition-colors"
                                    >
                                        <td className="p-3 font-mono font-bold text-gray-700">
                                            {item.id}
                                        </td>
                                        <td className="p-3">
                                            <strong className="text-gray-900">
                                                {item.name}
                                            </strong>
                                            <span className="text-[10px] text-gray-400 block">
                                                Đơn vị: {item.unit}
                                            </span>
                                        </td>
                                        <td className="p-3">
                                            <span
                                                className={cn(
                                                    "font-bold text-sm",
                                                    isLow
                                                        ? "text-amber-600"
                                                        : "text-emerald-700",
                                                )}
                                            >
                                                {item.stockQty} {item.unit}
                                            </span>
                                            {isLow && (
                                                <span className="text-[10px] text-amber-600 block font-semibold">
                                                    (Tối thiểu {item.minQty}{" "}
                                                    {item.unit})
                                                </span>
                                            )}
                                        </td>
                                        <td className="p-3 font-semibold text-gray-800">
                                            {item.unitPrice.toLocaleString(
                                                "vi-VN",
                                            )}{" "}
                                            đ/{item.unit}
                                        </td>
                                        <td className="p-3 text-gray-600">
                                            {item.supplier}
                                        </td>
                                        <td className="p-3 text-right">
                                            <button
                                                onClick={() => {
                                                    setSelectedIngredient(
                                                        item.name,
                                                    );
                                                    setImportModalOpen(true);
                                                }}
                                                className="px-2.5 py-1 bg-red-50 text-red-600 font-semibold rounded hover:bg-red-100 transition-colors"
                                            >
                                                + Nhập kho
                                            </button>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Tab Content: Import Receipts (UC22) */}
            {activeTab === "IMPORT" && (
                <div className="space-y-3">
                    <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm space-y-2 text-xs">
                        <div className="flex justify-between items-center border-b pb-2">
                            <span className="font-bold text-gray-900 text-sm">
                                Phiếu nhập PNK-20261111-01
                            </span>
                            <span className="bg-emerald-100 text-emerald-800 font-bold px-2 py-0.5 rounded-full text-[10px]">
                                Đã duyệt nhập kho
                            </span>
                        </div>
                        <p className="text-gray-600">
                            Nhà cung cấp: <strong>Công ty Thực Phẩm CP</strong>
                        </p>
                        <p className="text-gray-500">
                            Chi tiết: Sườn heo non tươi (x10 kg @ 120.000đ =
                            1.200.000đ)
                        </p>
                        <span className="text-[10px] text-gray-400 block pt-1">
                            Người lập: Quản lý ca (mẫu) • Ngày: 11/11/2026
                        </span>
                    </div>
                </div>
            )}

            {/* Tab Content: End-of-day count (UC23) */}
            {activeTab === "COUNT" && (
                <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm space-y-3 text-xs">
                    <h3 className="font-bold text-gray-900 text-sm">
                        Nhập số đếm tồn cuối ngày (UC23)
                    </h3>
                    <p className="text-gray-500">
                        Theo quy trình UC23: Nhân viên / Bếp / Bar nhập số đếm
                        nháp thực tế cuối ngày bán, Quản lý xem và duyệt để tính
                        giá vốn.
                    </p>
                    <div className="space-y-2 pt-2">
                        {ingredients.slice(0, 3).map((item) => (
                            <div
                                key={item.id}
                                className="flex items-center justify-between p-2.5 border rounded-lg bg-gray-50"
                            >
                                <span className="font-semibold text-gray-800">
                                    {item.name}
                                </span>
                                <div className="flex items-center gap-2">
                                    <span className="text-gray-400">
                                        Sổ sách: {item.stockQty} {item.unit}
                                    </span>
                                    <input
                                        type="number"
                                        step={0.1}
                                        defaultValue={item.stockQty}
                                        className="w-20 px-2 py-1 border rounded bg-white text-right font-bold text-xs"
                                    />
                                    <span>{item.unit}</span>
                                </div>
                            </div>
                        ))}
                    </div>
                    <button
                        onClick={() =>
                            toast.success(
                                "Quản lý đã duyệt số đếm tồn cuối ngày thành công (UC23).",
                            )
                        }
                        className="w-full py-2 bg-emerald-600 hover:bg-emerald-700 text-white font-bold rounded-lg shadow-sm"
                    >
                        Duyệt số đếm kiểm kê (UC23)
                    </button>
                </div>
            )}

            {/* Tab Content: Stock adjustments (UC24) */}
            {activeTab === "ADJUST" && (
                <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm space-y-3 text-xs">
                    <h3 className="font-bold text-gray-900 text-sm">
                        Điều chỉnh tồn kho ở ngày hiện tại (UC24)
                    </h3>
                    <p className="text-gray-500">
                        Dành cho Quản lý điều chỉnh sai số hao hụt, hỏng hóc
                        hoặc thừa thiếu trong ngày chưa chốt.
                    </p>
                    <div className="space-y-3 pt-2">
                        <div>
                            <label className="block font-semibold mb-1">
                                Chọn nguyên liệu điều chỉnh:
                            </label>
                            <select className="w-full p-2 border rounded-lg bg-white">
                                {ingredients.map((i) => (
                                    <option key={i.id} value={i.id}>
                                        {i.name} (Tồn: {i.stockQty} {i.unit})
                                    </option>
                                ))}
                            </select>
                        </div>
                        <div>
                            <label className="block font-semibold mb-1">
                                Lý do điều chỉnh (Bắt buộc):
                            </label>
                            <input
                                type="text"
                                placeholder="VD: Hao hụt sơ chế, vỡ hỏng..."
                                className="w-full p-2 border rounded-lg"
                            />
                        </div>
                        <button
                            onClick={() =>
                                toast.success(
                                    "Đã ghi nhận phiếu điều chỉnh tồn kho (UC24).",
                                )
                            }
                            className="w-full py-2 bg-red-600 hover:bg-red-700 text-white font-bold rounded-lg shadow-sm"
                        >
                            Xác nhận điều chỉnh (UC24)
                        </button>
                    </div>
                </div>
            )}

            {/* Modal: Lập phiếu nhập kho (UC22) */}
            {importModalOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
                    <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border space-y-4 animate-in fade-in zoom-in-95 text-xs">
                        <h3 className="text-base font-bold text-gray-900">
                            Lập phiếu nhập kho (UC22)
                        </h3>
                        <form
                            onSubmit={handleCreateImport}
                            className="space-y-3"
                        >
                            <div>
                                <label className="block font-semibold mb-1">
                                    Nguyên liệu:
                                </label>
                                <select
                                    value={selectedIngredient}
                                    onChange={(e) =>
                                        setSelectedIngredient(e.target.value)
                                    }
                                    required
                                    className="w-full p-2 border rounded-lg bg-white"
                                >
                                    <option value="">
                                        -- Chọn nguyên liệu --
                                    </option>
                                    {ingredients.map((i) => (
                                        <option key={i.id} value={i.name}>
                                            {i.name} ({i.unit})
                                        </option>
                                    ))}
                                </select>
                            </div>
                            <div className="grid grid-cols-2 gap-2">
                                <div>
                                    <label className="block font-semibold mb-1">
                                        Số lượng nhập:
                                    </label>
                                    <input
                                        type="number"
                                        step={0.5}
                                        value={importQty}
                                        onChange={(e) =>
                                            setImportQty(Number(e.target.value))
                                        }
                                        required
                                        className="w-full p-2 border rounded-lg"
                                    />
                                </div>
                                <div>
                                    <label className="block font-semibold mb-1">
                                        Đơn giá (đ):
                                    </label>
                                    <input
                                        type="number"
                                        step={1000}
                                        value={importPrice}
                                        onChange={(e) =>
                                            setImportPrice(
                                                Number(e.target.value),
                                            )
                                        }
                                        required
                                        className="w-full p-2 border rounded-lg"
                                    />
                                </div>
                            </div>
                            <div className="flex gap-2 pt-2 border-t">
                                <button
                                    type="button"
                                    onClick={() => setImportModalOpen(false)}
                                    className="flex-1 py-2 font-semibold text-gray-600 hover:bg-gray-100 rounded-lg"
                                >
                                    Hủy
                                </button>
                                <button
                                    type="submit"
                                    className="flex-1 py-2 font-bold bg-red-600 hover:bg-red-700 text-white rounded-lg"
                                >
                                    Xác nhận nhập kho
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    );
}
