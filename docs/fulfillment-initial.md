# Fulfillment: bảng nền (HIE-3.4, phần nền)

Migration `InitialFulfillment` tạo schema `fulfillment` theo mục "fulfillment" của
`docs/superpowers/specs/2026-11-05-database-revised-design.md`. **Chưa có consumer, chưa có máy trạng thái**:
hai phần đó là việc tiếp theo (xem cuối tài liệu).

## Bảng

| Bảng | Nội dung | Ghi chú |
|---|---|---|
| `PhieuCheBien` | Một lần chế biến của một phần món (`ChiTietDonId` là tham chiếu logic sang Sales) | `Lan` = lần thứ mấy; làm lại là phiếu mới với `Lan` kế tiếp |
| `SuCoMon` | Sự cố của một phiếu (cháy, sai món...) | Duyệt/quyết định điền sau (HIE-4.x) |
| `Outbox` | Sự kiện module Fulfillment phát ra | Cùng hình dạng với `sales.Outbox`; **chưa có writer** (xem `docs/outbox-inbox.md`) |
| `Inbox` | Dấu "consumer này đã xử lý sự kiện này" | Khóa (`Consumer`, `EventId`), ghi bằng `InboxStore.TryRecordAsync` trong transaction của consumer |

Không có khóa ngoại sang module khác. Ba khóa nội bộ (phiếu gốc, sự cố nguồn, phiếu của sự cố) đều `NO ACTION`.
Thứ tự chèn: phiếu đầu tiên, rồi sự cố, rồi phiếu làm lại, nên không có vòng chèn.

## Quy tắc do CSDL giữ (CHECK và chỉ mục duy nhất)

- `UQ(ChiTietDonId, Lan)`: một phần món chỉ có một phiếu cho mỗi lần chế biến. Đây là chốt chặn cuối cho yêu cầu
  HIE-3.4 "sự kiện giao hai lần chỉ tạo một phiếu".
- Lần 1 không có phiếu gốc và không có sự cố; lần > 1 phải có cả hai (làm lại cần một sự cố đã duyệt).
- `UQ(SuCoNguonId)` (lọc): một sự cố chỉ cho một lần làm lại.
- `UQ(PhieuGocId)` (lọc): một phiếu chỉ bị làm lại một lần, nên các lần chế biến là một chuỗi thẳng, không rẽ nhánh.
- Phiếu `MOI` chưa có thao tác nào; `DANG_LAM` có `BatDauLuc`; `SAN_SANG` có `HoanThanhLuc`; `DA_GIAO` có cả `HoanThanhLuc`
  và `GiaoLuc`. Người thao tác và thời điểm luôn đi theo cặp.
- Sự cố `CHO_DUYET` chưa có quyết định/người duyệt; `DA_DUYET` có `QuyetDinh`, người và thời điểm duyệt;
  `TU_CHOI` có người và thời điểm duyệt nhưng không có `QuyetDinh`. `LyDo` không được rỗng; `KhoaYeuCau` duy nhất.

## Khác với thiết kế CSDL đã duyệt (cần người duyệt xác nhận)

1. Thêm cột `PhieuCheBien.TaoLuc` để hàng đợi của bếp/bar sắp theo thời điểm vào (thiết kế không có cột này).
2. Thêm `UQ(PhieuGocId)` lọc (chuỗi làm lại thẳng); thiết kế chỉ yêu cầu `UQ(SuCoNguonId)` lọc.
3. Thêm các CHECK về tiến độ/cặp người-thời điểm ở trên; thiết kế mô tả quy tắc nhưng không nêu ràng buộc cụ thể.

## Chưa làm (cố ý)

- **Consumer "đợt đã xác nhận"** (phần còn lại của HIE-3.4): cần định nghĩa sự kiện do HIE-3.1 (Sales) phát, hiện chưa có.
  Khi có, consumer dùng `InboxStore` trên bảng `Inbox` này và `PhieuCheBien.CreateInitial`.
- **Máy trạng thái phiếu và quy tắc theo trạm** (HIE-4.1). CSDL chỉ chặn tổ hợp cột sai, không chặn chuyển trạng thái sai
  (ví dụ `DA_GIAO` về `MOI`).
- **Quy tắc chéo hàng** mà CHECK không diễn đạt được: phiếu làm lại phải cùng `ChiTietDonId` với phiếu gốc và có `Lan` = gốc + 1;
  phiếu của lần cũ chưa được làm lại thì không được giao. Thuộc tầng ứng dụng của HIE-4.x, trong transaction.
- `Outbox` của Fulfillment chưa có writer: một DI container chỉ resolve được một `IOutboxWriter` (xem `docs/outbox-inbox.md`).
