"use client";

import { useState } from "react";
import {
    ArrowLeft,
    Search,
    ChevronDown,
    Plus,
    Utensils,
    DollarSign,
    TrendingUp,
} from "lucide-react";
import { useRouter } from "next/navigation";

type Customer = {
    id: number;
    name: string;
    phone: string;
    visits: number;
    spent: number;
};

const customers: Customer[] = [
    {
        id: 1,
        name: "Đăng",
        phone: "+84985623569",
        visits: 2.0,
        spent: 14883699,
    },
    { id: 2, name: "Khách lẻ", phone: "N/A", visits: 0.0, spent: 0 },
    { id: 3, name: "Nguyen", phone: "+84902646558", visits: 0.0, spent: 0 },
];

function formatCurrency(n: number) {
    return n.toLocaleString("vi-VN") + "đ";
}

function CustomerCard({ customer }: { customer: Customer }) {
    const router = useRouter();
    const [expanded, setExpanded] = useState(false);

    return (
        <div className="bg-white rounded-xl shadow-sm mb-3 overflow-hidden">
            <div className="px-4 py-3">
                {/* Top row: name + phone + button */}
                <div className="flex items-center justify-between">
                    <div>
                        <p className="font-semibold text-gray-900">
                            {customer.name}
                        </p>
                        <p className="text-xs text-gray-500 mt-0.5">
                            {customer.phone}
                        </p>
                    </div>
                    <button
                        onClick={() => router.push("/orders")}
                        className="bg-green-500 hover:bg-green-600 transition-colors text-white text-xs font-medium px-3 py-1.5 rounded-lg flex items-center gap-1 shadow-sm"
                    >
                        <Plus size={12} />
                        Tạo đơn
                    </button>
                </div>

                {/* Stats row */}
                <div className="flex items-center gap-4 mt-3">
                    <div className="flex items-center gap-1 text-xs text-gray-500">
                        <Utensils size={13} className="text-gray-400" />
                        <span>{customer.visits.toFixed(1)} lần</span>
                    </div>
                    <div className="flex items-center gap-1 text-xs text-gray-500">
                        <DollarSign size={13} className="text-gray-400" />
                        <span>{formatCurrency(customer.spent)}</span>
                    </div>
                </div>

                {/* Expand toggle */}
                <button
                    onClick={() => setExpanded(!expanded)}
                    className="mt-2 flex items-center gap-1 text-xs text-gray-400"
                >
                    <ChevronDown
                        size={14}
                        className={`transition-transform duration-200 ${expanded ? "rotate-180" : ""}`}
                    />
                    {expanded ? "Thu gọn" : "Xem thêm"}
                </button>
            </div>

            {/* Expanded content */}
            {expanded && (
                <div className="border-t border-gray-100 px-4 py-3 bg-gray-50 text-sm text-gray-600 space-y-1">
                    <div className="flex justify-between">
                        <span>Tên khách hàng</span>
                        <span className="font-medium text-gray-800">
                            {customer.name}
                        </span>
                    </div>
                    <div className="flex justify-between">
                        <span>Số điện thoại</span>
                        <span className="font-medium text-gray-800">
                            {customer.phone}
                        </span>
                    </div>
                    <div className="flex justify-between">
                        <span>Số lần ghé thăm</span>
                        <span className="font-medium text-gray-800">
                            {customer.visits.toFixed(1)}
                        </span>
                    </div>
                    <div className="flex justify-between">
                        <span>Tổng chi tiêu</span>
                        <span className="font-medium text-gray-800">
                            {formatCurrency(customer.spent)}
                        </span>
                    </div>
                </div>
            )}
        </div>
    );
}

export default function CustomersPage() {
    const router = useRouter();
    const [activeTab, setActiveTab] = useState<"ql" | "xepHang">("ql");
    const [search, setSearch] = useState("");

    const filtered = customers.filter(
        (c) =>
            c.name.toLowerCase().includes(search.toLowerCase()) ||
            c.phone.includes(search),
    );

    return (
        <div className="min-h-screen bg-gray-50 flex flex-col">
            {/* Header */}
            <div className="bg-white flex items-center justify-between px-4 py-3 shadow-sm sticky top-0 z-10">
                <button
                    onClick={() => router.back()}
                    className="p-1 text-gray-600"
                >
                    <ArrowLeft size={22} />
                </button>
                <h1 className="text-lg font-semibold text-gray-800">
                    Quản lý khách hàng
                </h1>
                <div className="w-8" />
            </div>

            {/* Tabs */}
            <div className="bg-white border-b border-gray-100 px-4 flex space-x-4">
                <button
                    onClick={() => setActiveTab("ql")}
                    className={`py-3 text-sm font-medium border-b-2 transition-colors ${
                        activeTab === "ql"
                            ? "border-red-500 text-red-500"
                            : "border-transparent text-gray-500"
                    }`}
                >
                    QL Khách hàng
                </button>
                <button
                    onClick={() => setActiveTab("xepHang")}
                    className={`py-3 text-sm font-medium border-b-2 transition-colors ${
                        activeTab === "xepHang"
                            ? "border-red-500 text-red-500"
                            : "border-transparent text-gray-500"
                    }`}
                >
                    Xếp hạng thành viên
                </button>
            </div>

            {activeTab === "ql" ? (
                <div className="flex-1 p-4 space-y-3">
                    {/* Search bar */}
                    <div className="bg-white rounded-xl shadow-sm flex items-center px-3 gap-2">
                        <Utensils size={16} className="text-gray-400" />
                        <input
                            type="text"
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                            placeholder="Tìm khách hàng..."
                            className="flex-1 py-2.5 text-sm text-gray-700 outline-none bg-transparent placeholder-gray-400"
                        />
                        <TrendingUp size={16} className="text-gray-400" />
                        <Search size={16} className="text-gray-400" />
                    </div>

                    {/* Rank dropdown */}
                    <div className="bg-white rounded-xl shadow-sm flex items-center justify-between px-4 py-3">
                        <span className="text-sm text-gray-600">
                            Xếp hạng thành viên
                        </span>
                        <ChevronDown size={16} className="text-gray-400" />
                    </div>

                    {/* Add customer button */}
                    <button className="w-full bg-gray-100 text-gray-500 rounded-xl py-3 text-sm font-medium flex items-center justify-center gap-2">
                        <Plus size={16} />
                        Thêm khách hàng mới
                    </button>

                    {/* Customer list */}
                    <div>
                        {filtered.map((customer) => (
                            <CustomerCard
                                key={customer.id}
                                customer={customer}
                            />
                        ))}
                        {filtered.length === 0 && (
                            <div className="text-center text-gray-400 text-sm py-8">
                                Không tìm thấy khách hàng
                            </div>
                        )}
                    </div>
                </div>
            ) : (
                <div className="flex-1 flex items-center justify-center p-8">
                    <div className="text-center text-gray-400">
                        <TrendingUp
                            size={40}
                            className="mx-auto mb-3 text-gray-300"
                        />
                        <p className="text-sm">Chưa có dữ liệu xếp hạng</p>
                    </div>
                </div>
            )}
        </div>
    );
}
