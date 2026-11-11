"use client";

import { useState } from "react";
import {
    ArrowLeft,
    Menu,
    Search,
    Calendar,
    ChevronDown,
    Plus,
} from "lucide-react";
import { useRouter } from "next/navigation";

export default function PromotionsPage() {
    const router = useRouter();
    const [searchTerm, setSearchTerm] = useState("");
    const [promoType, setPromoType] = useState("all");
    const [status, setStatus] = useState("all");

    const promotions = [
        {
            id: 1,
            title: "Giảm giá sản phẩm",
            startDate: "19/02/2023",
            endDate: "19/02/2023",
            status: "Đang diễn ra",
            isActive: true,
        },
        {
            id: 2,
            title: "Khuyến mại giờ vàng (11h - 13h)",
            startDate: "10/11/2026",
            endDate: "20/11/2026",
            status: "Sắp diễn ra",
            isActive: false,
        },
    ];

    return (
        <div className="min-h-screen bg-gray-50 flex flex-col">
            {/* Header */}
            <div className="bg-white flex items-center justify-between px-4 py-3 shadow-sm sticky top-0 z-10">
                <button
                    onClick={() => router.back()}
                    className="p-1 rounded-full hover:bg-gray-100 text-gray-700 transition-colors"
                    aria-label="Quay lại"
                >
                    <ArrowLeft size={22} />
                </button>
                <h1 className="text-lg font-semibold text-gray-800">
                    Quản lý khuyến mại
                </h1>
                <button className="p-1 text-gray-600 hover:bg-gray-100 rounded-full">
                    <Menu size={22} />
                </button>
            </div>

            <div className="p-4 space-y-4 max-w-lg mx-auto w-full">
                {/* Search */}
                <div className="relative">
                    <Search
                        size={18}
                        className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400"
                    />
                    <input
                        type="text"
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                        placeholder="Tìm theo tên chương trình khuyến mại"
                        className="w-full pl-10 pr-4 py-2.5 bg-gray-100 rounded-lg text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                </div>

                {/* Date filter */}
                <div className="flex items-center gap-2 bg-gray-100 px-3 py-2.5 rounded-lg text-sm text-gray-700">
                    <Calendar size={18} className="text-gray-500" />
                    <span>19/02/2023 - 19/02/2023</span>
                </div>

                {/* Filter Dropdowns */}
                <div className="grid grid-cols-2 gap-3">
                    <div>
                        <label className="text-xs text-gray-500 block mb-1">
                            Loại khuyến mại
                        </label>
                        <div className="relative">
                            <select
                                value={promoType}
                                onChange={(e) => setPromoType(e.target.value)}
                                className="w-full bg-gray-100 text-sm py-2 px-3 pr-8 rounded-lg appearance-none text-gray-700 focus:outline-none"
                            >
                                <option value="all">Tất cả</option>
                                <option value="discount">Giảm giá món</option>
                                <option value="voucher">Voucher</option>
                            </select>
                            <ChevronDown
                                size={16}
                                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none"
                            />
                        </div>
                    </div>

                    <div>
                        <label className="text-xs text-gray-500 block mb-1">
                            Trạng thái
                        </label>
                        <div className="relative">
                            <select
                                value={status}
                                onChange={(e) => setStatus(e.target.value)}
                                className="w-full bg-gray-100 text-sm py-2 px-3 pr-8 rounded-lg appearance-none text-gray-700 focus:outline-none"
                            >
                                <option value="all">Tất cả</option>
                                <option value="active">Đang diễn ra</option>
                                <option value="upcoming">Sắp diễn ra</option>
                                <option value="ended">Đã kết thúc</option>
                            </select>
                            <ChevronDown
                                size={16}
                                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none"
                            />
                        </div>
                    </div>
                </div>

                {/* Add new button */}
                <button className="w-full bg-blue-600 hover:bg-blue-700 text-white font-medium py-2.5 px-4 rounded-xl flex items-center justify-center gap-1.5 shadow-sm transition-colors">
                    <span>Thêm mới</span>
                    <ChevronDown size={18} />
                </button>

                {/* List Section */}
                <div>
                    <p className="text-xs text-gray-400 font-medium mb-2 uppercase tracking-wide">
                        Khuyen mai
                    </p>
                    <div className="space-y-3">
                        {promotions.map((promo) => (
                            <div
                                key={promo.id}
                                className="bg-white rounded-xl p-4 shadow-sm space-y-2 border border-gray-100"
                            >
                                <h3 className="font-bold text-gray-900 text-base">
                                    {promo.title}
                                </h3>
                                <p className="text-xs text-gray-500">
                                    {promo.startDate} - {promo.endDate}
                                </p>
                                <p className="text-sm font-medium text-blue-600">
                                    {promo.status}
                                </p>

                                <div className="flex items-center justify-between pt-2 border-t border-gray-100">
                                    <span className="text-sm font-semibold text-gray-900">
                                        Hoạt động
                                    </span>
                                    <button className="flex items-center gap-1 bg-gray-100 hover:bg-gray-200 text-gray-700 text-xs font-medium px-3 py-1.5 rounded-lg transition-colors">
                                        <span>Chọn</span>
                                        <ChevronDown size={14} />
                                    </button>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
}
