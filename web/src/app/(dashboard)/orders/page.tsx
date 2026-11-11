"use client";

import { useState, useMemo, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import {
    Search,
    Plus,
    Clock,
    CheckCircle2,
    AlertCircle,
    Eye,
    Receipt,
    CreditCard,
    Banknote,
    Percent,
    X,
    ChefHat,
    Coffee,
    Check,
    Utensils,
    ArrowRight,
} from "lucide-react";
import {
    useRestaurant,
    type RestaurantOrder,
    type OrderStatus,
    type PaymentMethod,
} from "@/lib/store/restaurant-context";
import { cn } from "@/lib/utils";
import { toast } from "sonner";

function OrdersContent() {
    const searchParams = useSearchParams();
    const initialOrderId = searchParams.get("orderId");

    const {
        orders,
        tables,
        menuItems,
        salesDay,
        confirmOrder,
        serveItem,
        addPayment,
        waiveOrder,
        closeOrder,
        createOrder,
    } = useRestaurant();

    const [activeTab, setActiveTab] = useState<string>("ALL");
    const [search, setSearch] = useState("");

    // Modals
    const [selectedOrder, setSelectedOrder] = useState<RestaurantOrder | null>(
        () => {
            if (initialOrderId) {
                return orders.find((o) => o.id === initialOrderId) || null;
            }
            return null;
        },
    );

    const [createOrderModalOpen, setCreateOrderModalOpen] = useState(false);
    const [paymentModalOpen, setPaymentModalOpen] = useState(false);
    const [waiveModalOpen, setWaiveModalOpen] = useState(false);

    // Payment form state
    const [paymentAmount, setPaymentAmount] = useState<number>(0);
    const [paymentMethod, setPaymentMethod] =
        useState<PaymentMethod>("TIEN_MAT");
    const [cashGiven, setCashGiven] = useState<number>(0);

    // Waive form state
    const [waiveAmount, setWaiveAmount] = useState<number>(0);
    const [waiveReason, setWaiveReason] = useState<string>("");

    // Create Order form state
    const [createType, setCreateType] = useState<"TABLE" | "TAKEAWAY">("TABLE");
    const [selectedTableId, setSelectedTableId] = useState<string>("");
    const [customerNameInput, setCustomerNameInput] =
        useState<string>("Khách hàng");
    const [selectedDishes, setSelectedDishes] = useState<
        { dishId: number; quantity: number; note: string }[]
    >([]);

    // Update selectedOrder if orders update
    const currentOrder = useMemo(() => {
        if (!selectedOrder) return null;
        return orders.find((o) => o.id === selectedOrder.id) || selectedOrder;
    }, [orders, selectedOrder]);

    const filteredOrders = orders.filter((o) => {
        const matchTab =
            activeTab === "ALL" ||
            (activeTab === "CHO_XAC_NHAN" && o.status === "CHO_XAC_NHAN") ||
            (activeTab === "DANG_PHUC_VU" &&
                (o.status === "DANG_PHUC_VU" ||
                    o.status === "CHO_THANH_TOAN")) ||
            (activeTab === "TAKEAWAY" && o.isTakeaway) ||
            (activeTab === "DA_DONG" &&
                (o.status === "DA_DONG" || o.status === "DA_HUY"));

        const matchSearch =
            o.id.toLowerCase().includes(search.toLowerCase()) ||
            o.tableName.toLowerCase().includes(search.toLowerCase()) ||
            o.customerName.toLowerCase().includes(search.toLowerCase());
        return matchTab && matchSearch;
    });

    const getStatusBadge = (status: OrderStatus) => {
        switch (status) {
            case "CHO_XAC_NHAN":
                return (
                    <span className="flex items-center gap-1 text-xs font-semibold px-2.5 py-1 rounded-full bg-amber-100 text-amber-800 border border-amber-300">
                        <Clock size={12} /> Chờ xác nhận (UC06)
                    </span>
                );
            case "DANG_PHUC_VU":
                return (
                    <span className="flex items-center gap-1 text-xs font-semibold px-2.5 py-1 rounded-full bg-blue-100 text-blue-800 border border-blue-300">
                        <Utensils size={12} /> Đang phục vụ (UC09)
                    </span>
                );
            case "CHO_THANH_TOAN":
                return (
                    <span className="flex items-center gap-1 text-xs font-semibold px-2.5 py-1 rounded-full bg-purple-100 text-purple-800 border border-purple-300">
                        <Receipt size={12} /> Chờ thanh toán
                    </span>
                );
            case "DA_DONG":
                return (
                    <span className="flex items-center gap-1 text-xs font-semibold px-2.5 py-1 rounded-full bg-emerald-100 text-emerald-800 border border-emerald-300">
                        <CheckCircle2 size={12} /> Đã hoàn tất (UC20)
                    </span>
                );
            case "DA_HUY":
                return (
                    <span className="flex items-center gap-1 text-xs font-semibold px-2.5 py-1 rounded-full bg-red-100 text-red-800 border border-red-300">
                        <AlertCircle size={12} /> Đã hủy
                    </span>
                );
        }
    };

    // Helper for order balance
    const getOrderFinancials = (order: RestaurantOrder) => {
        const totalCost = order.items.reduce(
            (s, it) => s + it.price * it.quantity,
            0,
        );
        const paidAmount = order.payments.reduce((s, p) => s + p.amount, 0);
        const balance = totalCost - paidAmount - order.waivedAmount;
        return { totalCost, paidAmount, balance };
    };

    const openPaymentForOrder = (order: RestaurantOrder) => {
        const { balance } = getOrderFinancials(order);
        setPaymentAmount(Math.max(0, balance));
        setCashGiven(Math.max(0, balance));
        setPaymentMethod("TIEN_MAT");
        setPaymentModalOpen(true);
    };

    const handlePaymentSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        if (!currentOrder) return;
        addPayment(currentOrder.id, paymentAmount, paymentMethod, cashGiven);
        setPaymentModalOpen(false);
    };

    const handleWaiveSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        if (!currentOrder) return;
        if (!waiveReason.trim()) {
            toast.error("Vui lòng nhập lý do miễn thu bắt buộc!");
            return;
        }
        waiveOrder(currentOrder.id, waiveAmount, waiveReason);
        setWaiveModalOpen(false);
        setWaiveReason("");
    };

    const handleCreateOrderSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        if (selectedDishes.length === 0) {
            toast.error("Vui lòng chọn ít nhất 1 món ăn!");
            return;
        }
        const tableId = createType === "TABLE" ? selectedTableId : "MV";
        if (createType === "TABLE" && !tableId) {
            toast.error("Vui lòng chọn bàn!");
            return;
        }
        const newOrderId = createOrder(
            tableId,
            selectedDishes,
            customerNameInput,
            createType === "TAKEAWAY",
        );
        setCreateOrderModalOpen(false);
        setSelectedDishes([]);
        const created = orders.find((o) => o.id === newOrderId);
        if (created) setSelectedOrder(created);
    };

    return (
        <div className="min-h-screen bg-gray-50 flex flex-col space-y-4 pb-12">
            {/* Header */}
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-white p-4 rounded-xl shadow-sm border border-gray-100">
                <div>
                    <h1 className="text-xl font-bold text-gray-900 flex items-center gap-2">
                        <Receipt className="size-5 text-red-500" />
                        Quản lý Đơn hàng & Thanh toán
                    </h1>
                    <p className="text-xs text-gray-500 mt-0.5">
                        Theo dõi luồng: Tạo đơn (UC07) → Duyệt đợt (UC09) → Giao
                        món (UC15) → Thu tiền (UC17) → Đóng đơn trả bàn (UC20)
                    </p>
                </div>
                <button
                    onClick={() => {
                        if (salesDay.status !== "DANG_BAN") {
                            toast.error(
                                "Ngày bán chưa mở! Hãy mở ngày bán trước.",
                            );
                            return;
                        }
                        setCreateOrderModalOpen(true);
                    }}
                    className="flex items-center justify-center gap-1.5 bg-red-500 hover:bg-red-600 text-white text-xs font-semibold px-4 py-2.5 rounded-lg shadow-sm transition-colors"
                >
                    <Plus size={16} /> Tạo đơn mới (UC07)
                </button>
            </div>

            {/* Tabs theo đặc tả Tab 2: Quản lý Đơn hàng */}
            <div className="flex border-b border-gray-200 bg-white rounded-t-xl px-2 pt-2 gap-2 text-sm overflow-x-auto shadow-sm">
                {[
                    {
                        key: "CHO_XAC_NHAN",
                        label: `Chờ xác nhận (${orders.filter((o) => o.status === "CHO_XAC_NHAN").length})`,
                    },
                    {
                        key: "DANG_PHUC_VU",
                        label: `Đang phục vụ (${orders.filter((o) => o.status === "DANG_PHUC_VU" || o.status === "CHO_THANH_TOAN").length})`,
                    },
                    {
                        key: "TAKEAWAY",
                        label: `Đơn mang về (${orders.filter((o) => o.isTakeaway).length})`,
                    },
                    {
                        key: "DA_DONG",
                        label: `Đã đóng / Hủy (${orders.filter((o) => o.status === "DA_DONG" || o.status === "DA_HUY").length})`,
                    },
                    { key: "ALL", label: `Tất cả (${orders.length})` },
                ].map((tab) => (
                    <button
                        key={tab.key}
                        onClick={() => setActiveTab(tab.key)}
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

            {/* Search Bar */}
            <div className="relative">
                <Search
                    size={16}
                    className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400"
                />
                <input
                    type="text"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Tìm theo mã đơn, số bàn, tên khách hàng..."
                    className="w-full pl-9 pr-4 py-2.5 bg-white border border-gray-200 rounded-lg text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-red-500 shadow-sm"
                />
            </div>

            {/* Orders List */}
            <div className="space-y-3">
                {filteredOrders.length === 0 ? (
                    <div className="bg-white rounded-xl p-12 text-center text-gray-400 text-xs border">
                        Không tìm thấy đơn hàng nào phù hợp với bộ lọc.
                    </div>
                ) : (
                    filteredOrders.map((order) => {
                        const { totalCost, paidAmount, balance } =
                            getOrderFinancials(order);

                        return (
                            <div
                                key={order.id}
                                className="bg-white rounded-xl p-4 shadow-sm border border-gray-200 space-y-3 hover:border-gray-300 transition-colors"
                            >
                                {/* Header row */}
                                <div className="flex flex-wrap items-center justify-between gap-2">
                                    <div className="flex items-center gap-2">
                                        <span className="font-bold text-gray-900 text-base">
                                            {order.id}
                                        </span>
                                        <span className="text-xs bg-gray-100 text-gray-700 font-semibold px-2 py-0.5 rounded">
                                            {order.tableName}
                                        </span>
                                        {order.isTakeaway && (
                                            <span className="text-[10px] bg-orange-100 text-orange-700 font-bold px-2 py-0.5 rounded-full">
                                                Mang về
                                            </span>
                                        )}
                                    </div>
                                    <div className="flex items-center gap-2">
                                        {getStatusBadge(order.status)}
                                        <span className="text-xs text-gray-400">
                                            {order.createdAt}
                                        </span>
                                    </div>
                                </div>

                                {/* Customer & items preview */}
                                <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 text-xs text-gray-600 bg-gray-50/80 p-3 rounded-lg border border-gray-100">
                                    <div>
                                        <span className="text-gray-400">
                                            Khách:
                                        </span>{" "}
                                        <strong className="text-gray-800">
                                            {order.customerName}
                                        </strong>
                                        {order.customerPhone && (
                                            <span className="text-gray-400 ml-1">
                                                ({order.customerPhone})
                                            </span>
                                        )}
                                    </div>
                                    <div className="text-gray-500 truncate max-w-md">
                                        {order.items
                                            .map(
                                                (i) =>
                                                    `${i.name} (x${i.quantity})`,
                                            )
                                            .join(", ")}
                                    </div>
                                </div>

                                {/* Financial overview */}
                                <div className="flex flex-wrap items-center justify-between gap-2 pt-1 text-xs">
                                    <div className="flex items-center gap-4">
                                        <div>
                                            <span className="text-gray-400">
                                                Tổng tiền:
                                            </span>{" "}
                                            <strong className="text-gray-900">
                                                {totalCost.toLocaleString(
                                                    "vi-VN",
                                                )}{" "}
                                                đ
                                            </strong>
                                        </div>
                                        {paidAmount > 0 && (
                                            <div>
                                                <span className="text-gray-400">
                                                    Đã thu:
                                                </span>{" "}
                                                <strong className="text-emerald-600">
                                                    {paidAmount.toLocaleString(
                                                        "vi-VN",
                                                    )}{" "}
                                                    đ
                                                </strong>
                                            </div>
                                        )}
                                        {order.waivedAmount > 0 && (
                                            <div>
                                                <span className="text-gray-400">
                                                    Miễn thu:
                                                </span>{" "}
                                                <strong className="text-purple-600">
                                                    {order.waivedAmount.toLocaleString(
                                                        "vi-VN",
                                                    )}{" "}
                                                    đ
                                                </strong>
                                            </div>
                                        )}
                                        {order.status !== "DA_DONG" &&
                                            balance > 0 && (
                                                <div>
                                                    <span className="text-gray-400">
                                                        Còn thiếu:
                                                    </span>{" "}
                                                    <strong className="text-red-600">
                                                        {balance.toLocaleString(
                                                            "vi-VN",
                                                        )}{" "}
                                                        đ
                                                    </strong>
                                                </div>
                                            )}
                                    </div>

                                    {/* Fast Action Buttons */}
                                    <div className="flex items-center gap-2">
                                        {order.status === "CHO_XAC_NHAN" && (
                                            <button
                                                onClick={() =>
                                                    confirmOrder(order.id)
                                                }
                                                className="flex items-center gap-1 text-xs font-bold px-3 py-1.5 bg-red-600 hover:bg-red-700 text-white rounded-lg shadow-sm transition-colors"
                                            >
                                                <Check size={14} /> Duyệt đợt
                                                món (UC09)
                                            </button>
                                        )}

                                        <button
                                            onClick={() =>
                                                setSelectedOrder(order)
                                            }
                                            className="flex items-center gap-1 text-xs font-semibold px-3 py-1.5 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-lg transition-colors"
                                        >
                                            <Eye size={14} /> Chi tiết đơn
                                        </button>
                                    </div>
                                </div>
                            </div>
                        );
                    })
                )}
            </div>

            {/* ── MODAL: CHI TIẾT ĐƠN HÀNG ───────────────────────────────────── */}
            {currentOrder && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4 overflow-y-auto">
                    <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl border space-y-5 animate-in fade-in zoom-in-95 my-8">
                        {/* Modal Header */}
                        <div className="flex items-center justify-between border-b pb-3">
                            <div>
                                <div className="flex items-center gap-2">
                                    <h2 className="text-lg font-bold text-gray-900">
                                        Chi tiết đơn {currentOrder.id}
                                    </h2>
                                    <span className="text-xs bg-gray-100 font-bold px-2 py-0.5 rounded text-gray-700">
                                        {currentOrder.tableName}
                                    </span>
                                </div>
                                <p className="text-xs text-gray-500 mt-0.5">
                                    Khách: {currentOrder.customerName} • Tạo
                                    lúc: {currentOrder.createdAt}
                                </p>
                            </div>
                            <button
                                onClick={() => setSelectedOrder(null)}
                                className="p-1 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100"
                            >
                                <X size={20} />
                            </button>
                        </div>

                        {/* Status indicator */}
                        <div className="flex items-center justify-between bg-gray-50 p-3 rounded-xl border">
                            <span className="text-xs text-gray-500">
                                Trạng thái vòng đời:
                            </span>
                            {getStatusBadge(currentOrder.status)}
                        </div>

                        {/* Dishes progress table */}
                        <div className="space-y-2">
                            <div className="flex items-center justify-between">
                                <h3 className="text-xs font-bold uppercase tracking-wider text-gray-600">
                                    Danh sách món gọi (
                                    {currentOrder.items.length} món)
                                </h3>
                                <span className="text-xs text-gray-400">
                                    Đợt phục vụ tại bàn
                                </span>
                            </div>

                            <div className="border rounded-xl divide-y divide-gray-100 overflow-hidden text-xs">
                                {currentOrder.items.map((it) => (
                                    <div
                                        key={it.id}
                                        className="p-3 flex items-center justify-between bg-white"
                                    >
                                        <div className="space-y-0.5">
                                            <div className="flex items-center gap-2">
                                                <strong className="text-gray-900">
                                                    {it.name}
                                                </strong>
                                                <span className="text-gray-400">
                                                    x{it.quantity}
                                                </span>
                                                <span className="text-[10px] text-gray-500 bg-gray-100 px-1.5 py-0.5 rounded">
                                                    {it.station === "BEP"
                                                        ? "🍳 Bếp"
                                                        : "🍹 Bar"}
                                                </span>
                                            </div>
                                            <span className="text-gray-500">
                                                {(
                                                    it.price * it.quantity
                                                ).toLocaleString("vi-VN")}{" "}
                                                đ
                                            </span>
                                        </div>

                                        <div className="flex items-center gap-2">
                                            <span
                                                className={cn(
                                                    "px-2 py-0.5 rounded-full font-semibold text-[11px]",
                                                    it.status === "CHO_LAM" &&
                                                        "bg-amber-100 text-amber-800",
                                                    it.status === "DANG_LAM" &&
                                                        "bg-blue-100 text-blue-800",
                                                    it.status === "SAN_SANG" &&
                                                        "bg-emerald-100 text-emerald-800",
                                                    it.status === "DA_GIAO" &&
                                                        "bg-gray-100 text-gray-700",
                                                )}
                                            >
                                                {it.status === "CHO_LAM" &&
                                                    "Chờ làm"}
                                                {it.status === "DANG_LAM" &&
                                                    "Đang nấu"}
                                                {it.status === "SAN_SANG" &&
                                                    "Sẵn sàng"}
                                                {it.status === "DA_GIAO" &&
                                                    "Đã giao"}
                                            </span>

                                            {/* Staff quick serve button */}
                                            {it.status === "SAN_SANG" && (
                                                <button
                                                    onClick={() =>
                                                        serveItem(
                                                            currentOrder.id,
                                                            it.id,
                                                        )
                                                    }
                                                    className="px-2 py-1 bg-emerald-600 hover:bg-emerald-700 text-white rounded font-bold transition-colors"
                                                >
                                                    Bưng bàn (UC15)
                                                </button>
                                            )}
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </div>

                        {/* Financial & Payments history */}
                        {(() => {
                            const { totalCost, paidAmount, balance } =
                                getOrderFinancials(currentOrder);
                            return (
                                <div className="bg-gray-50 rounded-xl p-4 border space-y-3 text-xs">
                                    <div className="flex justify-between items-center text-sm font-bold text-gray-900 border-b pb-2">
                                        <span>Tổng tiền hóa đơn:</span>
                                        <span>
                                            {totalCost.toLocaleString("vi-VN")}{" "}
                                            đ
                                        </span>
                                    </div>

                                    {currentOrder.payments.length > 0 && (
                                        <div className="space-y-1">
                                            <span className="font-semibold text-gray-600">
                                                Chứng từ đã thu (UC17):
                                            </span>
                                            {currentOrder.payments.map((p) => (
                                                <div
                                                    key={p.id}
                                                    className="flex justify-between text-emerald-700"
                                                >
                                                    <span>
                                                        •{" "}
                                                        {p.method === "TIEN_MAT"
                                                            ? "Tiền mặt"
                                                            : "Chuyển khoản"}{" "}
                                                        ({p.paidAt})
                                                        {p.changeAmount
                                                            ? ` (Tiền thừa: ${p.changeAmount.toLocaleString("vi-VN")}đ)`
                                                            : ""}
                                                    </span>
                                                    <strong>
                                                        +
                                                        {p.amount.toLocaleString(
                                                            "vi-VN",
                                                        )}{" "}
                                                        đ
                                                    </strong>
                                                </div>
                                            ))}
                                        </div>
                                    )}

                                    {currentOrder.waivedAmount > 0 && (
                                        <div className="flex justify-between text-purple-700">
                                            <span>
                                                • Miễn thu:{" "}
                                                {currentOrder.waiveReason}
                                            </span>
                                            <strong>
                                                -
                                                {currentOrder.waivedAmount.toLocaleString(
                                                    "vi-VN",
                                                )}{" "}
                                                đ
                                            </strong>
                                        </div>
                                    )}

                                    <div className="flex justify-between text-sm font-bold text-red-600 border-t pt-2">
                                        <span>Còn phải thanh toán:</span>
                                        <span>
                                            {Math.max(
                                                0,
                                                balance,
                                            ).toLocaleString("vi-VN")}{" "}
                                            đ
                                        </span>
                                    </div>
                                </div>
                            );
                        })()}

                        {/* Operational Action Buttons */}
                        <div className="pt-2 border-t flex flex-wrap items-center justify-end gap-2 text-xs">
                            {currentOrder.status === "CHO_XAC_NHAN" && (
                                <button
                                    onClick={() =>
                                        confirmOrder(currentOrder.id)
                                    }
                                    className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white font-bold rounded-lg shadow-sm"
                                >
                                    Xác nhận đợt gọi món (UC09)
                                </button>
                            )}

                            {currentOrder.status !== "DA_DONG" && (
                                <>
                                    <button
                                        onClick={() => {
                                            const { balance } =
                                                getOrderFinancials(
                                                    currentOrder,
                                                );
                                            setWaiveAmount(
                                                Math.max(0, balance),
                                            );
                                            setWaiveModalOpen(true);
                                        }}
                                        className="px-3 py-2 bg-purple-100 hover:bg-purple-200 text-purple-800 font-semibold rounded-lg transition-colors"
                                    >
                                        <Percent
                                            size={14}
                                            className="inline mr-1"
                                        />
                                        Miễn thu (UC19)
                                    </button>

                                    <button
                                        onClick={() =>
                                            openPaymentForOrder(currentOrder)
                                        }
                                        className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white font-bold rounded-lg shadow-sm transition-colors flex items-center gap-1.5"
                                    >
                                        <Banknote size={15} />
                                        Thu tiền (UC17)
                                    </button>

                                    <button
                                        onClick={() => {
                                            const success = closeOrder(
                                                currentOrder.id,
                                            );
                                            if (success) setSelectedOrder(null);
                                        }}
                                        className="px-4 py-2 bg-gray-900 hover:bg-black text-white font-bold rounded-lg shadow-sm transition-colors flex items-center gap-1.5"
                                    >
                                        <CheckCircle2 size={15} />
                                        Đóng đơn & Giải phóng bàn (UC20)
                                    </button>
                                </>
                            )}
                        </div>
                    </div>
                </div>
            )}

            {/* ── MODAL: THU TIỀN ĐƠN HÀNG (UC17) ───────────────────────────── */}
            {paymentModalOpen && currentOrder && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
                    <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border space-y-4 animate-in fade-in zoom-in-95">
                        <h3 className="text-base font-bold text-gray-900">
                            Lập chứng từ thu tiền {currentOrder.id}
                        </h3>

                        <form
                            onSubmit={handlePaymentSubmit}
                            className="space-y-4 text-xs"
                        >
                            {/* Payment Method */}
                            <div>
                                <label className="block font-semibold text-gray-700 mb-1">
                                    Phương thức thanh toán:
                                </label>
                                <div className="grid grid-cols-2 gap-2">
                                    <button
                                        type="button"
                                        onClick={() =>
                                            setPaymentMethod("TIEN_MAT")
                                        }
                                        className={cn(
                                            "p-3 rounded-xl border flex items-center justify-center gap-2 font-bold transition-colors",
                                            paymentMethod === "TIEN_MAT"
                                                ? "bg-emerald-50 border-emerald-500 text-emerald-800"
                                                : "bg-gray-50 border-gray-200 text-gray-600",
                                        )}
                                    >
                                        <Banknote size={16} /> Tiền mặt
                                    </button>
                                    <button
                                        type="button"
                                        onClick={() =>
                                            setPaymentMethod("CHUYEN_KHOAN")
                                        }
                                        className={cn(
                                            "p-3 rounded-xl border flex items-center justify-center gap-2 font-bold transition-colors",
                                            paymentMethod === "CHUYEN_KHOAN"
                                                ? "bg-blue-50 border-blue-500 text-blue-800"
                                                : "bg-gray-50 border-gray-200 text-gray-600",
                                        )}
                                    >
                                        <CreditCard size={16} /> Chuyển khoản
                                        (QR)
                                    </button>
                                </div>
                            </div>

                            {/* Amount to collect */}
                            <div>
                                <label className="block font-semibold text-gray-700 mb-1">
                                    Số tiền thu (đ):
                                </label>
                                <input
                                    type="number"
                                    step={1000}
                                    value={paymentAmount}
                                    onChange={(e) =>
                                        setPaymentAmount(Number(e.target.value))
                                    }
                                    required
                                    className="w-full px-3 py-2 border rounded-lg text-sm font-bold focus:ring-2 focus:ring-emerald-500 focus:outline-none"
                                />
                            </div>

                            {/* If Cash: calculate change */}
                            {paymentMethod === "TIEN_MAT" && (
                                <div className="space-y-2 bg-emerald-50/50 p-3 rounded-xl border border-emerald-100">
                                    <label className="block font-semibold text-gray-700">
                                        Tiền khách đưa (đ):
                                    </label>
                                    <input
                                        type="number"
                                        step={1000}
                                        value={cashGiven}
                                        onChange={(e) =>
                                            setCashGiven(Number(e.target.value))
                                        }
                                        className="w-full px-3 py-2 border rounded-lg text-sm font-bold focus:ring-2 focus:ring-emerald-500 focus:outline-none bg-white"
                                    />
                                    <div className="flex justify-between items-center text-xs font-bold pt-1">
                                        <span className="text-gray-600">
                                            Tiền thừa trả lại:
                                        </span>
                                        <span className="text-emerald-700 text-sm">
                                            {Math.max(
                                                0,
                                                cashGiven - paymentAmount,
                                            ).toLocaleString("vi-VN")}{" "}
                                            đ
                                        </span>
                                    </div>
                                </div>
                            )}

                            <div className="flex items-center gap-2 pt-2 border-t">
                                <button
                                    type="button"
                                    onClick={() => setPaymentModalOpen(false)}
                                    className="flex-1 py-2 font-semibold text-gray-600 hover:bg-gray-100 rounded-lg"
                                >
                                    Hủy
                                </button>
                                <button
                                    type="submit"
                                    className="flex-1 py-2 font-bold bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg shadow-sm"
                                >
                                    Xác nhận thu tiền
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* ── MODAL: MIỄN THU (UC19) ────────────────────────────────────── */}
            {waiveModalOpen && currentOrder && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
                    <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border space-y-4 animate-in fade-in zoom-in-95">
                        <h3 className="text-base font-bold text-gray-900">
                            Quản lý duyệt miễn thu {currentOrder.id} (UC19)
                        </h3>
                        <p className="text-xs text-gray-500">
                            Chỉ Quản lý được phép miễn thu phần nghĩa vụ thanh
                            toán chưa thu. Lý do bắt buộc phải được ghi nhận để
                            kiểm toán.
                        </p>

                        <form
                            onSubmit={handleWaiveSubmit}
                            className="space-y-4 text-xs"
                        >
                            <div>
                                <label className="block font-semibold text-gray-700 mb-1">
                                    Số tiền miễn thu (đ):
                                </label>
                                <input
                                    type="number"
                                    step={1000}
                                    value={waiveAmount}
                                    onChange={(e) =>
                                        setWaiveAmount(Number(e.target.value))
                                    }
                                    required
                                    className="w-full px-3 py-2 border rounded-lg text-sm font-bold focus:ring-2 focus:ring-purple-500 focus:outline-none"
                                />
                            </div>

                            <div>
                                <label className="block font-semibold text-gray-700 mb-1">
                                    Lý do miễn thu (Bắt buộc):
                                </label>
                                <textarea
                                    rows={2}
                                    value={waiveReason}
                                    onChange={(e) =>
                                        setWaiveReason(e.target.value)
                                    }
                                    required
                                    placeholder="VD: Món phục vụ muộn khách phàn nàn, Khách VIP tri ân..."
                                    className="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-purple-500 focus:outline-none"
                                />
                            </div>

                            <div className="flex items-center gap-2 pt-2 border-t">
                                <button
                                    type="button"
                                    onClick={() => setWaiveModalOpen(false)}
                                    className="flex-1 py-2 font-semibold text-gray-600 hover:bg-gray-100 rounded-lg"
                                >
                                    Hủy
                                </button>
                                <button
                                    type="submit"
                                    className="flex-1 py-2 font-bold bg-purple-600 hover:bg-purple-700 text-white rounded-lg shadow-sm"
                                >
                                    Duyệt miễn thu
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* ── MODAL: TẠO ĐƠN MỚI (UC07) ─────────────────────────────────── */}
            {createOrderModalOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 overflow-y-auto">
                    <div className="bg-white rounded-2xl max-w-xl w-full p-6 shadow-2xl border space-y-4 animate-in fade-in zoom-in-95 my-8">
                        <div className="flex items-center justify-between border-b pb-3">
                            <h3 className="text-base font-bold text-gray-900">
                                Tạo đơn hàng mới (UC07 - Tại bàn / Mang về)
                            </h3>
                            <button
                                onClick={() => setCreateOrderModalOpen(false)}
                                className="p-1 rounded-lg text-gray-400 hover:text-gray-600"
                            >
                                <X size={20} />
                            </button>
                        </div>

                        <form
                            onSubmit={handleCreateOrderSubmit}
                            className="space-y-4 text-xs"
                        >
                            {/* Type Switch */}
                            <div className="grid grid-cols-2 gap-2">
                                <button
                                    type="button"
                                    onClick={() => setCreateType("TABLE")}
                                    className={cn(
                                        "py-2 rounded-lg font-bold border transition-colors",
                                        createType === "TABLE"
                                            ? "bg-red-50 border-red-500 text-red-700"
                                            : "bg-gray-50 border-gray-200 text-gray-600",
                                    )}
                                >
                                    Tại bàn
                                </button>
                                <button
                                    type="button"
                                    onClick={() => setCreateType("TAKEAWAY")}
                                    className={cn(
                                        "py-2 rounded-lg font-bold border transition-colors",
                                        createType === "TAKEAWAY"
                                            ? "bg-orange-50 border-orange-500 text-orange-700"
                                            : "bg-gray-50 border-gray-200 text-gray-600",
                                    )}
                                >
                                    Mang về
                                </button>
                            </div>

                            {/* Table select if table */}
                            {createType === "TABLE" ? (
                                <div>
                                    <label className="block font-semibold text-gray-700 mb-1">
                                        Chọn bàn:
                                    </label>
                                    <select
                                        value={selectedTableId}
                                        onChange={(e) =>
                                            setSelectedTableId(e.target.value)
                                        }
                                        required
                                        className="w-full px-3 py-2 border rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-red-500"
                                    >
                                        <option value="">
                                            -- Chọn bàn phục vụ --
                                        </option>
                                        {tables.map((t) => (
                                            <option key={t.id} value={t.id}>
                                                {t.name} ({t.area}){" "}
                                                {t.status === "DANG_DUNG"
                                                    ? "- [Đang dùng]"
                                                    : "- [Trống]"}
                                            </option>
                                        ))}
                                    </select>
                                </div>
                            ) : (
                                <div>
                                    <label className="block font-semibold text-gray-700 mb-1">
                                        Mã mang về:
                                    </label>
                                    <input
                                        type="text"
                                        disabled
                                        value="Tự động sinh (MV-XX)"
                                        className="w-full px-3 py-2 border rounded-lg bg-gray-100 text-gray-500"
                                    />
                                </div>
                            )}

                            {/* Customer Name */}
                            <div>
                                <label className="block font-semibold text-gray-700 mb-1">
                                    Tên khách hàng:
                                </label>
                                <input
                                    type="text"
                                    value={customerNameInput}
                                    onChange={(e) =>
                                        setCustomerNameInput(e.target.value)
                                    }
                                    placeholder="Khách hàng"
                                    className="w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-red-500"
                                />
                            </div>

                            {/* Dish selection list */}
                            <div className="space-y-2">
                                <label className="block font-semibold text-gray-700">
                                    Chọn món từ thực đơn (Chỉ món còn hàng):
                                </label>
                                <div className="max-h-56 overflow-y-auto border rounded-xl divide-y divide-gray-100">
                                    {menuItems.map((dish) => {
                                        const selected = selectedDishes.find(
                                            (d) => d.dishId === dish.id,
                                        );
                                        return (
                                            <div
                                                key={dish.id}
                                                className={cn(
                                                    "p-2.5 flex items-center justify-between transition-colors",
                                                    dish.outOfStock
                                                        ? "bg-gray-100 opacity-60"
                                                        : "bg-white hover:bg-gray-50",
                                                )}
                                            >
                                                <div>
                                                    <div className="flex items-center gap-2">
                                                        <span className="font-semibold text-gray-900">
                                                            {dish.name}
                                                        </span>
                                                        <span className="text-[10px] text-gray-500 bg-gray-100 px-1 py-0.5 rounded">
                                                            {dish.station ===
                                                            "BEP"
                                                                ? "Bếp"
                                                                : "Bar"}
                                                        </span>
                                                    </div>
                                                    <span className="text-gray-500">
                                                        {dish.price.toLocaleString(
                                                            "vi-VN",
                                                        )}{" "}
                                                        đ
                                                    </span>
                                                </div>

                                                {dish.outOfStock ? (
                                                    <span className="text-xs text-red-500 font-bold">
                                                        Đã hết
                                                    </span>
                                                ) : selected ? (
                                                    <div className="flex items-center gap-1.5">
                                                        <button
                                                            type="button"
                                                            onClick={() => {
                                                                if (
                                                                    selected.quantity >
                                                                    1
                                                                ) {
                                                                    setSelectedDishes(
                                                                        (
                                                                            prev,
                                                                        ) =>
                                                                            prev.map(
                                                                                (
                                                                                    d,
                                                                                ) =>
                                                                                    d.dishId ===
                                                                                    dish.id
                                                                                        ? {
                                                                                              ...d,
                                                                                              quantity:
                                                                                                  d.quantity -
                                                                                                  1,
                                                                                          }
                                                                                        : d,
                                                                            ),
                                                                    );
                                                                } else {
                                                                    setSelectedDishes(
                                                                        (
                                                                            prev,
                                                                        ) =>
                                                                            prev.filter(
                                                                                (
                                                                                    d,
                                                                                ) =>
                                                                                    d.dishId !==
                                                                                    dish.id,
                                                                            ),
                                                                    );
                                                                }
                                                            }}
                                                            className="size-6 bg-gray-200 rounded text-gray-800 font-bold"
                                                        >
                                                            -
                                                        </button>
                                                        <span className="w-5 text-center font-bold">
                                                            {selected.quantity}
                                                        </span>
                                                        <button
                                                            type="button"
                                                            onClick={() =>
                                                                setSelectedDishes(
                                                                    (prev) =>
                                                                        prev.map(
                                                                            (
                                                                                d,
                                                                            ) =>
                                                                                d.dishId ===
                                                                                dish.id
                                                                                    ? {
                                                                                          ...d,
                                                                                          quantity:
                                                                                              d.quantity +
                                                                                              1,
                                                                                      }
                                                                                    : d,
                                                                        ),
                                                                )
                                                            }
                                                            className="size-6 bg-red-500 rounded text-white font-bold"
                                                        >
                                                            +
                                                        </button>
                                                    </div>
                                                ) : (
                                                    <button
                                                        type="button"
                                                        onClick={() =>
                                                            setSelectedDishes(
                                                                (prev) => [
                                                                    ...prev,
                                                                    {
                                                                        dishId: dish.id,
                                                                        quantity: 1,
                                                                        note: "",
                                                                    },
                                                                ],
                                                            )
                                                        }
                                                        className="px-2.5 py-1 bg-red-50 text-red-600 border border-red-200 rounded font-semibold hover:bg-red-100"
                                                    >
                                                        + Chọn
                                                    </button>
                                                )}
                                            </div>
                                        );
                                    })}
                                </div>
                            </div>

                            <div className="flex items-center gap-2 pt-2 border-t">
                                <button
                                    type="button"
                                    onClick={() =>
                                        setCreateOrderModalOpen(false)
                                    }
                                    className="flex-1 py-2 font-semibold text-gray-600 hover:bg-gray-100 rounded-lg"
                                >
                                    Hủy
                                </button>
                                <button
                                    type="submit"
                                    disabled={selectedDishes.length === 0}
                                    className="flex-1 py-2 font-bold bg-red-600 hover:bg-red-700 disabled:opacity-50 text-white rounded-lg shadow-sm"
                                >
                                    Gửi đợt gọi món (UC06)
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    );
}

export default function OrdersPage() {
    return (
        <Suspense
            fallback={
                <div className="p-8 text-center text-sm text-gray-500">
                    Đang tải đơn hàng...
                </div>
            }
        >
            <OrdersContent />
        </Suspense>
    );
}
