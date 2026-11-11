"use client";

import {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  type ReactNode,
} from "react";
import { toast } from "sonner";

// ── TYPES THEO ĐẶC TẢ ĐỀ TÀI ──────────────────────────────────────────

export type TableStatus = "TRONG" | "DANG_DUNG";

export interface RestaurantTable {
  id: string;
  name: string;
  area: string; // Tầng 1, Tầng 2, Ngoài trời
  status: TableStatus;
  currentOrderId?: string;
  guestCount?: number;
  openedAt?: string;
}

export type OrderStatus =
  | "CHO_XAC_NHAN" // Khách QR hoặc nhân viên vừa gửi đợt
  | "DANG_PHUC_VU" // Đã xác nhận, các món đang làm hoặc đang phục vụ
  | "CHO_THANH_TOAN" // Đã giao hết món, chờ thanh toán
  | "DA_DONG" // Mọi món kết thúc + thu đủ/miễn thu
  | "DA_HUY"; // Hủy đơn

export type DishStation = "BEP" | "BAR";
export type DishItemStatus =
  | "CHO_LAM" // Chờ bếp/bar tiếp nhận
  | "DANG_LAM" // Đang chế biến
  | "SAN_SANG" // Đã làm xong, chờ nhân viên bưng
  | "DA_GIAO" // Đã phục vụ tại bàn
  | "DA_HUY"; // Hủy món

export interface OrderItemDetail {
  id: string;
  dishId: number;
  name: string;
  price: number;
  quantity: number;
  station: DishStation;
  status: DishItemStatus;
  note?: string;
}

export type PaymentMethod = "TIEN_MAT" | "CHUYEN_KHOAN";

export interface PaymentRecord {
  id: string;
  amount: number;
  method: PaymentMethod;
  cashGiven?: number;
  changeAmount?: number;
  paidAt: string;
}

export interface RestaurantOrder {
  id: string;
  tableId: string;
  tableName: string;
  isTakeaway: boolean;
  takeawayCode?: string;
  customerName: string;
  customerPhone?: string;
  status: OrderStatus;
  items: OrderItemDetail[];
  payments: PaymentRecord[];
  waivedAmount: number; // Tiền miễn thu (UC19)
  waiveReason?: string;
  createdAt: string;
  closedAt?: string;
}

export interface MenuItem {
  id: number;
  name: string;
  category: "KHAI_VI" | "MON_CHINH" | "DO_UONG" | "TRANG_MIENG";
  price: number;
  station: DishStation;
  outOfStock: boolean;
  imageUrl?: string | null;
}

export type SalesDayStatus = "CHUA_MO" | "DANG_BAN" | "CHO_CHOT" | "DA_CHOT";

export interface SalesDayState {
  status: SalesDayStatus;
  businessDate: string;
  openingCash: number;
  closingCash?: number;
  openedAt?: string;
  closedAt?: string;
}

// ── CONTEXT VALUE INTERFACE ──────────────────────────────────────────

interface RestaurantContextValue {
  tables: RestaurantTable[];
  orders: RestaurantOrder[];
  menuItems: MenuItem[];
  salesDay: SalesDayState;

  // Actions
  openTableSession: (tableId: string, guestCount?: number) => void;
  transferTable: (fromTableId: string, toTableId: string) => boolean;
  createOrder: (
    tableId: string,
    items: { dishId: number; quantity: number; note?: string }[],
    customerName?: string,
    isTakeaway?: boolean,
  ) => string;
  confirmOrder: (orderId: string) => void;
  startCookingItem: (orderId: string, itemId: string) => void;
  finishCookingItem: (orderId: string, itemId: string) => void;
  serveItem: (orderId: string, itemId: string) => void;
  addPayment: (
    orderId: string,
    amount: number,
    method: PaymentMethod,
    cashGiven?: number,
  ) => void;
  waiveOrder: (orderId: string, amount: number, reason: string) => void;
  closeOrder: (orderId: string) => boolean;
  toggleDishStock: (dishId: number) => void;
  openSalesDay: (openingCash: number) => void;
  closeSalesDay: (actualCash: number, note?: string) => boolean;
}

const RestaurantContext = createContext<RestaurantContextValue | null>(null);

// ── DEFAULT SEED DATA ────────────────────────────────────────────────

const initialTables: RestaurantTable[] = [
  { id: "T01", name: "Bàn 01", area: "Tầng 1", status: "TRONG" },
  { id: "T02", name: "Bàn 02", area: "Tầng 1", status: "TRONG" },
  { id: "T03", name: "Bàn 03", area: "Tầng 1", status: "TRONG" },
  {
    id: "T04",
    name: "Bàn 04",
    area: "Tầng 1",
    status: "DANG_DUNG",
    currentOrderId: "DH-1024",
    guestCount: 2,
    openedAt: "10:15",
  },
  {
    id: "T08",
    name: "Bàn 08",
    area: "Tầng 2",
    status: "DANG_DUNG",
    currentOrderId: "DH-1023",
    guestCount: 4,
    openedAt: "10:30",
  },
  { id: "T05", name: "Bàn 05", area: "Tầng 2", status: "TRONG" },
  { id: "T06", name: "Bàn 06", area: "Tầng 2", status: "TRONG" },
  { id: "T07", name: "Bàn 07", area: "Ngoài trời", status: "TRONG" },
];

const initialMenuItems: MenuItem[] = [
  { id: 1, name: "Cháo sườn リブコンジー", category: "KHAI_VI", price: 25000, station: "BEP", outOfStock: true },
  { id: 2, name: "Khoai tây chiên", category: "KHAI_VI", price: 80000, station: "BEP", outOfStock: true },
  { id: 3, name: "Món B (Salad cá hồi)", category: "KHAI_VI", price: 80000, station: "BEP", outOfStock: false },
  { id: 4, name: "Ngô chiên bơ", category: "KHAI_VI", price: 40000, station: "BEP", outOfStock: false },
  { id: 5, name: "Cơm sườn nướng đặc biệt", category: "MON_CHINH", price: 65000, station: "BEP", outOfStock: false },
  { id: 6, name: "Bún bò Huế gia truyền", category: "MON_CHINH", price: 55000, station: "BEP", outOfStock: false },
  { id: 7, name: "Trà chanh sả quế", category: "DO_UONG", price: 25000, station: "BAR", outOfStock: false },
  { id: 8, name: "Cà phê muối Huế", category: "DO_UONG", price: 35000, station: "BAR", outOfStock: false },
];

const initialOrders: RestaurantOrder[] = [
  {
    id: "DH-1024",
    tableId: "T04",
    tableName: "Bàn 04",
    isTakeaway: false,
    customerName: "Đăng",
    customerPhone: "+84985623569",
    status: "DANG_PHUC_VU",
    createdAt: "10:15",
    waivedAmount: 0,
    payments: [],
    items: [
      { id: "it-1", dishId: 1, name: "Cháo sườn", price: 25000, quantity: 2, station: "BEP", status: "DANG_LAM" },
      { id: "it-2", dishId: 2, name: "Khoai tây chiên", price: 80000, quantity: 1, station: "BEP", status: "SAN_SANG" },
      { id: "it-7", dishId: 7, name: "Trà chanh sả quế", price: 25000, quantity: 2, station: "BAR", status: "DA_GIAO" },
    ],
  },
  {
    id: "DH-1023",
    tableId: "T08",
    tableName: "Bàn 08",
    isTakeaway: false,
    customerName: "Nguyen",
    customerPhone: "+84902646558",
    status: "CHO_XAC_NHAN",
    createdAt: "10:30",
    waivedAmount: 0,
    payments: [],
    items: [
      { id: "it-3", dishId: 3, name: "Món B (Salad cá hồi)", price: 80000, quantity: 2, station: "BEP", status: "CHO_LAM" },
      { id: "it-4", dishId: 4, name: "Ngô chiên bơ", price: 40000, quantity: 1, station: "BEP", status: "CHO_LAM" },
    ],
  },
  {
    id: "DH-1022",
    tableId: "MV-01",
    tableName: "Mang về #01",
    isTakeaway: true,
    takeawayCode: "MV-01",
    customerName: "Khách lẻ",
    status: "DA_DONG",
    createdAt: "09:00",
    closedAt: "09:20",
    waivedAmount: 0,
    payments: [{ id: "p-1", amount: 65000, method: "TIEN_MAT", cashGiven: 100000, changeAmount: 35000, paidAt: "09:18" }],
    items: [
      { id: "it-5", dishId: 5, name: "Cơm sườn nướng đặc biệt", price: 65000, quantity: 1, station: "BEP", status: "DA_GIAO" },
    ],
  },
];

export function RestaurantProvider({ children }: { children: ReactNode }) {
  const [tables, setTables] = useState<RestaurantTable[]>(() => {
    if (typeof window !== "undefined") {
      const saved = localStorage.getItem("restaurant_tables");
      if (saved) try { return JSON.parse(saved); } catch {}
    }
    return initialTables;
  });

  const [orders, setOrders] = useState<RestaurantOrder[]>(() => {
    if (typeof window !== "undefined") {
      const saved = localStorage.getItem("restaurant_orders");
      if (saved) try { return JSON.parse(saved); } catch {}
    }
    return initialOrders;
  });

  const [menuItems, setMenuItems] = useState<MenuItem[]>(() => {
    if (typeof window !== "undefined") {
      const saved = localStorage.getItem("restaurant_menu");
      if (saved) try { return JSON.parse(saved); } catch {}
    }
    return initialMenuItems;
  });

  const [salesDay, setSalesDay] = useState<SalesDayState>(() => {
    if (typeof window !== "undefined") {
      const saved = localStorage.getItem("restaurant_salesday");
      if (saved) try { return JSON.parse(saved); } catch {}
    }
    return {
      status: "DANG_BAN",
      businessDate: "2026-11-11",
      openingCash: 1000000,
      openedAt: "07:30",
    };
  });

  // Sync to localStorage
  useEffect(() => {
    localStorage.setItem("restaurant_tables", JSON.stringify(tables));
  }, [tables]);

  useEffect(() => {
    localStorage.setItem("restaurant_orders", JSON.stringify(orders));
  }, [orders]);

  useEffect(() => {
    localStorage.setItem("restaurant_menu", JSON.stringify(menuItems));
  }, [menuItems]);

  useEffect(() => {
    localStorage.setItem("restaurant_salesday", JSON.stringify(salesDay));
  }, [salesDay]);

  // ── 1. MỞ PHIÊN BÀN (UC03) ──────────────────────────────────────────
  const openTableSession = useCallback((tableId: string, guestCount: number = 2) => {
    if (salesDay.status !== "DANG_BAN") {
      toast.error("Ngày bán chưa mở hoặc đã chốt. Hãy mở ngày bán trước!");
      return;
    }
    setTables((prev) =>
      prev.map((t) =>
        t.id === tableId
          ? {
              ...t,
              status: "DANG_DUNG",
              guestCount,
              openedAt: new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" }),
            }
          : t,
      ),
    );
    toast.success(`Đã mở phiên cho ${tableId} (${guestCount} khách).`);
  }, [salesDay.status]);

  // ── 2. CHUYỂN BÀN (UC03) ─────────────────────────────────────────────
  const transferTable = useCallback((fromTableId: string, toTableId: string) => {
    const toTable = tables.find((t) => t.id === toTableId);
    if (!toTable || toTable.status !== "TRONG") {
      toast.error("Bàn đích không trống, không thể chuyển!");
      return false;
    }

    const fromTable = tables.find((t) => t.id === fromTableId);
    if (!fromTable) return false;

    // Update tables and order
    setTables((prev) =>
      prev.map((t) => {
        if (t.id === fromTableId) {
          return { ...t, status: "TRONG", currentOrderId: undefined, guestCount: undefined, openedAt: undefined };
        }
        if (t.id === toTableId) {
          return {
            ...t,
            status: "DANG_DUNG",
            currentOrderId: fromTable.currentOrderId,
            guestCount: fromTable.guestCount,
            openedAt: fromTable.openedAt,
          };
        }
        return t;
      }),
    );

    if (fromTable.currentOrderId) {
      setOrders((prev) =>
        prev.map((o) =>
          o.id === fromTable.currentOrderId
            ? { ...o, tableId: toTableId, tableName: toTable.name }
            : o,
        ),
      );
    }

    toast.success(`Đã chuyển phiên từ ${fromTable.name} sang ${toTable.name}. Giữ nguyên đợt món.`);
    return true;
  }, [tables]);

  // ── 3. TẠO ĐƠN & GỬI ĐỢT MÓN (UC06, UC07) ────────────────────────────
  const createOrder = useCallback(
    (
      tableId: string,
      items: { dishId: number; quantity: number; note?: string }[],
      customerName: string = "Khách hàng",
      isTakeaway: boolean = false,
    ) => {
      const orderId = `DH-${Math.floor(1000 + Math.random() * 9000)}`;
      const targetTable = tables.find((t) => t.id === tableId);
      const tableName = isTakeaway ? `Mang về #${orderId.slice(-2)}` : (targetTable?.name || tableId);

      const detailedItems: OrderItemDetail[] = items.map((it, idx) => {
        const dish = menuItems.find((d) => d.id === it.dishId);
        return {
          id: `item-${Date.now()}-${idx}`,
          dishId: it.dishId,
          name: dish?.name || "Món ăn",
          price: dish?.price || 0,
          quantity: it.quantity,
          station: dish?.station || "BEP",
          status: "CHO_LAM",
          note: it.note,
        };
      });

      const newOrder: RestaurantOrder = {
        id: orderId,
        tableId,
        tableName,
        isTakeaway,
        takeawayCode: isTakeaway ? `MV-${orderId.slice(-2)}` : undefined,
        customerName,
        status: "CHO_XAC_NHAN",
        items: detailedItems,
        payments: [],
        waivedAmount: 0,
        createdAt: new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" }),
      };

      setOrders((prev) => [newOrder, ...prev]);

      if (!isTakeaway) {
        setTables((prev) =>
          prev.map((t) => (t.id === tableId ? { ...t, status: "DANG_DUNG", currentOrderId: orderId } : t)),
        );
      }

      toast.success(`Đã tạo đơn ${orderId} (${tableName}) - Chờ nhân viên xác nhận.`);
      return orderId;
    },
    [tables, menuItems],
  );

  // ── 4. XÁC NHẬN ĐỢT GỌI MÓN (UC09) ───────────────────────────────────
  const confirmOrder = useCallback((orderId: string) => {
    setOrders((prev) =>
      prev.map((o) => {
        if (o.id !== orderId) return o;
        return {
          ...o,
          status: "DANG_PHUC_VU",
        };
      }),
    );
    toast.success(`Đã duyệt đơn ${orderId}. Các món đã gửi sang trạm Bếp & Bar!`);
  }, []);

  // ── 5. BẾP / BAR: BẮT ĐẦU CHẾ BIẾN (UC11) ───────────────────────────
  const startCookingItem = useCallback((orderId: string, itemId: string) => {
    setOrders((prev) =>
      prev.map((o) => {
        if (o.id !== orderId) return o;
        return {
          ...o,
          items: o.items.map((it) => (it.id === itemId ? { ...it, status: "DANG_LAM" } : it)),
        };
      }),
    );
    toast.info("Đã chuyển món sang trạng thái: ĐANG NẤU.");
  }, []);

  // ── 6. BẾP / BAR: HOÀN THÀNH MÓN (UC12) ───────────────────────────────
  const finishCookingItem = useCallback((orderId: string, itemId: string) => {
    setOrders((prev) =>
      prev.map((o) => {
        if (o.id !== orderId) return o;
        return {
          ...o,
          items: o.items.map((it) => (it.id === itemId ? { ...it, status: "SAN_SANG" } : it)),
        };
      }),
    );
    // Báo chuông sẵn sàng phục vụ
    toast.success("Món đã xong! SẴN SÀNG PHỤC VỤ (Phát tín hiệu cho nhân viên).");
  }, []);

  // ── 7. PHỤC VỤ TẠI BÀN: ĐÃ GIAO MÓN (UC15) ───────────────────────────
  const serveItem = useCallback((orderId: string, itemId: string) => {
    setOrders((prev) =>
      prev.map((o) => {
        if (o.id !== orderId) return o;
        const updatedItems = o.items.map((it) => (it.id === itemId ? { ...it, status: "DA_GIAO" as DishItemStatus } : it));
        const allServed = updatedItems.every((it) => it.status === "DA_GIAO" || it.status === "DA_HUY");
        return {
          ...o,
          items: updatedItems,
          status: allServed ? "CHO_THANH_TOAN" : o.status,
        };
      }),
    );
    toast.success("Đã ghi nhận giao món tới bàn.");
  }, []);

  // ── 8. THU TIỀN ĐƠN HÀNG (UC17) ───────────────────────────────────────
  const addPayment = useCallback(
    (orderId: string, amount: number, method: PaymentMethod, cashGiven?: number) => {
      const record: PaymentRecord = {
        id: `pay-${Date.now()}`,
        amount,
        method,
        cashGiven,
        changeAmount: cashGiven && cashGiven > amount ? cashGiven - amount : 0,
        paidAt: new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" }),
      };

      setOrders((prev) =>
        prev.map((o) => (o.id === orderId ? { ...o, payments: [...o.payments, record] } : o)),
      );

      toast.success(
        `Đã thu ${amount.toLocaleString("vi-VN")} đ (${method === "TIEN_MAT" ? "Tiền mặt" : "Chuyển khoản"}).` +
          (record.changeAmount ? ` Tiền thừa trả khách: ${record.changeAmount.toLocaleString("vi-VN")} đ` : ""),
      );
    },
    [],
  );

  // ── 9. MIỄN THU (UC19) ────────────────────────────────────────────────
  const waiveOrder = useCallback((orderId: string, amount: number, reason: string) => {
    setOrders((prev) =>
      prev.map((o) =>
        o.id === orderId
          ? {
              ...o,
              waivedAmount: o.waivedAmount + amount,
              waiveReason: reason,
            }
          : o,
      ),
    );
    toast.warning(`Quản lý đã duyệt miễn thu ${amount.toLocaleString("vi-VN")} đ. Lý do: ${reason}`);
  }, []);

  // ── 10. ĐÓNG ĐƠN VÀ GIẢI PHÓNG BÀN (UC20, UC03) ──────────────────────
  const closeOrder = useCallback((orderId: string) => {
    const order = orders.find((o) => o.id === orderId);
    if (!order) return false;

    // Tính toán kiểm tra điều kiện đóng theo UC20
    const totalOrderCost = order.items.reduce((sum, it) => sum + it.price * it.quantity, 0);
    const totalPaid = order.payments.reduce((sum, p) => sum + p.amount, 0);
    const balance = totalOrderCost - totalPaid - order.waivedAmount;

    if (balance > 0) {
      toast.error(`Đơn chưa thanh toán đủ (Còn thiếu ${balance.toLocaleString("vi-VN")} đ). Không thể đóng đơn!`);
      return false;
    }

    const hasPendingDishes = order.items.some(
      (it) => it.status === "CHO_LAM" || it.status === "DANG_LAM" || it.status === "SAN_SANG",
    );
    if (hasPendingDishes) {
      toast.error("Còn món chưa được phục vụ xong. Phải phục vụ hoặc hủy món trước khi đóng đơn!");
      return false;
    }

    // Đóng đơn thành công
    setOrders((prev) =>
      prev.map((o) =>
        o.id === orderId
          ? {
              ...o,
              status: "DA_DONG",
              closedAt: new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" }),
            }
          : o,
      ),
    );

    // Giải phóng bàn nếu là đơn tại bàn (UC03)
    if (!order.isTakeaway && order.tableId) {
      setTables((prev) =>
        prev.map((t) =>
          t.id === order.tableId
            ? { ...t, status: "TRONG", currentOrderId: undefined, guestCount: undefined, openedAt: undefined }
            : t,
        ),
      );
    }

    toast.success(`Đã đóng đơn ${orderId} thành công. Bàn ${order.tableName} đã chuyển về TRỐNG!`);
    return true;
  }, [orders]);

  // ── 11. ĐÁNH DẤU HẾT MÓN / MỞ BÁN LẠI (UC05) ─────────────────────────
  const toggleDishStock = useCallback((dishId: number) => {
    setMenuItems((prev) =>
      prev.map((d) => (d.id === dishId ? { ...d, outOfStock: !d.outOfStock } : d)),
    );
  }, []);

  // ── 12. MỞ NGÀY BÁN (UC25) ───────────────────────────────────────────
  const openSalesDay = useCallback((openingCash: number) => {
    setSalesDay({
      status: "DANG_BAN",
      businessDate: new Date().toISOString().split("T")[0],
      openingCash,
      openedAt: new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" }),
    });
    // Reset cờ hết món theo UC25
    setMenuItems((prev) => prev.map((d) => ({ ...d, outOfStock: false })));
    toast.success(`Đã mở ngày bán với tiền đầu ngày: ${openingCash.toLocaleString("vi-VN")} đ. Reset tất cả cờ hết món.`);
  }, []);

  // ── 13. CHỐT NGÀY BÁN (UC26) ─────────────────────────────────────────
  const closeSalesDay = useCallback(
    (actualCash: number, note?: string) => {
      // Điều kiện UC26: Không còn order mở hoặc bàn đang sử dụng
      const hasOpenOrders = orders.some((o) => o.status !== "DA_DONG" && o.status !== "DA_HUY");
      const hasActiveTables = tables.some((t) => t.status === "DANG_DUNG");

      if (hasOpenOrders || hasActiveTables) {
        toast.error("Điều kiện chặn: Vẫn còn đơn hàng mở hoặc bàn đang sử dụng! Hãy đóng tất cả trước khi chốt ngày.");
        return false;
      }

      setSalesDay((prev) => ({
        ...prev,
        status: "DA_CHOT",
        closingCash: actualCash,
        closedAt: new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" }),
      }));

      toast.success(`Chốt ngày bán thành công! Tiền két thực tế: ${actualCash.toLocaleString("vi-VN")} đ.`);
      return true;
    },
    [orders, tables],
  );

  return (
    <RestaurantContext.Provider
      value={{
        tables,
        orders,
        menuItems,
        salesDay,
        openTableSession,
        transferTable,
        createOrder,
        confirmOrder,
        startCookingItem,
        finishCookingItem,
        serveItem,
        addPayment,
        waiveOrder,
        closeOrder,
        toggleDishStock,
        openSalesDay,
        closeSalesDay,
      }}
    >
      {children}
    </RestaurantContext.Provider>
  );
}

export function useRestaurant() {
  const context = useContext(RestaurantContext);
  if (!context) {
    throw new Error("useRestaurant must be used within RestaurantProvider");
  }
  return context;
}
