"use client";

import { useState } from "react";
import { ChevronRight, Trash2, Plus, TrendingUp, TrendingDown, Store } from "lucide-react";
import Link from "next/link";

interface RestaurantStore {
  id: number;
  name: string;
  address: string;
  logoText: string;
}

export default function StoresPage() {
  const [stores, setStores] = useState<RestaurantStore[]>([
    {
      id: 1,
      name: "Mì Cay Seoul test-clone",
      address: "1093, Nguyễn Ảnh Thủ, Tân Chánh Hiệp, Quận 12",
      logoText: "SEOUL",
    },
    {
      id: 2,
      name: "SUMIBOZU / GÀ NƯỚNG THAN-clone",
      address: "115 Triệu Việt Vương, Bùi Thị Xuân, Hai Bà Trưng, HN",
      logoText: "炭",
    },
    {
      id: 3,
      name: "Chi nhánh 1",
      address: "Khu đô thị mới An Phú, TP. Thủ Đức",
      logoText: "M",
    },
    {
      id: 4,
      name: "Cơm Thố Anh Nguyễn - Dương Khuê",
      address: "37 Dương Khuê, P. Mai Dịch, Cầu Giấy, Hà Nội",
      logoText: "CƠM",
    },
  ]);

  const handleDelete = (id: number) => {
    setStores(stores.filter((s) => s.id !== id));
  };

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* Header */}
      <div className="bg-white flex items-center justify-center py-4 px-4 border-b border-gray-100 shadow-sm sticky top-0 z-10">
        <h1 className="text-base font-semibold text-gray-800">Danh sách cửa hàng</h1>
      </div>

      <div className="p-4 space-y-4 max-w-lg mx-auto w-full flex-1">
        {/* User bar */}
        <Link
          href="/profile"
          className="bg-white rounded-xl p-3.5 flex items-center justify-between shadow-sm border border-gray-100 hover:bg-gray-50 transition-colors"
        >
          <div className="flex items-center gap-2">
            <span className="font-semibold text-gray-900 text-sm">nguyen the vinh</span>
            <span className="text-xs text-gray-500">+84902646559</span>
          </div>
          <div className="flex items-center gap-1 text-xs text-gray-400">
            <span>1.25.90</span>
            <ChevronRight size={16} />
          </div>
        </Link>

        {/* Chain Revenue Card */}
        <div className="bg-white rounded-xl p-4 shadow-sm border border-gray-100">
          <div className="flex items-center justify-between mb-1">
            <h2 className="font-bold text-gray-900 text-sm">Doanh Thu Toàn Chuỗi</h2>
            <ChevronRight size={16} className="text-gray-400" />
          </div>
          <p className="text-xs text-gray-400 mb-4">19:27 19/02/2023</p>

          <div className="grid grid-cols-2 divide-x divide-gray-100 text-center">
            <div className="pr-3">
              <p className="text-xs text-gray-500 mb-1">Hôm nay</p>
              <p className="text-lg font-bold text-gray-900">188,320</p>
              <p className="text-[11px] text-gray-400 mt-1 flex items-center justify-center gap-0.5">
                <span>So với ngày hôm qua</span>
                <span className="text-green-500 font-medium flex items-center">
                  <TrendingUp size={10} className="inline mr-0.5" /> 0%
                </span>
              </p>
            </div>

            <div className="pl-3">
              <p className="text-xs text-gray-500 mb-1">Tháng này</p>
              <p className="text-lg font-bold text-gray-900">188,320</p>
              <p className="text-[11px] text-gray-400 mt-1 flex items-center justify-center gap-0.5">
                <span>So với tháng trước</span>
                <span className="text-red-500 font-medium flex items-center">
                  <TrendingDown size={10} className="inline mr-0.5" /> -98%
                </span>
              </p>
            </div>
          </div>
        </div>

        {/* Stores List */}
        <div className="space-y-3">
          {stores.map((store) => (
            <div
              key={store.id}
              className="bg-white rounded-xl p-3.5 flex items-center justify-between shadow-sm border border-gray-100 gap-3 hover:border-red-200 transition-colors"
            >
              <Link
                href="/tables"
                className="flex items-center gap-3 flex-1 min-w-0 group"
                title="Chọn cửa hàng này và vào quản lý vận hành"
              >
                <div className="size-11 rounded-full bg-red-50 border border-red-100 flex items-center justify-center font-bold text-xs text-red-600 shrink-0 group-hover:bg-red-500 group-hover:text-white transition-colors">
                  {store.logoText}
                </div>
                <div className="min-w-0 flex-1">
                  <h3 className="font-semibold text-gray-900 text-sm truncate group-hover:text-red-600 transition-colors">
                    {store.name}
                  </h3>
                  <p className="text-xs text-gray-500 truncate mt-0.5">{store.address}</p>
                </div>
                <ChevronRight size={18} className="text-gray-300 group-hover:text-red-500 transition-colors shrink-0" />
              </Link>
              <button
                onClick={() => handleDelete(store.id)}
                className="p-2 text-gray-400 hover:text-red-500 transition-colors shrink-0"
                aria-label="Xóa cửa hàng"
              >
                <Trash2 size={16} />
              </button>
            </div>
          ))}
        </div>

        {/* Add store button */}
        <div className="pt-2">
          <button className="w-full bg-red-500 hover:bg-red-600 text-white font-medium py-3 px-4 rounded-xl flex items-center justify-center gap-2 shadow-sm transition-colors">
            <Plus size={18} />
            <span>Thêm mới cửa hàng</span>
          </button>
        </div>
      </div>
    </div>
  );
}
