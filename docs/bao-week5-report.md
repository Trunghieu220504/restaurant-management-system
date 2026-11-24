# Báo cáo tuần 5 của Bảo Anh

Nhánh `feature/bao-week5-guest-accessibility` tiếp nối bản tuần 4 ở commit `48f9941`. Phạm vi gồm giao diện QR của khách và trải nghiệm truy cập màn hình nhân viên.

## Cập nhật GitHub ngày 24/11/2026

Đã gộp `origin/develop` tại `26c1054` vào nhánh tuần 5 tại merge commit `52293d9`. Lịch sử có cả PR #93 của Bảo Anh và commit tuần 5 `d739f2d`. Git tự gộp phần điều hướng dùng chung trong `app-shell.tsx`.

| Code của Hiếu | Phạm vi đã có trên develop | Tác động đến QR |
| --- | --- | --- |
| [PR #99](https://github.com/Trunghieu220504/restaurant-management-system/pull/99) | Tạo đơn, thêm đợt. Sales nhận một dòng cho mỗi phần món. | Đã chỉnh dữ liệu phía khách theo `BatchLine`: chọn hai phần sẽ gửi hai dòng, giữ nguyên món, biến thể, tùy chọn và ghi chú. |
| [PR #100](https://github.com/Trunghieu220504/restaurant-management-system/pull/100) | Nhân viên xác nhận, từ chối và hủy đợt. | Đã bổ sung nhãn tiếng Việt cho `DA_TU_CHOI` và `DA_HUY`, dừng hỏi trạng thái khi đợt đã kết thúc. |

Đối chiếu `SalesEndpoints.cs`, `OrderModels.cs`, `SalesRules.cs` và `SeatingEndpoints.cs`: thêm đợt qua Sales yêu cầu danh tính nhân viên với quyền `sales.orders.manage`, kể cả khi nguồn ghi nhận là `KHACH`. Hai API guest-access hiện có là đổi mã và xác minh phiên. Việc gửi và đọc trạng thái bằng token Guest vẫn phụ thuộc HIE-5.1. Client đã xử lý thêm HTTP 400 với mã `sales.batch.line_invalid` để cập nhật lại thực đơn khi Sales từ chối dòng món.

## BAO-5.1 · Trang QR cho khách

| Tệp | Thay đổi và lý do |
| --- | --- |
| `web/src/features/guest/guest-access.tsx` | Chuyển token trong bộ nhớ sang phần gọi món sau khi kiểm tra phiên và máy chủ báo hỗ trợ đặt món. Ngừng cho gửi khi xác minh phiên lỗi hoặc token hết hạn. QR chỉ có mã bàn vẫn xem được thực đơn. |
| `web/src/features/guest/guest-menu.tsx` | Cho chọn tùy chọn theo biến thể, kiểm tra giới hạn của từng nhóm và cộng phụ phí vào tổng tiền. Thêm tên truy cập cho nút tăng, giảm và ô ghi chú. Khi thực đơn thay đổi, khách có thể bỏ món đã hết khỏi giỏ. |
| `web/src/features/guest/guest-checkout.tsx` | Gửi giỏ bằng token khách, khóa thao tác trong lúc gửi và dùng lại khóa chống trùng khi gửi lại cùng nội dung. Hiện trạng thái của đợt vừa gửi; tạm dừng theo Retry-After khi gặp 429, yêu cầu quét lại khi mất quyền, tải lại thực đơn khi gặp lỗi dòng món 400 hoặc 409/422. |

**Phụ thuộc HIE-5.1.** API trong nhánh tuần 4 đã có thực đơn công khai, đổi mã và xác minh phiên. OpenAPI của nhánh này chưa công bố thao tác gửi và theo dõi đợt của khách. Giao diện và test hiện dùng hợp đồng đề xuất dưới đây. Hiếu cần xác nhận hoặc cung cấp hợp đồng chính thức để đối chiếu trước khi tích hợp trên staging.

**Đối chiếu GitHub ngày 23/11/2026.** Đã kiểm tra danh sách nhánh của repo nhóm, các PR và mã đăng ký endpoint. Trên `main` tại `a8622de` đã có API thực đơn công khai. Trên `develop` tại `172280b` chưa tìm thấy endpoint để khách gửi đợt hoặc xem trạng thái. [PR #61 của Hiếu](https://github.com/Trunghieu220504/restaurant-management-system/pull/61) mô tả thiết kế QR. [PR #93 của Bảo Anh](https://github.com/Trunghieu220504/restaurant-management-system/pull/93) ở `48f9941` có API đổi mã và xác minh phiên. Chưa tìm thấy triển khai HIE-5.1 cho hai thao tác còn thiếu trên các nhánh đã đẩy. Với phiên do PR #93 cấp, khách xem được menu và lập giỏ; nút gửi tiếp tục khóa.

- Phản hồi xác minh phiên có `canSubmitOrders: true` khi máy chủ sẵn sàng nhận món. Trường thiếu hoặc bằng false giữ giỏ ở chế độ xem trước.
- `POST /api/public/guest-access/batches` nhận `{lines: [{menuItemId, variantId, note, optionIds}]}`, dùng header `Authorization: Guest <token>` và UUID trong `Idempotency-Key`. Phản hồi thành công gồm `id` và `status`.
- `GET /api/public/guest-access/batches/{id}` trả mã và trạng thái của đợt thuộc khách đang truy cập. Máy chủ cần kiểm quyền sở hữu, kiểm giá và tồn tại thời điểm nhận đợt, đồng thời bảo đảm tính chống trùng. Trình duyệt làm mới trạng thái mỗi 5 giây khi API hoạt động bình thường.
- Giỏ và biên nhận hiện nằm trong bộ nhớ của trang. Tải lại trang sẽ mất giỏ và trạng thái đang theo dõi.

## BAO-5.2 · Phân quyền và khả năng truy cập

| Tệp | Thay đổi và lý do |
| --- | --- |
| `web/src/components/app-shell.tsx` | Thêm liên kết đầu trang “Đến nội dung chính”, đích focus cho phím Tab/Enter và trạng thái đóng/mở có thể đọc được cho nút menu. Tăng độ đậm của nhãn nhóm điều hướng. Giữ cơ chế lọc danh mục và chặn URL theo quyền hiện có. |
| `web/src/components/auth/auth-guard.tsx` | Hiện thông báo và liên kết đăng nhập trong lúc chuyển trang sau khi mất phiên, giúp người dùng biết bước tiếp theo. |
| `web/e2e-production/week-five.spec.ts` | Bổ sung các tình huống gửi lại sau 429 với cùng khóa, nhận trạng thái, món hết sau khi chọn, điều hướng bàn phím, lỗi 403/500 và thử lại sau khi API phục hồi. |

## Kết quả kiểm tra

- Build production Next.js và kiểm tra TypeScript đạt. ESLint trên các component đã sửa đạt.
- Playwright Chromium chạy toàn bộ `e2e-production` trên bản production, **35/35 đạt**. Các bài UI dùng API mô phỏng. Bài QR gửi và theo dõi chạy ở 360 × 740 px, kiểm tra chiều rộng trang không tràn.
- Lighthouse mobile trên trang `/guest/T01`, production cục bộ với ba món mẫu, không có ảnh món, đạt **92/100 hiệu năng** và **100/100 khả năng truy cập** trong lần đo. Ngưỡng giao việc lần lượt là 80 và 90. Điểm này áp dụng cho trang QR đã đo. Kết quả với ảnh R2, thiết bị, mạng và API staging cần được đo riêng.

## Danh sách nghiệm thu thủ công

Cột bằng chứng ghi kết quả đã thực hiện. Các kiểm tra Playwright mô phỏng phím bấm và HTTP; phần đánh giá trực tiếp bằng người dùng và điện thoại thật còn chờ.

| Thao tác | Kết quả mong đợi | Bằng chứng hiện có |
| --- | --- | --- |
| Mở màn hình nhân viên khi hết phiên | Thấy trạng thái chuyển trang rồi biểu mẫu đăng nhập | Test đăng nhập và 401 đạt |
| Đăng nhập tài khoản thiếu quyền, thử mở URL quản trị trực tiếp | Mục tương ứng bị ẩn; URL hiển thị thông báo thiếu quyền | Test phân quyền đạt |
| Trên màn Thực đơn, gây lỗi 403 rồi 500 | Nội dung lỗi và nút thử lại vẫn đọc được | Test cả hai mã lỗi đạt |
| Nhấn Tab rồi Enter từ đầu màn Thực đơn; thử lại khi API hoạt động | Focus đến nội dung chính; thông báo lỗi biến mất sau khi tải lại | Test bàn phím và phục hồi đạt |
| Đọc các nhãn, lần theo viền focus và thao tác điều hướng chỉ bằng bàn phím trên các màn nhân viên dùng tại quán | Người dùng đọc rõ nhãn và nhận ra vị trí focus trong suốt thao tác | Chờ người dùng đánh giá trực tiếp |
| Dùng điện thoại thật quét mã của một bàn đang mở, chọn món kèm tùy chọn, gửi rồi nhờ nhân viên xác nhận | Giỏ tính đúng giá, chỉ tạo một đợt, điện thoại hiện trạng thái mới của chính khách | Đạt trên Chromium với API mô phỏng; chờ HIE-5.1 và điện thoại thật |
| Sau khi chọn món, đánh dấu món hết hoặc đóng phiên bàn | Khách bỏ được món hết, thấy thông báo khi mất phiên | Test UI đạt; chờ đối chiếu staging |
