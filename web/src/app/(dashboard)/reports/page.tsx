"use client";

import { ArrowLeft, Menu, TrendingUp } from "lucide-react";
import { useRouter } from "next/navigation";
import { useRestaurant } from "@/lib/store/restaurant-context";

const MAX_VALUE = 300000;
const Y_LABELS = [250000, 200000, 150000, 100000, 50000, 0];

function formatNumber(n: number) {
  return n.toLocaleString("vi-VN");
}

export default function ReportsPage() {
  const router = useRouter();
  const { orders, salesDay } = useRestaurant();

  // Dynamic calculations from live store
  const closedOrders = orders.filter((o) => o.status === "DA_DONG");
  const openOrders = orders.filter((o) => o.status !== "DA_DONG" && o.status !== "DA_HUY");

  const todayRevenue = closedOrders.reduce((sum, o) => {
    const paid = o.payments.reduce((s, p) => s + p.amount, 0);
    return sum + paid;
  }, 0);

  const pendingRevenue = openOrders.reduce((sum, o) => {
    const cost = o.items.reduce((s, it) => s + it.price * it.quantity, 0);
    return sum + cost;
  }, 0);

  // Total collected = 188.320đ baseline from mockup + any newly closed orders today
  const totalRevenue = 188320 + todayRevenue;

  const chartData = [
    { date: "15/02", value: 0 },
    { date: "16/02", value: 0 },
    { date: "17/02", value: 0 },
    { date: "18/02", value: 0 },
    { date: "19/02", value: 188320 },
    { date: "Hôm nay", value: todayRevenue > 0 ? todayRevenue : 0 },
    { date: "21/02", value: 0 },
  ];

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* Header */}
      <div className="bg-white flex items-center justify-between px-4 py-3 shadow-sm sticky top-0 z-10">
        <button onClick={() => router.back()} className="p-1 text-gray-600 hover:bg-gray-100 rounded-lg">
          <ArrowLeft size={22} />
        </button>
        <h1 className="text-lg font-semibold text-gray-800">Báo cáo KD (UC28)</h1>
        <button className="p-1 text-gray-600 hover:bg-gray-100 rounded-lg">
          <Menu size={22} />
        </button>
      </div>

      <div className="flex-1 p-4 space-y-4 max-w-4xl mx-auto w-full">
        {/* Bar Chart */}
        <div className="bg-white rounded-xl shadow-sm p-4 border border-gray-100">
          <div className="flex">
            {/* Y-axis labels */}
            <div
              className="flex flex-col justify-between text-right pr-2 text-xs text-gray-400"
              style={{ height: 200 }}
            >
              {Y_LABELS.map((label) => (
                <span key={label}>{formatNumber(label)}</span>
              ))}
            </div>

            {/* Chart bars */}
            <div className="flex-1 relative" style={{ height: 200 }}>
              {/* Horizontal grid lines */}
              <div className="absolute inset-0 flex flex-col justify-between pointer-events-none">
                {Y_LABELS.map((label) => (
                  <div key={label} className="border-t border-gray-100 w-full" />
                ))}
              </div>

              {/* Bars */}
              <div className="absolute inset-0 flex items-end justify-around px-1">
                {chartData.map((item) => {
                  const heightPct = item.value > 0 ? Math.min(100, (item.value / MAX_VALUE) * 100) : 2;
                  const isActive = item.value > 0;
                  return (
                    <div
                      key={item.date}
                      className="flex flex-col items-center justify-end h-full"
                      style={{ flex: 1 }}
                    >
                      <div
                        title={`${item.date}: ${formatNumber(item.value)} đ`}
                        className={`w-6 rounded-t-sm transition-all ${
                          isActive ? (item.date === "Hôm nay" ? "bg-emerald-500" : "bg-red-500") : "bg-gray-200"
                        }`}
                        style={{ height: `${heightPct}%` }}
                      />
                    </div>
                  );
                })}
              </div>
            </div>
          </div>

          {/* X-axis labels */}
          <div className="flex justify-around mt-1 pl-14">
            {chartData.map((item) => (
              <span
                key={item.date}
                className="text-xs text-gray-400 text-center font-medium"
                style={{ flex: 1 }}
              >
                {item.date}
              </span>
            ))}
          </div>
        </div>

        {/* Summary rows matching mockup */}
        <div className="bg-white rounded-xl shadow-sm divide-y divide-gray-100 border border-gray-100">
          <div className="flex justify-between items-center px-4 py-3">
            <span className="text-sm text-gray-600">Doanh thu tạm tính (Đơn đang phục vụ)</span>
            <span className="text-sm font-semibold text-gray-800">
              {formatNumber(pendingRevenue)} đ
            </span>
          </div>
          <div className="flex justify-between items-center px-4 py-3 bg-red-50/40">
            <span className="text-sm font-bold text-gray-900">Tổng thu thực tế (Thu ròng)</span>
            <span className="text-base font-black text-red-600">{formatNumber(totalRevenue)} đ</span>
          </div>
        </div>

        {/* Detailed Table */}
        <div className="bg-white rounded-xl shadow-sm overflow-hidden border border-gray-100">
          <div className="p-3 border-b bg-gray-50 flex items-center justify-between">
            <h3 className="text-xs font-bold uppercase tracking-wider text-gray-700">
              Bảng kê doanh thu theo ngày
            </h3>
            <span className="text-xs text-gray-500">
              Đồng bộ dữ liệu thời gian thực
            </span>
          </div>
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50/50 border-b border-gray-100">
                <th className="text-left px-4 py-3 text-gray-500 font-medium">STT</th>
                <th className="text-left px-4 py-3 text-gray-500 font-medium">Ngày bán</th>
                <th className="text-left px-4 py-3 text-gray-500 font-medium">Số đơn</th>
                <th className="text-right px-4 py-3 text-gray-500 font-medium">Doanh thu</th>
              </tr>
            </thead>
            <tbody>
              {todayRevenue > 0 && (
                <tr className="border-b border-gray-50 bg-emerald-50/30">
                  <td className="px-4 py-3 text-gray-700 font-bold">1</td>
                  <td className="px-4 py-3 text-gray-900 font-bold">
                    {salesDay.businessDate} (Hôm nay)
                  </td>
                  <td className="px-4 py-3 text-gray-600">{closedOrders.length} đơn</td>
                  <td className="px-4 py-3 text-right text-emerald-700 font-bold">
                    {formatNumber(todayRevenue)} đ
                  </td>
                </tr>
              )}
              <tr className="border-b border-gray-50">
                <td className="px-4 py-3 text-gray-700">{todayRevenue > 0 ? "2" : "1"}</td>
                <td className="px-4 py-3 text-gray-700">19-2-2023</td>
                <td className="px-4 py-3 text-gray-500">2 đơn</td>
                <td className="px-4 py-3 text-right text-gray-800 font-medium">188.320đ</td>
              </tr>
            </tbody>
          </table>
        </div>

        {/* Pagination dots */}
        <div className="flex justify-center space-x-2 py-2">
          <span className="w-2.5 h-2.5 rounded-full bg-red-500 inline-block" />
          <span className="w-2.5 h-2.5 rounded-full bg-gray-300 inline-block" />
          <span className="w-2.5 h-2.5 rounded-full bg-gray-300 inline-block" />
        </div>
      </div>
    </div>
  );
}
