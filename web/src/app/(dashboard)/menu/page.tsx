"use client";

import { useState } from "react";
import { Search, SlidersHorizontal, Plus, Upload, Download, Pencil, X, ChefHat, Coffee } from "lucide-react";
import { useRestaurant } from "@/lib/store/restaurant-context";
import { toast } from "sonner";

const baseCategories = [
  {
    name: "MÓN KHAI VỊ | APPETIZER",
    items: [
      { id: 1, name: "Cháo sườn リブコンジー", price: 25000, defaultOutOfStock: true, image: null },
      { id: 2, name: "Khoai tây chiên", price: 80000, defaultOutOfStock: true, image: null },
      { id: 3, name: "Món B (Salad cá hồi)", price: 80000, defaultOutOfStock: false, image: null },
      { id: 4, name: "Ngô chiên bơ", price: 40000, defaultOutOfStock: false, image: null },
      { id: 5, name: "Salad rau củ quả", price: 35000, defaultOutOfStock: false, image: null },
      { id: 6, name: "Gỏi cuốn tôm thịt", price: 45000, defaultOutOfStock: true, image: null },
    ],
  },
  {
    name: "MÓN CHÍNH | MAIN DISHES",
    items: [
      { id: 7, name: "Cơm sườn nướng đặc biệt", price: 65000, defaultOutOfStock: false, image: null },
      { id: 8, name: "Bún bò Huế gia truyền", price: 55000, defaultOutOfStock: false, image: null },
    ],
  },
];

export default function MenuPage() {
  const { menuItems, toggleDishStock } = useRestaurant();
  const [activeCategoryIndex, setActiveCategoryIndex] = useState(0);
  const [addDishModalOpen, setAddDishModalOpen] = useState(false);
  const [newDishName, setNewDishName] = useState("");
  const [newDishPrice, setNewDishPrice] = useState(50000);
  const [newDishStation, setNewDishStation] = useState<"BEP" | "BAR">("BEP");

  const getDishOutOfStock = (id: number, fallback: boolean) => {
    const item = menuItems.find((d) => d.id === id);
    return item ? item.outOfStock : fallback;
  };

  const handleToggle = (item: any, isCurrentlyOut: boolean) => {
    toggleDishStock(item.id);
    if (!isCurrentlyOut) {
      toast.warning(`Đã tắt món "${item.name}" (Đã hết)! Thực đơn Khách quét QR và Bếp KDS đã được khóa.`);
    } else {
      toast.success(`Đã mở bán lại món "${item.name}"! Khách quét QR có thể đặt hàng bình thường.`);
    }
  };

  const handleCreateDish = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newDishName.trim()) return;
    toast.success(`Đã thêm món mới "${newDishName}" (${newDishStation === "BEP" ? "Trạm Bếp" : "Trạm Bar"}) vào thực đơn!`);
    setAddDishModalOpen(false);
    setNewDishName("");
  };

  const activeCategory = baseCategories[activeCategoryIndex];

  return (
    <div className="flex flex-col h-full bg-gray-50 min-h-screen">
      {/* Header */}
      <div className="flex items-center justify-between px-4 pt-5 pb-3 bg-white">
        <h1 className="text-xl font-bold text-gray-900">QL món ăn</h1>
        <div className="flex items-center gap-2">
          <button className="p-2 rounded-full hover:bg-gray-100" aria-label="Tìm kiếm">
            <Search size={22} className="text-gray-600" />
          </button>
          <button className="p-2 rounded-full hover:bg-gray-100" aria-label="Bộ lọc">
            <SlidersHorizontal size={22} className="text-gray-600" />
          </button>
        </div>
      </div>

      {/* Action buttons */}
      <div className="flex items-center gap-2 px-4 py-3 bg-white border-b border-gray-100">
        <button
          onClick={() => setAddDishModalOpen(true)}
          className="flex items-center gap-1 bg-orange-500 hover:bg-orange-600 text-white text-sm font-semibold px-4 py-2 rounded-lg"
        >
          <Plus size={16} />
          Thêm món mới
        </button>
        <button className="flex items-center gap-1 border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-medium px-4 py-2 rounded-lg">
          <Upload size={16} />
          Nhập file
        </button>
        <button className="p-2 border border-gray-300 hover:bg-gray-50 rounded-lg" aria-label="Tải xuống">
          <Download size={18} className="text-gray-600" />
        </button>
      </div>

      {/* Section header */}
      <div className="px-4 py-3">
        <span className="text-xs font-bold text-gray-500 tracking-wider uppercase">
          {activeCategory.name}
        </span>
      </div>

      {/* Food cards grid */}
      <div className="flex-1 overflow-y-auto px-4 pb-4">
        <div className="grid grid-cols-2 gap-3">
          {activeCategory.items.map((item) => {
            const isOutOfStock = getDishOutOfStock(item.id, item.defaultOutOfStock);
            return (
              <div key={item.id} className="bg-white rounded-xl shadow-sm overflow-hidden flex flex-col">
                {/* Image area */}
                <div className="relative w-full aspect-square bg-gray-100 flex items-center justify-center">
                  {item.image ? (
                    <img src={item.image} alt={item.name} className="w-full h-full object-cover" />
                  ) : (
                    <div className="flex flex-col items-center justify-center w-full h-full bg-gray-100">
                      <svg
                        xmlns="http://www.w3.org/2000/svg"
                        className="w-12 h-12 text-gray-300"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke="currentColor"
                        strokeWidth={1.5}
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z"
                        />
                      </svg>
                    </div>
                  )}

                  {/* Edit button */}
                  <button
                    className="absolute top-2 right-2 bg-white rounded-full p-1.5 shadow"
                    aria-label="Chỉnh sửa"
                  >
                    <Pencil size={13} className="text-gray-500" />
                  </button>

                  {/* Out of stock overlay */}
                  {isOutOfStock && (
                    <div className="absolute inset-0 flex items-center justify-center bg-black/30">
                      <span className="bg-red-500 text-white text-xs font-bold px-3 py-1 rounded-full">
                        Đã hết
                      </span>
                    </div>
                  )}
                </div>

                {/* Info area */}
                <div className="p-3 flex flex-col gap-1">
                  <p className="text-sm font-semibold text-gray-800 leading-tight line-clamp-2">
                    {item.name}
                  </p>
                  <p className="text-sm text-gray-500">
                    {item.price.toLocaleString("vi-VN")} đ
                  </p>

                  {/* Toggle row */}
                  <div className="flex items-center justify-between mt-1">
                    <span className={`text-xs font-medium ${isOutOfStock ? "text-gray-400" : "text-green-600"}`}>
                      {isOutOfStock ? "Đã hết" : "Đang bán"}
                    </span>
                    <button
                      role="switch"
                      aria-checked={!isOutOfStock}
                      onClick={() => handleToggle(item, isOutOfStock)}
                      className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 ${
                        isOutOfStock ? "bg-gray-300" : "bg-green-500"
                      }`}
                    >
                      <span
                        className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${
                          isOutOfStock ? "translate-x-1" : "translate-x-6"
                        }`}
                      />
                    </button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Bottom category tabs */}
      <div className="flex border-t border-gray-200 bg-white">
        {baseCategories.map((cat, idx) => (
          <button
            key={cat.name}
            onClick={() => setActiveCategoryIndex(idx)}
            className={`flex-1 py-3 text-xs font-bold tracking-wide transition-colors ${
              activeCategoryIndex === idx
                ? "bg-gray-900 text-white"
                : "bg-white text-gray-500 hover:bg-gray-50"
            }`}
          >
            {cat.name}
          </button>
        ))}
      </div>
      {/* Modal: Thêm món mới / Chỉ định trạm Bếp/Bar */}
      {addDishModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl max-w-sm w-full p-5 shadow-2xl border space-y-4 animate-in fade-in zoom-in-95 text-xs">
            <div className="flex items-center justify-between border-b pb-2">
              <h3 className="text-base font-bold text-gray-900">Thêm món mới vào thực đơn</h3>
              <button onClick={() => setAddDishModalOpen(false)} className="p-1 text-gray-400 hover:text-gray-600">
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleCreateDish} className="space-y-3">
              <div>
                <label className="block font-semibold mb-1">Tên món ăn / đồ uống:</label>
                <input
                  type="text"
                  required
                  placeholder="VD: Mì cay hải sản, Trà đào cam sả..."
                  value={newDishName}
                  onChange={(e) => setNewDishName(e.target.value)}
                  className="w-full p-2 border rounded-lg text-sm font-semibold focus:outline-none focus:ring-2 focus:ring-orange-500"
                />
              </div>

              <div>
                <label className="block font-semibold mb-1">Giá bán (đ):</label>
                <input
                  type="number"
                  step={5000}
                  value={newDishPrice}
                  onChange={(e) => setNewDishPrice(Number(e.target.value))}
                  className="w-full p-2 border rounded-lg text-sm font-semibold focus:outline-none focus:ring-2 focus:ring-orange-500"
                />
              </div>

              <div>
                <label className="block font-semibold mb-1">Trạm chế biến (Bếp vs Bar):</label>
                <div className="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    onClick={() => setNewDishStation("BEP")}
                    className={`p-2 rounded-lg border font-bold flex items-center justify-center gap-1.5 transition-colors ${
                      newDishStation === "BEP" ? "bg-orange-50 border-orange-500 text-orange-700" : "bg-gray-50 text-gray-600"
                    }`}
                  >
                    <ChefHat size={14} /> Trạm Bếp
                  </button>
                  <button
                    type="button"
                    onClick={() => setNewDishStation("BAR")}
                    className={`p-2 rounded-lg border font-bold flex items-center justify-center gap-1.5 transition-colors ${
                      newDishStation === "BAR" ? "bg-blue-50 border-blue-500 text-blue-700" : "bg-gray-50 text-gray-600"
                    }`}
                  >
                    <Coffee size={14} /> Trạm Bar
                  </button>
                </div>
              </div>

              <div className="flex gap-2 pt-2 border-t">
                <button
                  type="button"
                  onClick={() => setAddDishModalOpen(false)}
                  className="flex-1 py-2 font-semibold text-gray-600 hover:bg-gray-100 rounded-lg"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="flex-1 py-2 font-bold bg-orange-500 hover:bg-orange-600 text-white rounded-lg shadow-sm"
                >
                  Thêm món ngay
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
