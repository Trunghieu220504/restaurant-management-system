"use client";

import { useState } from "react";
import Link from "next/link";
import {
  CalendarDays,
  CircleDollarSign,
  Lock,
  Unlock,
  AlertTriangle,
  CheckCircle2,
  Receipt,
  RotateCcw,
  Store,
  ArrowRight,
} from "lucide-react";
import { useRestaurant } from "@/lib/store/restaurant-context";
import { cn } from "@/lib/utils";

export default function SalesDayPage() {
  const { salesDay, tables, orders, openSalesDay, closeSalesDay } = useRestaurant();

  // Open Sales Day form
  const [openingCashInput, setOpeningCashInput] = useState<number>(1000000);

  // Close Sales Day form
  const [closingCashInput, setClosingCashInput] = useState<number>(1188320);
  const [closeNote, setCloseNote] = useState<string>("");

  // Check conditions per UC26
  const openOrders = orders.filter((o) => o.status !== "DA_DONG" && o.status !== "DA_HUY");
  const activeTables = tables.filter((t) => t.status === "DANG_DUNG");

  // Revenue calculation for closed orders
  const closedOrders = orders.filter((o) => o.status === "DA_DONG");
  const totalCashCollected = closedOrders.reduce((sum, o) => {
    const cash = o.payments
      .filter((p) => p.method === "TIEN_MAT")
      .reduce((s, p) => s + p.amount, 0);
    return sum + cash;
  }, 0);

  const totalBankCollected = closedOrders.reduce((sum, o) => {
    const bank = o.payments
      .filter((p) => p.method === "CHUYEN_KHOAN")
      .reduce((s, p) => s + p.amount, 0);
    return sum + bank;
  }, 0);

  const totalWaived = closedOrders.reduce((sum, o) => sum + o.waivedAmount, 0);
  const totalNetRevenue = totalCashCollected + totalBankCollected;

  const expectedClosingCash = (salesDay.openingCash || 0) + totalCashCollected;
  const cashDifference = closingCashInput - expectedClosingCash;

  const handleOpenSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    openSalesDay(openingCashInput);
  };

  const handleCloseSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    closeSalesDay(closingCashInput, closeNote);
  };

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col space-y-4 pb-12">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-white p-4 rounded-xl shadow-sm border border-gray-100">
        <div>
          <h1 className="text-xl font-bold text-gray-900 flex items-center gap-2">
            <CalendarDays className="size-5 text-red-500" />
            Quản lý Ngày bán & Chốt ca (UC25, UC26)
          </h1>
          <p className="text-xs text-gray-500 mt-0.5">
            Mở ngày bán đầu ca, đối soát két tiền mặt, kiểm kê và chốt ngày kinh doanh
          </p>
        </div>

        {/* Current status pill */}
        <div className="flex items-center gap-2">
          <span
            className={cn(
              "px-3 py-1.5 rounded-full text-xs font-bold flex items-center gap-1.5 shadow-sm",
              salesDay.status === "DANG_BAN"
                ? "bg-emerald-100 text-emerald-800 border border-emerald-300"
                : salesDay.status === "DA_CHOT"
                ? "bg-gray-100 text-gray-700 border border-gray-300"
                : "bg-amber-100 text-amber-800 border border-amber-300",
            )}
          >
            {salesDay.status === "DANG_BAN" && (
              <>
                <Unlock size={14} className="text-emerald-600" />
                ĐANG BÁN (Ngày {salesDay.businessDate})
              </>
            )}
            {salesDay.status === "DA_CHOT" && (
              <>
                <Lock size={14} className="text-gray-600" />
                ĐÃ CHỐT CA ({salesDay.closedAt})
              </>
            )}
            {salesDay.status === "CHUA_MO" && (
              <>
                <Lock size={14} className="text-amber-600" />
                CHƯA MỞ NGÀY BÁN
              </>
            )}
          </span>
        </div>
      </div>

      {/* Overview Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white rounded-xl p-4 shadow-sm border border-gray-100 space-y-1">
          <span className="text-xs text-gray-400 font-medium">Tiền két đầu ngày</span>
          <p className="text-xl font-bold text-gray-900">
            {salesDay.openingCash.toLocaleString("vi-VN")} đ
          </p>
          <span className="text-[11px] text-gray-500">Mở lúc: {salesDay.openedAt || "07:30"}</span>
        </div>

        <div className="bg-white rounded-xl p-4 shadow-sm border border-gray-100 space-y-1">
          <span className="text-xs text-gray-400 font-medium">Tiền mặt đã thu</span>
          <p className="text-xl font-bold text-emerald-600">
            {totalCashCollected.toLocaleString("vi-VN")} đ
          </p>
          <span className="text-[11px] text-gray-500">Vào két trong ngày</span>
        </div>

        <div className="bg-white rounded-xl p-4 shadow-sm border border-gray-100 space-y-1">
          <span className="text-xs text-gray-400 font-medium">Chuyển khoản (Bank)</span>
          <p className="text-xl font-bold text-blue-600">
            {totalBankCollected.toLocaleString("vi-VN")} đ
          </p>
          <span className="text-[11px] text-gray-500">Tài khoản ngân hàng</span>
        </div>

        <div className="bg-white rounded-xl p-4 shadow-sm border border-gray-100 space-y-1">
          <span className="text-xs text-gray-400 font-medium">Doanh thu thuần</span>
          <p className="text-xl font-bold text-red-600">
            {totalNetRevenue.toLocaleString("vi-VN")} đ
          </p>
          <span className="text-[11px] text-gray-500">Miễn thu: {totalWaived.toLocaleString("vi-VN")} đ</span>
        </div>
      </div>

      {/* Main Operations section */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left 2 Cols: Status check & Close Sales Day */}
        <div className="lg:col-span-2 space-y-4">
          {/* Blocking Condition Validation Card (UC26) */}
          <div className="bg-white rounded-xl p-5 shadow-sm border border-gray-100 space-y-4">
            <h2 className="text-base font-bold text-gray-900">
              Kiểm tra điều kiện chốt ca (Quy chuẩn UC26)
            </h2>
            <p className="text-xs text-gray-500">
              Theo tài liệu đề tài: Để chốt ngày bán, hệ thống kiểm tra điều kiện chặn: Không còn đơn mở hoặc bàn đang sử dụng.
            </p>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2">
              <div
                className={cn(
                  "p-3 rounded-xl border flex items-center justify-between",
                  openOrders.length === 0
                    ? "bg-emerald-50 border-emerald-200 text-emerald-800"
                    : "bg-red-50 border-red-200 text-red-800",
                )}
              >
                <div>
                  <p className="text-xs font-semibold">Đơn hàng chưa đóng</p>
                  <p className="text-lg font-bold">{openOrders.length} đơn</p>
                </div>
                {openOrders.length === 0 ? (
                  <CheckCircle2 size={24} className="text-emerald-600" />
                ) : (
                  <Link
                    href="/orders"
                    className="text-xs bg-red-600 text-white px-2.5 py-1.5 rounded-lg font-semibold hover:bg-red-700 transition-colors flex items-center gap-1"
                  >
                    Xem đơn <ArrowRight size={12} />
                  </Link>
                )}
              </div>

              <div
                className={cn(
                  "p-3 rounded-xl border flex items-center justify-between",
                  activeTables.length === 0
                    ? "bg-emerald-50 border-emerald-200 text-emerald-800"
                    : "bg-red-50 border-red-200 text-red-800",
                )}
              >
                <div>
                  <p className="text-xs font-semibold">Bàn đang có khách</p>
                  <p className="text-lg font-bold">{activeTables.length} bàn</p>
                </div>
                {activeTables.length === 0 ? (
                  <CheckCircle2 size={24} className="text-emerald-600" />
                ) : (
                  <Link
                    href="/tables"
                    className="text-xs bg-red-600 text-white px-2.5 py-1.5 rounded-lg font-semibold hover:bg-red-700 transition-colors flex items-center gap-1"
                  >
                    Xem bàn <ArrowRight size={12} />
                  </Link>
                )}
              </div>
            </div>

            {openOrders.length > 0 && (
              <div className="bg-amber-50 border border-amber-200 p-3 rounded-xl text-xs text-amber-800 flex items-start gap-2">
                <AlertTriangle size={16} className="text-amber-600 mt-0.5 shrink-0" />
                <div>
                  <strong>Lưu ý:</strong> Vẫn còn {openOrders.length} đơn đang hoạt động (
                  {openOrders.map((o) => o.id).join(", ")}). Bạn cần thu tiền và đóng đơn hoàn tất tại trang Đơn hàng trước khi chốt ngày bán.
                </div>
              </div>
            )}
          </div>

          {/* Form Chốt Ngày Bán (UC26) */}
          {salesDay.status === "DANG_BAN" && (
            <div className="bg-white rounded-xl p-5 shadow-sm border border-gray-100 space-y-4">
              <h2 className="text-base font-bold text-gray-900 flex items-center gap-2">
                <Lock size={18} className="text-red-500" />
                Đối soát & Chốt ngày bán (UC26)
              </h2>

              <form onSubmit={handleCloseSubmit} className="space-y-4">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">
                      Két lý thuyết (Đầu ngày + Tiền mặt):
                    </label>
                    <div className="px-3 py-2 bg-gray-100 rounded-lg text-sm font-bold text-gray-800">
                      {expectedClosingCash.toLocaleString("vi-VN")} đ
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">
                      Tiền két thực tế đếm được (đ):
                    </label>
                    <input
                      type="number"
                      step={1000}
                      value={closingCashInput}
                      onChange={(e) => setClosingCashInput(Number(e.target.value))}
                      className="w-full px-3 py-2 border rounded-lg text-sm font-semibold focus:ring-2 focus:ring-red-500 focus:outline-none"
                    />
                  </div>
                </div>

                <div
                  className={cn(
                    "p-3 rounded-xl text-xs font-medium flex items-center justify-between",
                    cashDifference === 0
                      ? "bg-emerald-50 text-emerald-800 border border-emerald-200"
                      : "bg-amber-50 text-amber-800 border border-amber-200",
                  )}
                >
                  <span>Chênh lệch đối soát két:</span>
                  <strong>
                    {cashDifference > 0 ? "+" : ""}
                    {cashDifference.toLocaleString("vi-VN")} đ{" "}
                    {cashDifference === 0 ? "(Khớp hoàn hảo)" : "(Có sai số)"}
                  </strong>
                </div>

                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Ghi chú chốt ca / Lý do chênh lệch (nếu có):
                  </label>
                  <input
                    type="text"
                    value={closeNote}
                    onChange={(e) => setCloseNote(e.target.value)}
                    placeholder="VD: Tiền lẻ tip, hao hụt kiểm kê..."
                    className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-red-500 focus:outline-none"
                  />
                </div>

                <button
                  type="submit"
                  disabled={openOrders.length > 0 || activeTables.length > 0}
                  className="w-full py-2.5 bg-red-600 hover:bg-red-700 disabled:opacity-40 disabled:cursor-not-allowed text-white font-bold text-sm rounded-xl shadow transition-colors flex items-center justify-center gap-2"
                >
                  <Lock size={16} />
                  Xác nhận chốt ngày bán (UC26)
                </button>
              </form>
            </div>
          )}
        </div>

        {/* Right 1 Col: Mở ngày bán mới (UC25) */}
        <div className="space-y-4">
          <div className="bg-white rounded-xl p-5 shadow-sm border border-gray-100 space-y-4">
            <h2 className="text-base font-bold text-gray-900 flex items-center gap-2">
              <Unlock size={18} className="text-emerald-600" />
              Mở ngày bán mới (UC25)
            </h2>
            <p className="text-xs text-gray-500">
              Quy tắc UC25: Khi mở ngày bán, hệ thống tự động reset tất cả cờ hết món trên thực đơn về trạng thái còn hàng.
            </p>

            <form onSubmit={handleOpenSubmit} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">
                  Tiền két đầu ngày (đ):
                </label>
                <input
                  type="number"
                  step={10000}
                  value={openingCashInput}
                  onChange={(e) => setOpeningCashInput(Number(e.target.value))}
                  className="w-full px-3 py-2 border rounded-lg text-sm font-semibold focus:ring-2 focus:ring-emerald-500 focus:outline-none"
                />
              </div>

              <button
                type="submit"
                className="w-full py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-sm rounded-xl shadow transition-colors flex items-center justify-center gap-2"
              >
                <Unlock size={16} />
                Mở ngày bán (UC25)
              </button>
            </form>
          </div>

          <div className="bg-gradient-to-br from-red-500 to-rose-600 rounded-xl p-5 text-white shadow-md space-y-3">
            <h3 className="font-bold text-sm">Liên kết luồng vận hành</h3>
            <p className="text-xs text-white/90 leading-relaxed">
              Mọi hóa đơn đóng thành công tại phân hệ Đơn hàng sẽ ngay lập tức đối soát vào Doanh thu thuần của Ngày bán này và hiển thị trên Báo cáo KD.
            </p>
            <div className="pt-2 border-t border-white/20 flex items-center justify-between">
              <Link
                href="/reports"
                className="text-xs font-semibold bg-white/20 hover:bg-white/30 px-3 py-1.5 rounded-lg transition-colors flex items-center gap-1"
              >
                Xem báo cáo KD <ArrowRight size={13} />
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
