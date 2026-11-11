"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  LayoutGrid,
  Users,
  Clock,
  ArrowRightLeft,
  Receipt,
  Plus,
  CheckCircle2,
  AlertCircle,
  UtensilsCrossed,
  Sparkles,
  QrCode,
  Printer,
} from "lucide-react";
import { useRestaurant, type RestaurantTable } from "@/lib/store/restaurant-context";
import { cn } from "@/lib/utils";

export default function TablesPage() {
  const router = useRouter();
  const { tables, orders, openTableSession, transferTable, salesDay } = useRestaurant();

  const [selectedArea, setSelectedArea] = useState<string>("ALL");
  const [statusFilter, setStatusFilter] = useState<"ALL" | "TRONG" | "DANG_DUNG">("ALL");

  // Dialog states
  const [openSessionModal, setOpenSessionModal] = useState<RestaurantTable | null>(null);
  const [guestCount, setGuestCount] = useState<number>(2);

  const [transferModal, setTransferModal] = useState<RestaurantTable | null>(null);
  const [targetTableId, setTargetTableId] = useState<string>("");

  const [qrModalTable, setQrModalTable] = useState<RestaurantTable | null>(null);

  const areas = ["ALL", "Tầng 1", "Tầng 2", "Ngoài trời"];

  const filteredTables = tables.filter((table) => {
    const matchArea = selectedArea === "ALL" || table.area === selectedArea;
    const matchStatus = statusFilter === "ALL" || table.status === statusFilter;
    return matchArea && matchStatus;
  });

  const occupiedCount = tables.filter((t) => t.status === "DANG_DUNG").length;
  const freeCount = tables.filter((t) => t.status === "TRONG").length;

  const handleOpenSessionSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!openSessionModal) return;
    openTableSession(openSessionModal.id, guestCount);
    setOpenSessionModal(null);
  };

  const handleTransferSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!transferModal || !targetTableId) return;
    const success = transferTable(transferModal.id, targetTableId);
    if (success) {
      setTransferModal(null);
      setTargetTableId("");
    }
  };

  const getTableOrder = (table: RestaurantTable) => {
    if (!table.currentOrderId) return null;
    return orders.find((o) => o.id === table.currentOrderId);
  };

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col space-y-4 pb-12">
      {/* Alert banner if sales day is not open */}
      {salesDay.status !== "DANG_BAN" && (
        <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <AlertCircle className="size-5 text-amber-600" />
            <div>
              <p className="text-sm font-semibold text-amber-900">
                Ngày bán đang ở trạng thái: {salesDay.status === "CHUA_MO" ? "Chưa mở" : "Đã chốt ca"}
              </p>
              <p className="text-xs text-amber-700">
                Hãy mở ngày bán trước khi mở phiên bàn hoặc tạo đơn mới (theo quy trình UC25).
              </p>
            </div>
          </div>
          <Link
            href="/sales-day"
            className="text-xs font-semibold bg-amber-600 hover:bg-amber-700 text-white px-3 py-2 rounded-lg transition-colors"
          >
            Mở ngày bán ngay
          </Link>
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-white p-4 rounded-xl shadow-sm border border-gray-100">
        <div>
          <h1 className="text-xl font-bold text-gray-900 flex items-center gap-2">
            <LayoutGrid className="size-5 text-red-500" />
            Sơ đồ Bàn & Khu vực
          </h1>
          <p className="text-xs text-gray-500 mt-0.5">
            Quản lý phiên bàn (UC03), mở phiên, đổi bàn và điều hướng gọi món tại bàn
          </p>
        </div>

        {/* Counter Stats */}
        <div className="flex items-center gap-2 text-xs">
          <span className="bg-gray-100 text-gray-700 px-3 py-1.5 rounded-lg font-medium">
            Tổng: <strong className="text-gray-900">{tables.length}</strong> bàn
          </span>
          <span className="bg-red-50 text-red-700 px-3 py-1.5 rounded-lg font-medium border border-red-200">
            Đang dùng: <strong className="text-red-800">{occupiedCount}</strong>
          </span>
          <span className="bg-emerald-50 text-emerald-700 px-3 py-1.5 rounded-lg font-medium border border-emerald-200">
            Trống: <strong className="text-emerald-800">{freeCount}</strong>
          </span>
        </div>
      </div>

      {/* Filter Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-3 bg-white p-3 rounded-xl shadow-sm border border-gray-100">
        {/* Area tabs */}
        <div className="flex items-center gap-1.5 overflow-x-auto">
          {areas.map((area) => (
            <button
              key={area}
              onClick={() => setSelectedArea(area)}
              className={cn(
                "px-3 py-1.5 text-xs font-semibold rounded-lg transition-colors whitespace-nowrap",
                selectedArea === area
                  ? "bg-red-500 text-white shadow-sm"
                  : "bg-gray-100 text-gray-600 hover:bg-gray-200",
              )}
            >
              {area === "ALL" ? "Tất cả khu vực" : area}
            </button>
          ))}
        </div>

        {/* Status filters */}
        <div className="flex items-center gap-1.5 text-xs">
          <button
            onClick={() => setStatusFilter("ALL")}
            className={cn(
              "px-2.5 py-1 rounded-md font-medium",
              statusFilter === "ALL" ? "bg-gray-800 text-white" : "text-gray-600 hover:bg-gray-100",
            )}
          >
            Tất cả
          </button>
          <button
            onClick={() => setStatusFilter("TRONG")}
            className={cn(
              "px-2.5 py-1 rounded-md font-medium",
              statusFilter === "TRONG"
                ? "bg-emerald-600 text-white"
                : "text-emerald-700 bg-emerald-50 hover:bg-emerald-100",
            )}
          >
            Bàn trống ({freeCount})
          </button>
          <button
            onClick={() => setStatusFilter("DANG_DUNG")}
            className={cn(
              "px-2.5 py-1 rounded-md font-medium",
              statusFilter === "DANG_DUNG"
                ? "bg-red-600 text-white"
                : "text-red-700 bg-red-50 hover:bg-red-100",
            )}
          >
            Đang phục vụ ({occupiedCount})
          </button>
        </div>
      </div>

      {/* Tables Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
        {filteredTables.map((table) => {
          const isOccupied = table.status === "DANG_DUNG";
          const order = getTableOrder(table);
          const totalAmount = order
            ? order.items.reduce((s, it) => s + it.price * it.quantity, 0)
            : 0;

          return (
            <div
              key={table.id}
              className={cn(
                "relative rounded-2xl border p-4 shadow-sm transition-all flex flex-col justify-between",
                isOccupied
                  ? "bg-white border-red-200 ring-1 ring-red-100"
                  : "bg-white border-emerald-100 hover:border-emerald-300",
              )}
            >
              <div>
                {/* Card Top */}
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <span
                      className={cn(
                        "size-3 rounded-full animate-pulse",
                        isOccupied ? "bg-red-500" : "bg-emerald-500",
                      )}
                    />
                    <h3 className="font-bold text-gray-900 text-base">{table.name}</h3>
                  </div>
                  <div className="flex items-center gap-1.5">
                    <button
                      type="button"
                      onClick={() => setQrModalTable(table)}
                      title="Xem / In mã QR cố định của bàn"
                      className="p-1.5 rounded-lg text-gray-500 hover:text-red-600 hover:bg-red-50 border border-gray-200 transition-colors"
                    >
                      <QrCode size={14} />
                    </button>
                    <span
                      className={cn(
                        "text-[11px] font-semibold px-2 py-0.5 rounded-full",
                        isOccupied
                          ? "bg-red-50 text-red-600 border border-red-200"
                          : "bg-emerald-50 text-emerald-700 border border-emerald-200",
                      )}
                    >
                      {isOccupied ? "Đang sử dụng" : "Bàn trống"}
                    </span>
                  </div>
                </div>

                {/* Area Tag */}
                <p className="text-xs text-gray-400 font-medium mb-3">Khu vực: {table.area}</p>

                {/* Occupied details */}
                {isOccupied && (
                  <div className="space-y-2 bg-red-50/50 rounded-xl p-3 border border-red-100/60 mb-3 text-xs">
                    <div className="flex items-center justify-between text-gray-600">
                      <span className="flex items-center gap-1.5">
                        <Users size={13} className="text-gray-400" />
                        Số khách:
                      </span>
                      <strong className="text-gray-900">{table.guestCount || 2} khách</strong>
                    </div>

                    <div className="flex items-center justify-between text-gray-600">
                      <span className="flex items-center gap-1.5">
                        <Clock size={13} className="text-gray-400" />
                        Giờ vào:
                      </span>
                      <span className="font-medium text-gray-800">{table.openedAt || "10:15"}</span>
                    </div>

                    {order && (
                      <>
                        <div className="flex items-center justify-between text-gray-600 border-t border-red-100/80 pt-1.5">
                          <span>Mã đơn:</span>
                          <span className="font-semibold text-red-600">{order.id}</span>
                        </div>
                        <div className="flex items-center justify-between text-gray-600">
                          <span>Số món:</span>
                          <span>{order.items.length} món</span>
                        </div>
                        <div className="flex items-center justify-between text-gray-900 font-bold">
                          <span>Tạm tính:</span>
                          <span className="text-red-600">{totalAmount.toLocaleString("vi-VN")} đ</span>
                        </div>
                      </>
                    )}
                  </div>
                )}

                {/* Vacant state helper */}
                {!isOccupied && (
                  <div className="text-center py-4 bg-gray-50/70 rounded-xl mb-3 border border-dashed border-gray-200">
                    <UtensilsCrossed className="size-6 text-gray-300 mx-auto mb-1" />
                    <p className="text-xs text-gray-400">Bàn sẵn sàng đón khách</p>
                  </div>
                )}
              </div>

              {/* Action Buttons */}
              <div className="pt-2 border-t border-gray-100 flex items-center gap-2">
                {isOccupied ? (
                  <>
                    <button
                      onClick={() => setTransferModal(table)}
                      title="Chuyển bàn (UC03)"
                      className="flex-1 flex items-center justify-center gap-1 text-xs font-semibold py-2 px-2 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-lg transition-colors"
                    >
                      <ArrowRightLeft size={13} />
                      Đổi bàn
                    </button>
                    <Link
                      href={order ? `/orders?orderId=${order.id}` : "/orders"}
                      className="flex-1 flex items-center justify-center gap-1 text-xs font-semibold py-2 px-2 bg-red-500 hover:bg-red-600 text-white rounded-lg transition-colors shadow-sm"
                    >
                      <Receipt size={13} />
                      Vào đơn
                    </Link>
                  </>
                ) : (
                  <>
                    <button
                      onClick={() => {
                        setOpenSessionModal(table);
                        setGuestCount(2);
                      }}
                      className="w-full flex items-center justify-center gap-1.5 text-xs font-semibold py-2 px-3 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg transition-colors shadow-sm"
                    >
                      <Sparkles size={14} />
                      Mở phiên bàn (UC03)
                    </button>
                  </>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* ── MODAL: MỞ PHIÊN BÀN (UC03) ─────────────────────────────────── */}
      {openSessionModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl max-w-sm w-full p-5 shadow-2xl border animate-in fade-in zoom-in-95">
            <h3 className="text-base font-bold text-gray-900 mb-1">
              Mở phiên cho {openSessionModal.name}
            </h3>
            <p className="text-xs text-gray-500 mb-4">
              Khu vực: {openSessionModal.area}. Nhập số lượng khách vào bàn.
            </p>

            <form onSubmit={handleOpenSessionSubmit} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Số khách (người):
                </label>
                <div className="flex items-center gap-2">
                  {[1, 2, 4, 6, 8].map((num) => (
                    <button
                      key={num}
                      type="button"
                      onClick={() => setGuestCount(num)}
                      className={cn(
                        "size-9 rounded-lg text-xs font-bold transition-colors",
                        guestCount === num
                          ? "bg-emerald-600 text-white"
                          : "bg-gray-100 text-gray-700 hover:bg-gray-200",
                      )}
                    >
                      {num}
                    </button>
                  ))}
                </div>
                <input
                  type="number"
                  min={1}
                  max={50}
                  value={guestCount}
                  onChange={(e) => setGuestCount(Number(e.target.value))}
                  className="mt-2 w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-emerald-500 focus:outline-none"
                />
              </div>

              <div className="flex items-center gap-2 pt-2 border-t">
                <button
                  type="button"
                  onClick={() => setOpenSessionModal(null)}
                  className="flex-1 py-2 text-xs font-semibold text-gray-600 hover:bg-gray-100 rounded-lg"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  className="flex-1 py-2 text-xs font-semibold bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg shadow-sm"
                >
                  Xác nhận mở bàn
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── MODAL: CHUYỂN BÀN (UC03) ──────────────────────────────────── */}
      {transferModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl max-w-sm w-full p-5 shadow-2xl border animate-in fade-in zoom-in-95">
            <h3 className="text-base font-bold text-gray-900 mb-1">
              Chuyển phiên từ {transferModal.name}
            </h3>
            <p className="text-xs text-gray-500 mb-4">
              Theo quy tắc UC03: Đơn hàng và toàn bộ đợt gọi món sẽ được giữ nguyên và chuyển sang bàn trống mới.
            </p>

            <form onSubmit={handleTransferSubmit} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Chọn bàn đích (đang trống):
                </label>
                <select
                  value={targetTableId}
                  onChange={(e) => setTargetTableId(e.target.value)}
                  required
                  className="w-full px-3 py-2 border rounded-lg text-sm bg-white focus:ring-2 focus:ring-red-500 focus:outline-none"
                >
                  <option value="">-- Chọn bàn trống --</option>
                  {tables
                    .filter((t) => t.status === "TRONG")
                    .map((t) => (
                      <option key={t.id} value={t.id}>
                        {t.name} ({t.area})
                      </option>
                    ))}
                </select>
              </div>

              <div className="flex items-center gap-2 pt-2 border-t">
                <button
                  type="button"
                  onClick={() => setTransferModal(null)}
                  className="flex-1 py-2 text-xs font-semibold text-gray-600 hover:bg-gray-100 rounded-lg"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={!targetTableId}
                  className="flex-1 py-2 text-xs font-semibold bg-red-600 hover:bg-red-700 disabled:opacity-50 text-white rounded-lg shadow-sm"
                >
                  Chuyển bàn ngay
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
      {/* ── MODAL: MÃ QR BÀN (THEO ĐẶC TẢ TAB 1) ────────────────────── */}
      {qrModalTable && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl max-w-sm w-full p-6 shadow-2xl border space-y-4 animate-in fade-in zoom-in-95 text-center">
            <div>
              <h3 className="text-base font-bold text-gray-900">
                Mã QR cố định: {qrModalTable.name}
              </h3>
              <p className="text-xs text-gray-500 mt-0.5">Khu vực: {qrModalTable.area}</p>
            </div>

            {/* QR Mockup visual */}
            <div className="bg-white p-4 rounded-2xl border-2 border-red-500/20 inline-block shadow-inner mx-auto">
              <div className="size-48 bg-slate-900 p-3 rounded-xl flex flex-col items-center justify-between text-white relative">
                {/* Simulated QR Pattern */}
                <div className="grid grid-cols-5 gap-1.5 w-full h-full p-1">
                  {Array.from({ length: 25 }).map((_, i) => (
                    <div
                      key={i}
                      className={cn(
                        "rounded-sm",
                        i === 0 || i === 4 || i === 20 || i === 12 || i === 6 || i === 18
                          ? "bg-red-500"
                          : i % 2 === 0
                          ? "bg-white"
                          : "bg-slate-800",
                      )}
                    />
                  ))}
                </div>
                <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                  <span className="size-10 rounded-lg bg-red-600 text-white font-black text-base flex items-center justify-center shadow-lg border-2 border-white">
                    R
                  </span>
                </div>
              </div>
            </div>

            <p className="text-xs text-gray-600">
              Quét mã để truy cập thực đơn và tự gọi món tại {qrModalTable.name}
            </p>

            <div className="space-y-2 pt-2">
              <Link
                href={`/guest?table=${qrModalTable.id}`}
                className="w-full py-2.5 bg-red-500 hover:bg-red-600 text-white text-xs font-bold rounded-xl shadow flex items-center justify-center gap-2 transition-colors"
              >
                <QrCode size={16} />
                <span>Khách quét thử ngay (Mở menu QR)</span>
              </Link>
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => {
                    toast.success(`Đã gửi lệnh in mã QR cho ${qrModalTable.name}!`);
                  }}
                  className="flex-1 py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 text-xs font-semibold rounded-xl flex items-center justify-center gap-1.5"
                >
                  <Printer size={14} />
                  <span>In mã QR</span>
                </button>
                <button
                  type="button"
                  onClick={() => setQrModalTable(null)}
                  className="flex-1 py-2 border text-gray-600 hover:bg-gray-50 text-xs font-semibold rounded-xl"
                >
                  Đóng
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
