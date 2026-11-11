"use client";

import { useState, useMemo, Suspense } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import Link from "next/link";
import {
  UtensilsCrossed,
  ShoppingCart,
  Clock,
  CheckCircle2,
  AlertCircle,
  Plus,
  Minus,
  ChefHat,
  Bell,
  ArrowRight,
  User,
  Coffee,
  Sparkles,
  QrCode,
  Check,
} from "lucide-react";
import { useRestaurant, type MenuItem } from "@/lib/store/restaurant-context";
import { cn } from "@/lib/utils";
import { toast } from "sonner";

function GuestMenuContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const tableParam = searchParams.get("table") || "T04";

  const { tables, orders, menuItems, createOrder, salesDay } = useRestaurant();

  // Find table info
  const currentTable = tables.find((t) => t.id === tableParam) || tables[0];

  // Find existing order for this table if any
  const activeOrder = useMemo(() => {
    return orders.find(
      (o) =>
        o.tableId === currentTable.id &&
        (o.status === "CHO_XAC_NHAN" || o.status === "DANG_PHUC_VU" || o.status === "CHO_THANH_TOAN"),
    );
  }, [orders, currentTable.id]);

  // Cart state: map dishId -> { quantity, note }
  const [cart, setCart] = useState<Record<number, { quantity: number; note: string }>>({});
  const [activeCategory, setActiveCategory] = useState<string>("ALL");
  const [orderTrackingOpen, setOrderTrackingOpen] = useState(false);

  const categories = [
    { key: "ALL", label: "Tất cả" },
    { key: "KHAI_VI", label: "Khai vị" },
    { key: "MON_CHINH", label: "Món chính" },
    { key: "DO_UONG", label: "Đồ uống" },
  ];

  const filteredDishes = menuItems.filter((dish) => {
    if (activeCategory === "ALL") return true;
    return dish.category === activeCategory;
  });

  const cartItemsCount = Object.values(cart).reduce((s, it) => s + it.quantity, 0);
  const cartTotalPrice = Object.entries(cart).reduce((sum, [dishIdStr, item]) => {
    const dish = menuItems.find((d) => d.id === Number(dishIdStr));
    return sum + (dish ? dish.price * item.quantity : 0);
  }, 0);

  const handleAddToCart = (dish: MenuItem) => {
    if (dish.outOfStock) {
      toast.error(`Món "${dish.name}" đã hết ở bếp, không thể gọi!`);
      return;
    }
    setCart((prev) => {
      const current = prev[dish.id];
      const newQty = (current?.quantity || 0) + 1;
      return {
        ...prev,
        [dish.id]: { quantity: newQty, note: current?.note || "" },
      };
    });
  };

  const handleRemoveFromCart = (dishId: number) => {
    setCart((prev) => {
      const current = prev[dishId];
      if (!current) return prev;
      if (current.quantity <= 1) {
        const next = { ...prev };
        delete next[dishId];
        return next;
      }
      return {
        ...prev,
        [dishId]: { ...current, quantity: current.quantity - 1 },
      };
    });
  };

  const handleSubmitGuestOrder = () => {
    if (cartItemsCount === 0) {
      toast.error("Vui lòng chọn món trước khi gửi đợt gọi!");
      return;
    }

    const items = Object.entries(cart).map(([dishIdStr, val]) => ({
      dishId: Number(dishIdStr),
      quantity: val.quantity,
      note: val.note || undefined,
    }));

    const newOrderId = createOrder(
      currentTable.id,
      items,
      `Khách ${currentTable.name}`,
      false,
    );

    setCart({});
    setOrderTrackingOpen(true);
    toast.success(`Đã gửi đợt gọi món ${newOrderId}! Vui lòng chờ nhân viên xác nhận.`);
  };

  return (
    <div className="min-h-screen bg-slate-50 text-slate-800 pb-28 max-w-md mx-auto relative shadow-2xl border-x">
      {/* Top Banner: QR Table Identity */}
      <div className="sticky top-0 z-30 bg-white/95 backdrop-blur border-b border-slate-200 px-4 py-3 flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="size-9 rounded-xl bg-red-500 text-white flex items-center justify-center font-black text-sm shadow-md shadow-red-200">
            R
          </div>
          <div>
            <div className="flex items-center gap-1.5">
              <span className="font-bold text-slate-900 text-sm">{currentTable.name}</span>
              <span className="text-[10px] bg-red-100 text-red-700 font-bold px-2 py-0.5 rounded-full">
                {currentTable.area}
              </span>
            </div>
            <p className="text-[11px] text-slate-400">Khách tự gọi món qua mã QR (UC06)</p>
          </div>
        </div>

        <Link
          href="/login"
          className="text-xs bg-slate-100 hover:bg-slate-200 text-slate-700 font-semibold px-2.5 py-1.5 rounded-xl transition-colors flex items-center gap-1"
        >
          <User size={13} />
          <span>Đăng nhập</span>
        </Link>
      </div>

      {/* Switch table pill for demo */}
      <div className="bg-red-50/60 px-4 py-2 border-b border-red-100 flex items-center justify-between text-xs">
        <span className="text-red-800 font-medium flex items-center gap-1">
          <QrCode size={13} className="text-red-500" />
          Đang quét QR tại: <strong>{currentTable.name}</strong>
        </span>
        <div className="flex gap-1 text-[11px]">
          {tables.slice(0, 3).map((t) => (
            <button
              key={t.id}
              onClick={() => router.push(`/guest?table=${t.id}`)}
              className={cn(
                "px-2 py-0.5 rounded font-bold transition-colors",
                t.id === currentTable.id
                  ? "bg-red-500 text-white"
                  : "bg-white text-slate-600 hover:bg-slate-200",
              )}
            >
              {t.name}
            </button>
          ))}
        </div>
      </div>

      {/* Ongoing Order Progress Banner (UC33) */}
      {activeOrder && (
        <div className="p-3 bg-white border-b border-slate-200 space-y-2">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-1.5">
              <span className="font-bold text-xs text-slate-900">Tiến độ đơn: {activeOrder.id}</span>
              <span className="text-[10px] text-slate-400">({activeOrder.createdAt})</span>
            </div>
            <button
              onClick={() => setOrderTrackingOpen(!orderTrackingOpen)}
              className="text-xs text-red-600 font-bold hover:underline"
            >
              {orderTrackingOpen ? "Thu gọn" : "Xem chi tiết tiến độ"}
            </button>
          </div>

          {/* Progress stepper */}
          <div className="grid grid-cols-4 gap-1 text-center text-[10px] pt-1">
            <div
              className={cn(
                "p-1.5 rounded-lg font-bold border",
                activeOrder.status === "CHO_XAC_NHAN"
                  ? "bg-amber-100 text-amber-800 border-amber-300"
                  : "bg-slate-100 text-slate-500 border-slate-200",
              )}
            >
              1. Chờ duyệt
            </div>
            <div
              className={cn(
                "p-1.5 rounded-lg font-bold border",
                activeOrder.status === "DANG_PHUC_VU" && activeOrder.items.some((i) => i.status === "DANG_LAM")
                  ? "bg-blue-100 text-blue-800 border-blue-300"
                  : "bg-slate-100 text-slate-500 border-slate-200",
              )}
            >
              2. Bếp nấu
            </div>
            <div
              className={cn(
                "p-1.5 rounded-lg font-bold border",
                activeOrder.items.some((i) => i.status === "SAN_SANG")
                  ? "bg-emerald-100 text-emerald-800 border-emerald-300 animate-pulse"
                  : "bg-slate-100 text-slate-500 border-slate-200",
              )}
            >
              3. Sẵn sàng
            </div>
            <div
              className={cn(
                "p-1.5 rounded-lg font-bold border",
                activeOrder.status === "CHO_THANH_TOAN" || activeOrder.items.every((i) => i.status === "DA_GIAO")
                  ? "bg-emerald-500 text-white border-emerald-600"
                  : "bg-slate-100 text-slate-500 border-slate-200",
              )}
            >
              4. Đã bưng
            </div>
          </div>

          {/* Detailed item tracking dropdown */}
          {orderTrackingOpen && (
            <div className="pt-2 border-t border-slate-100 space-y-1.5 text-xs">
              {activeOrder.items.map((it) => (
                <div key={it.id} className="flex justify-between items-center py-1">
                  <div>
                    <span className="font-semibold text-slate-800">{it.name}</span>
                    <span className="text-slate-400 ml-1">x{it.quantity}</span>
                  </div>
                  <span
                    className={cn(
                      "text-[10px] font-bold px-2 py-0.5 rounded-full",
                      it.status === "CHO_LAM" && "bg-amber-100 text-amber-700",
                      it.status === "DANG_LAM" && "bg-blue-100 text-blue-700",
                      it.status === "SAN_SANG" && "bg-emerald-100 text-emerald-700",
                      it.status === "DA_GIAO" && "bg-gray-100 text-gray-700",
                    )}
                  >
                    {it.status === "CHO_LAM" && "Chờ xác nhận"}
                    {it.status === "DANG_LAM" && "🍳 Đang nấu"}
                    {it.status === "SAN_SANG" && "🔔 Sẵn sàng bưng"}
                    {it.status === "DA_GIAO" && "✓ Đã phục vụ"}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Category Pills */}
      <div className="sticky top-[61px] z-20 bg-white px-4 py-2.5 border-b border-slate-100 flex items-center gap-2 overflow-x-auto">
        {categories.map((cat) => (
          <button
            key={cat.key}
            onClick={() => setActiveCategory(cat.key)}
            className={cn(
              "px-3 py-1.5 rounded-full text-xs font-semibold whitespace-nowrap transition-colors",
              activeCategory === cat.key
                ? "bg-red-500 text-white shadow-sm"
                : "bg-slate-100 text-slate-600 hover:bg-slate-200",
            )}
          >
            {cat.label}
          </button>
        ))}
      </div>

      {/* Menu List */}
      <div className="p-4 space-y-3">
        {filteredDishes.map((dish) => {
          const cartItem = cart[dish.id];
          const isOutOfStock = dish.outOfStock;

          return (
            <div
              key={dish.id}
              className={cn(
                "p-3 rounded-2xl border transition-all flex items-center justify-between gap-3 shadow-xs",
                isOutOfStock
                  ? "bg-slate-100/80 border-slate-200 opacity-60"
                  : "bg-white border-slate-200 hover:border-red-200",
              )}
            >
              {/* Dish info */}
              <div className="flex-1 space-y-1">
                <div className="flex items-center gap-1.5">
                  <h3 className="font-bold text-sm text-slate-900 leading-tight">
                    {dish.name}
                  </h3>
                  {isOutOfStock && (
                    <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-red-500 text-white">
                      Đã hết
                    </span>
                  )}
                </div>
                <p className="text-xs font-semibold text-red-600">
                  {dish.price.toLocaleString("vi-VN")} đ
                </p>
                <span className="text-[10px] text-slate-400 uppercase">
                  {dish.station === "BEP" ? "🍳 Trạm Bếp" : "🍹 Trạm Bar"}
                </span>

                {/* Dish Note input if in cart */}
                {cartItem && (
                  <div className="pt-1.5">
                    <input
                      type="text"
                      placeholder="Ghi chú (ít cay, không hành...)"
                      value={cartItem.note}
                      onChange={(e) =>
                        setCart((prev) => ({
                          ...prev,
                          [dish.id]: { ...cartItem, note: e.target.value },
                        }))
                      }
                      className="w-full text-xs px-2.5 py-1 bg-slate-50 border rounded-lg focus:outline-none focus:ring-1 focus:ring-red-500"
                    />
                  </div>
                )}
              </div>

              {/* Add / Quantity Controls */}
              <div className="shrink-0">
                {isOutOfStock ? (
                  <button
                    disabled
                    className="px-3 py-1.5 rounded-xl bg-slate-200 text-slate-400 text-xs font-bold cursor-not-allowed"
                  >
                    Hết món
                  </button>
                ) : cartItem ? (
                  <div className="flex items-center gap-2 bg-red-50 p-1 rounded-xl border border-red-200">
                    <button
                      onClick={() => handleRemoveFromCart(dish.id)}
                      className="size-7 rounded-lg bg-white text-red-600 font-bold flex items-center justify-center shadow-xs hover:bg-red-100 transition-colors"
                    >
                      <Minus size={14} />
                    </button>
                    <span className="w-5 text-center font-bold text-sm text-red-700">
                      {cartItem.quantity}
                    </span>
                    <button
                      onClick={() => handleAddToCart(dish)}
                      className="size-7 rounded-lg bg-red-500 text-white font-bold flex items-center justify-center shadow-xs hover:bg-red-600 transition-colors"
                    >
                      <Plus size={14} />
                    </button>
                  </div>
                ) : (
                  <button
                    onClick={() => handleAddToCart(dish)}
                    className="flex items-center gap-1 bg-red-500 hover:bg-red-600 text-white text-xs font-bold px-3 py-2 rounded-xl shadow-sm transition-colors"
                  >
                    <Plus size={14} /> Chọn
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* Floating Bottom Cart Bar */}
      {cartItemsCount > 0 && (
        <div className="fixed bottom-0 left-0 right-0 max-w-md mx-auto p-4 z-40 bg-gradient-to-t from-white via-white to-transparent">
          <div className="bg-slate-900 text-white rounded-2xl p-3.5 shadow-2xl flex items-center justify-between border border-slate-700 animate-in slide-in-from-bottom-3">
            <div>
              <div className="flex items-center gap-2">
                <span className="size-6 rounded-full bg-red-500 text-white text-xs font-bold flex items-center justify-center">
                  {cartItemsCount}
                </span>
                <span className="font-bold text-sm">{cartTotalPrice.toLocaleString("vi-VN")} đ</span>
              </div>
              <p className="text-[11px] text-slate-400">Đợt gọi tại {currentTable.name}</p>
            </div>

            <button
              onClick={handleSubmitGuestOrder}
              className="bg-red-500 hover:bg-red-600 text-white text-xs font-bold px-4 py-2.5 rounded-xl shadow-md transition-all flex items-center gap-1.5 active:scale-95"
            >
              <span>Gửi đợt gọi món</span>
              <ArrowRight size={14} />
            </button>
          </div>
        </div>
      )}

      {/* Floating Staff Switcher Button */}
      <div className="fixed bottom-3 right-3 z-30">
        <Link
          href="/tables"
          className="bg-white/90 backdrop-blur text-slate-800 text-[11px] font-bold px-3 py-1.5 rounded-full border border-slate-300 shadow-lg flex items-center gap-1.5 hover:bg-white"
        >
          <span>🖥️ Sang trang Quản lý</span>
        </Link>
      </div>
    </div>
  );
}

export default function GuestPage() {
  return (
    <Suspense fallback={<div className="p-8 text-center text-sm">Đang tải thực đơn...</div>}>
      <GuestMenuContent />
    </Suspense>
  );
}
