type CatalogEntry = {
    id: string;
    code: string;
    name: string;
    version?: string;
};

export type SeatingArea = CatalogEntry;

export type SeatingSession = {
    id: string;
    openedAt: string;
    version?: string;
};

export type SeatingTable = CatalogEntry & {
    areaId: string;
    session: SeatingSession | null;
};

export type SeatingBoard = {
    areas: readonly SeatingArea[];
    tables: readonly SeatingTable[];
};

type CatalogInput = { code: string; name: string };
type EntityRef = { id: string; version?: string };

export type SeatingChange =
    | { kind: "saveArea"; input: CatalogInput; existing?: EntityRef }
    | {
          kind: "saveTable";
          input: CatalogInput & { areaId: string };
          existing?: EntityRef;
      }
    | { kind: "archiveArea" | "archiveTable"; existing: EntityRef }
    | { kind: "openSession"; tableId: string }
    | { kind: "closeSession"; session: EntityRef }
    | {
          kind: "transferSession";
          session: EntityRef;
          targetTableId: string;
          reason: string;
      };

// Shared by the authenticated API and the development preview.
export interface SeatingGateway {
    load(signal: AbortSignal): Promise<SeatingBoard>;
    apply(change: SeatingChange, key: string): Promise<void>;
}

// The gateway maps backend problem codes to these UI reasons.
const messages = {
    invalidResponse: "Dữ liệu bàn trả về không hợp lệ.",
    signedOut: "Phiên đăng nhập đã hết hạn.",
    forbidden: "Tài khoản chưa có quyền thực hiện thao tác này.",
    openOrders: "Cần đóng các order của bàn trước khi đóng phiên.",
    dayClosed: "Cần mở ngày bán trước khi thao tác phiên bàn.",
    unavailable: "Không thể kết nối dịch vụ bàn. Vui lòng tải lại.",
    duplicateCode: "Mã đã được sử dụng. Vui lòng nhập mã khác.",
    invalidInput: "Kiểm tra lại các trường bắt buộc và độ dài nội dung.",
    areaHasTables: "Hãy chuyển hoặc lưu trữ các bàn trong khu vực trước.",
    tableOccupied: "Bàn đang dùng. Hãy chọn bàn trống hoặc tải lại sơ đồ.",
    stale: "Dữ liệu đã thay đổi. Hãy đóng biểu mẫu và tải lại sơ đồ.",
} as const;

export class SeatingFailure extends Error {
    constructor(readonly reason: keyof typeof messages) {
        super(messages[reason]);
    }
}

export function describeSeatingFailure(error: unknown) {
    return error instanceof SeatingFailure
        ? error.message
        : "Không thể xử lý yêu cầu. Vui lòng thử lại.";
}
