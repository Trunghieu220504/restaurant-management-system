# ADR-02: Cấp và kiểm tra quyền truy cập của khách qua mã QR theo phiên

- Ngày: 18/11/2026. Chủ trì: Hiếu. Task: HIE-2.4.
- Trạng thái: **Đề xuất, chờ Phương nhận xét và chủ repo duyệt (quyết định D-7, hạn 20/11/2026).**
- Mốc phân tích: `develop` tại 68cd462.
- Phạm vi: quyết định thiết kế và mã lỗi. Chưa có code. Phần cài đặt nằm ở HIE-4.4 (backend QR phần 1) và
  BAO-3.3 (khung trang khách). Các con số (thời hạn, giới hạn tốc độ) là **đề xuất khởi điểm**, đặt trong
  cấu hình và chốt khi duyệt.

## Bối cảnh

Khách tự gọi món bằng điện thoại, không có tài khoản. Thiết kế dữ liệu
(`docs/superpowers/specs/2026-11-05-database-revised-design.md`, mục seating) đã cố định:

- `Ban.MaQrCongKhai` là mã in sẵn trên bàn, duy nhất. Thiết kế ghi rõ đây là **mã định vị, không phải quyền
  vào một phiên bất kỳ**.
- `QuyenTruyCapKhach` gắn với `PhienSuDung`: `TokenHash` binary(32) duy nhất, `TaoLuc`, `HetHanLuc`,
  `ThuHoiLuc`. Nhiều thiết bị có thể cùng tham gia một phiên.
- Chuyển bàn giữ nguyên token trong cùng phiên (đề xuất C05). Đóng phiên thu hồi mọi token.
- Không lưu token thô lâu dài (mục `YeuCauDaXuLy`).
- Test 4 của ma trận nghiệm thu: mã định vị một mình không đặt được; quyền hết hạn, bị thu hồi hoặc khác phiên
  bị từ chối.

Hiện chưa có code QR nào trên `develop`: `SeatingEndpoints` mới chỉ có nhóm route, và Seating chưa có bảng
`YeuCauDaXuLy`. Bảo Anh cần ADR này (19/11) trước khi dựng trang khách.

## Mô hình đe dọa

Kẻ tấn công có thể: chụp ảnh mã trên bàn rồi dùng sau khi rời quán; ngồi ở bàn khác; nhặt mã in cũ; đoán mã;
gửi mã cho người ngoài quán; gửi hàng loạt yêu cầu để đoán hoặc làm quá tải.

| Mã | Tình huống | Hậu quả nếu không chặn | Cách chặn |
|---|---|---|---|
| T1 | Có mã bàn, không có phiên đang mở | Đặt món vào bàn trống | Mã bàn không cấp quyền đặt; đổi token cần mã tham gia của một phiên đang mở |
| T2 | Có mã bàn, phiên đang mở nhưng chưa được nhân viên cấp mã tham gia (chụp ảnh mã bàn rồi quay lại sau) | Người ngoài đặt vào bàn của người khác | Đổi token bắt buộc mã tham gia do nhân viên cấp, đi kèm mã bàn |
| T3 | Token cũ sau khi phiên đóng | Đặt món vào phiên đã thanh toán | Đóng phiên thu hồi mọi token và mã tham gia trong cùng giao dịch; kiểm tra lại trong giao dịch đặt món |
| T4 | Token của phiên A dùng cho đơn hoặc phiên B | Đọc, sửa đơn người khác | Phiên lấy từ token ở máy chủ, không lấy từ body; đơn khác phiên bị từ chối |
| T5 | Đoán mã tham gia hoặc token | Chiếm quyền một phiên | Giá trị ngẫu nhiên từ CSPRNG, không đoán được (128 bit trở lên); giới hạn tốc độ |
| T6 | Chia sẻ mã tham gia cho người ngoài quán | Người ngoài vào phiên | Mã dùng một lần, hết hạn nhanh; nhân viên thấy và thu hồi được; thu hồi cả phiên khi đóng |
| T7 | Mã bàn cũ sau khi chuyển bàn hoặc bàn được đổi mã | Khách bàn mới vào nhầm phiên cũ | Đổi token kiểm tra mã bàn khớp với **bàn hiện tại** của phiên; có thể đổi `MaQrCongKhai` |
| T8 | Rò token qua log, URL, Referer | Lộ quyền truy cập | Không log; token đi trong header, không nằm trong query; DB chỉ lưu băm |
| T9 | Gọi API nhân viên bằng token khách | Leo thang quyền | Token khách là scheme xác thực riêng, chỉ được các endpoint công khai chấp nhận |
| T10 | Tin giá và nội dung từ khách | Đặt món giá sai | Khách gửi mã món và lựa chọn, không gửi giá; giá chốt lại khi nhân viên xác nhận đợt gọi |

## Quyết định

### 1. Ba loại thông tin, ba mức quyền

| Thông tin | Ai cấp | Quyền | Lưu |
|---|---|---|---|
| Mã bàn (`MaQrCongKhai`) | In sẵn, quản lý đổi được | Chỉ định vị bàn và xem menu công khai chỉ đọc. Không đặt món, không xem phiên | `Ban.MaQrCongKhai` (đã có) |
| Mã tham gia phiên | Nhân viên, qua `POST /api/seating/sessions/{id}/guest-access` (`seating.sessions.manage`) | Một lần dùng để đổi lấy token khách cho phiên đó | Bảng mới, chỉ lưu băm (xem Hệ quả) |
| Token khách | Máy chủ, khi đổi mã tham gia hợp lệ | Gọi món và xem đơn trong **một phiên** | `QuyenTruyCapKhach.TokenHash` (đã có) |

Mã bàn lộ ra là chấp nhận được vì nó không đủ để đặt món (T1, T2).

### 2. Luồng

1. Nhân viên mở phiên rồi chọn "Mời khách gọi món". Máy chủ tạo mã tham gia và trả về một lần (mã thô cùng
   thời hạn) để hiển thị dạng QR.
2. Khách quét QR. Trang khách gọi `POST /api/public/guest-access/exchange` với `{ tableCode, joinCode }`.
3. Máy chủ băm `joinCode`, tìm mã tham gia chưa dùng, chưa hết hạn, chưa thu hồi; phiên chưa đóng; `tableCode`
   khớp `MaQrCongKhai` của **bàn hiện tại** của phiên. Trong một giao dịch, đánh dấu mã đã dùng và tạo
   `QuyenTruyCapKhach`.
4. Trả `{ token, expiresAt }` một lần. Token thô không được lưu lại, không có endpoint lấy lại.
5. Khách gửi token ở header `Authorization: Guest <token>` cho mọi lời gọi sau. Xem menu công khai chỉ cần mã
   bàn (hoặc không cần) vì không gắn phiên.

Nhiều thiết bị cùng bàn: nhân viên cấp thêm mã tham gia (mỗi thiết bị một mã). Mã dùng chung chỉ đúng một lần
nên mã bị chuyển tiếp cho người ngoài thường đã bị dùng (T6).

### 3. Định dạng và lưu trữ

- Mã tham gia và token: 256 bit ngẫu nhiên từ CSPRNG, mã hóa base64url. Lưu `SHA-256` (32 byte, khớp
  `binary(32)`), tra cứu theo chỉ mục duy nhất. Giá trị đã đủ entropy nên không cần băm chậm hay muối.
- Mã thô chỉ xuất hiện trong phản hồi tạo, không vào log, ProblemDetails, audit hay `YeuCauDaXuLy`.
- QR của nhân viên mã hóa `https://<host>/guest/<tableCode>#j=<joinCode>`. Mã tham gia nằm trong fragment
  nên không gửi tới máy chủ qua URL và không vào Referer. Trang khách xóa fragment sau khi đọc.
- Đề xuất thời hạn: mã tham gia 10 phút; token đến lúc phiên đóng nhưng tối đa 4 giờ kể từ lúc cấp (`HetHanLuc`
  tuyệt đối, không gia hạn trượt). Hết hạn thì nhân viên cấp lại.

### 4. Kiểm tra token trên mọi lời gọi của khách

Một handler xác thực `Guest` ở Host gọi hợp đồng của Seating (một phương thức tra theo băm token, trả
`PhienSuDungId`, `BanHienTaiId`, hoặc lý do từ chối). Module khác chỉ thấy hợp đồng, không thấy bảng Seating.

Từ chối khi: không có token; không tìm thấy băm; `ThuHoiLuc` khác NULL; `HetHanLuc` đã qua; phiên có `DongLuc`.

`PhienSuDungId` luôn lấy từ token. Mọi id của đơn hay đợt trong request phải thuộc phiên đó, nếu không trả
`qr.session.mismatch` (T4). Với thao tác ghi, Sales kiểm tra lại token **sau khi** lấy khóa theo thứ tự đã chốt
(ngày, rồi đơn hoặc phiên). Nhờ đó đợt gọi không chen vào được sau khi phiên đóng.

### 5. Quyền của khách

Được phép: xem menu công khai; tạo đợt gọi món cho đơn của phiên mình (nguồn `KHACH`, chờ nhân viên xác nhận
bằng `sales.batches.confirm`); xem trạng thái đơn của phiên mình; hủy đợt chưa xác nhận của chính phiên.

Không được: xác nhận đợt, thanh toán, hoàn tiền, xem hay sửa phiên khác, gọi bất kỳ route nhân viên nào (T9),
gửi giá (T10).

### 6. Vòng đời

| Sự kiện | Mã tham gia | Token khách |
|---|---|---|
| Đổi thành công | Đánh dấu đã dùng | Tạo mới |
| Hết hạn | Bị từ chối | Bị từ chối, nhân viên cấp lại |
| Đóng phiên | Thu hồi tất cả | Thu hồi tất cả (cùng giao dịch với đóng phiên) |
| Chuyển bàn (C05) | Giữ nguyên, gắn theo phiên | **Giữ nguyên hiệu lực**, vẫn gắn cùng phiên. Mã bàn cũ không còn khớp bàn hiện tại nên không dùng để đổi thêm (T7) |
| Nhân viên thu hồi | Thu hồi một hoặc tất cả | Thu hồi một hoặc tất cả |
| Đổi `MaQrCongKhai` | Không ảnh hưởng mã đã cấp | Không ảnh hưởng token đã cấp |

### 7. Giới hạn tốc độ

Dùng bộ giới hạn có sẵn của ASP.NET (cùng cách `identity-login`). Khởi điểm:

- `qr-exchange`: 10 lần mỗi phút theo IP máy khách (sau proxy tin cậy), kèm 30 lần mỗi phút theo `tableCode`.
  Chỉ giới hạn, không khóa bàn, để kẻ gửi bậy không làm bàn bị khóa với khách thật.
- `qr-guest`: 60 lần mỗi phút theo token cho các lời gọi của khách.
- Vượt giới hạn trả 429 `qr.rate.limited` kèm `Retry-After`.

Bộ giới hạn trong bộ nhớ chỉ đúng với một instance. Nếu triển khai nhiều instance cần lưu trữ chung. Việc này
không chặn MVP vì entropy 256 bit đã chặn brute force độc lập với giới hạn tốc độ.

### 8. Mã lỗi

| Mã | HTTP | Khi nào |
|---|---|---|
| `qr.code.invalid` | 400 | Mã tham gia hoặc mã bàn sai, không tồn tại, không khớp bàn hiện tại, phiên đã đóng. Một mã chung để không lộ tồn tại |
| `qr.code.expired` | 400 | Mã tham gia đúng nhưng hết hạn |
| `qr.code.used` | 409 | Mã tham gia đã được đổi |
| `qr.token.missing` | 401 | Thiếu token |
| `qr.token.invalid` | 401 | Token không nhận ra |
| `qr.token.expired` | 401 | Token hết hạn |
| `qr.token.revoked` | 401 | Token bị thu hồi, gồm cả do đóng phiên |
| `qr.session.mismatch` | 403 | Đơn hoặc đợt không thuộc phiên của token |
| `qr.rate.limited` | 429 | Vượt giới hạn |

Mã theo quy tắc `<module>.<subject>.<reason>` của ADR-01. Việc nhân viên cấp mã cho phiên đã đóng trả mã
`seating.session.closed`, vì lỗi thuộc Seating. Trả lời đều là ProblemDetails có `code`.

## Phương án đã cân nhắc

1. **Mã bàn đủ để đặt khi bàn đang có phiên.** Đơn giản nhất nhưng vi phạm thiết kế (mã bàn chỉ là định vị) và
   không chặn T2. Loại.
2. **Một bước: nhân viên cấp thẳng token khách, khách dùng ngay.** Ít bảng hơn, nhưng token nằm trong URL
   của QR, dễ rò, và khó cấp riêng cho từng thiết bị. Không khớp tên endpoint "đổi mã lấy token" trong danh mục
   API. Loại.
3. **Hai bước: mã bàn cộng mã tham gia dùng một lần, đổi lấy token (chọn).** Chặn T1, T2, T6, T7 với một bảng
   nhỏ.
4. **Cookie hoặc JWT ký.** Không thu hồi được tức thời khi đóng phiên nếu không có kho trạng thái. Loại.

## Hệ quả

- **Cần bảng lưu mã tham gia**, vì `QuyenTruyCapKhach` chỉ mô tả token. Đề xuất bảng `MaThamGiaPhien`
  (`Id`, `PhienSuDungId` FK, `MaHash` binary(32) duy nhất, `TaoLuc`, `HetHanLuc`, `DaDungLuc?`, `ThuHoiLuc?`,
  `NguoiCapId`). Đây là thay đổi so với thiết kế dữ liệu hiện hành, cần chủ repo duyệt cùng ADR và làm bằng một
  migration của Seating trong HIE-4.4.
- Đổi mã tham gia và tạo token chạy trong một giao dịch; mã thô không lưu nên đổi mà mất phản hồi thì mã đã bị
  dùng, nhân viên cấp lại. Chấp nhận để không phải lưu token thô.
- Cần hợp đồng Seating cho tra token và đóng phiên thu hồi; phối hợp với contracts của HIE-2.3.
- Test 3 và test 4 của ma trận nghiệm thu phụ thuộc ADR này.

## Kiểm thử dự kiến (gắn test 4)

| Mã | Tình huống | Kết quả mong đợi | Nhóm |
|---|---|---|---|
| QR-01 | Đổi token bằng mã bàn, không có mã tham gia | 400 `qr.code.invalid` | Test 4 |
| QR-02 | Có mã bàn, phiên mở, chưa ai cấp mã tham gia | Không có token, không đặt được (T2) | Test 4 |
| QR-03 | Mã tham gia hợp lệ nhưng mã bàn sai bàn | 400 `qr.code.invalid` | Test 4 |
| QR-04 | Mã tham gia đã dùng, đổi lần hai | 409 `qr.code.used`, không tạo token thứ hai | Test 4 |
| QR-05 | Mã tham gia quá 10 phút | 400 `qr.code.expired` | Test 4 |
| QR-06 | Token quá `HetHanLuc` | 401 `qr.token.expired` | Test 4 |
| QR-07 | Token sau khi nhân viên thu hồi | 401 `qr.token.revoked` | Test 4 |
| QR-08 | Đóng phiên rồi dùng token | 401 `qr.token.revoked`; mọi mã tham gia chưa dùng cũng bị thu hồi | Test 3 và 4 |
| QR-09 | Token phiên A gửi tới đơn của phiên B | 403 `qr.session.mismatch` | Test 4 |
| QR-10 | Chuyển bàn, token cũ vẫn gọi được cùng phiên; mã bàn cũ không đổi thêm token | Hợp lệ; `qr.code.invalid` (C05) | Test 4 |
| QR-11 | Token khách gọi route nhân viên | 401 hoặc 403, không lộ dữ liệu | Test 4 |
| QR-12 | Đặt món đồng thời với đóng phiên | Đợt không được thêm sau khi phiên đóng | Test 3 và 4 |
| QR-13 | Quá giới hạn đổi mã | 429 `qr.rate.limited` có `Retry-After` | Test 4 |
| QR-14 | Quét log, audit và `YeuCauDaXuLy` sau luồng | Không chứa mã hay token thô | Test 4 |
| QR-15 | Hai thiết bị đổi hai mã khác nhau của một phiên | Cả hai có token riêng, cùng `PhienSuDungId` | Test 4 |

## Câu hỏi cần chủ repo và Phương trả lời

1. Duyệt thêm bảng `MaThamGiaPhien` (thay đổi so với thiết kế dữ liệu hiện hành) hay yêu cầu thiết kế khác?
2. Mã tham gia dùng một lần có phù hợp vận hành không, hay cho phép dùng nhiều lần trong thời hạn?
3. Chốt thời hạn: mã 10 phút, token tối đa 4 giờ.
4. Xác nhận C05 (token còn hiệu lực sau chuyển bàn) vì QR-10 dựa vào đó.
5. Mã bàn công khai có được trả về thông tin bàn đang trống hay có khách không? Đề xuất: không, chỉ trả menu.
