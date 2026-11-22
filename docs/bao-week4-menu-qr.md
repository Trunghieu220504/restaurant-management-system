# BAO-4.2 và BAO-4.3

## Cấu hình staging

API đọc năm giá trị từ cấu hình .NET. Với biến môi trường, dùng `MenuImages__AccountId`, `MenuImages__Bucket`, `MenuImages__PublicBaseUrl`, `MenuImages__AccessKeyId` và `MenuImages__SecretAccessKey`. PublicBaseUrl là địa chỉ HTTPS công khai của bucket ảnh. Đặt hai khóa trong secret của API. Cấp token đọc/ghi chỉ cho bucket ảnh món.

CORS trên bucket cần cho phép origin của web gửi `PUT` với `Content-Type: image/webp`. Đặt quy tắc vòng đời xóa tiền tố `incoming/menu/` sau một ngày. URL PUT có hiệu lực 5 phút. API đọc lại tệp, kiểm tra kích thước và dấu hiệu WebP rồi lưu bản đã kiểm tra dưới tiền tố `menu/` theo SHA-256. Thực đơn lưu khóa của bản đã xuất bản. [Tài liệu URL ký sẵn của Cloudflare](https://developers.cloudflare.com/r2/api/s3/presigned-urls/)

Áp dụng migration `20261122000100_AddGuestInvitations` của `SeatingDbContext` trước khi chạy bản API mới. Web, API và SQL phải cùng phiên bản. Trang công khai đọc `GET /api/public/menu` và cần có ngày đang bán để thấy món.

## Nghiệm thu với thiết bị thật

1. Dùng tài khoản có `catalog.menu.manage`, mở **Thực đơn**, chọn ảnh cho món đang bán. Mở **Đơn hàng nhân viên** và đối chiếu ảnh của cùng món.
2. Mở phiên của bàn thử, chọn **Mời khách qua QR**, rồi quét bằng camera điện thoại. Kiểm tra tên bàn và ảnh món. Trang xóa mã tham gia khỏi thanh địa chỉ sau khi đọc.
3. Quét lại cùng mã bằng thiết bị khác để kiểm tra thông báo mã đã dùng. Chuyển bàn rồi đóng phiên để kiểm tra tên bàn mới và thông báo thu hồi. Trang kiểm tra phiên đang mở mỗi 20 giây. Mã tham gia hết hạn sau 10 phút và token khách tối đa 4 giờ.
4. Với ca giới hạn tốc độ, đối chiếu thời gian chờ trên màn hình với header `Retry-After`. Kiểm thử giao diện tự động dùng fixture cho phản hồi 429, quá hạn, menu và R2. Ca SQL về đổi mã đồng thời nằm trong `SeatingSqlServerTests`.
