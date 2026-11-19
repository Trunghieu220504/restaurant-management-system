# BAO-2.2 — kết quả kiểm tra ngày 19/11/2026

Đã triển khai màn `/menu` gọi Catalog API thật cho nhóm món, món, biến thể, nhóm tùy chọn, topping và gắn min/max. Backend dùng schema/migration Catalog hiện có, không thay đổi schema. Giữ chỗ ảnh và để cờ hết món cho tuần 3–4.

## Kết quả đã quan sát

- Restore .NET bằng locked mode thành công.
- Build toàn bộ solution Release thành công, 0 lỗi, 0 cảnh báo (.NET SDK 10.0.300 portable).
- 5 kiểm thử kiến trúc thành công.
- 52 unit test frontend thành công, gồm 4 test mới về giá, giới hạn lựa chọn, lỗi API và dữ liệu phản hồi.
- Build production Next.js thành công, bao gồm TypeScript và tạo trang `/menu`.
- TypeScript và ESLint phần Catalog đã qua; generated client sinh lại cho cùng kết quả với hợp đồng đã cập nhật.
- Cả 5 kiểm thử trình duyệt Catalog thành công: tạo đầy đủ món/biến thể/topping/gắn min-max và đọc lại qua fixture; chặn min/max sai; chỉ đọc khi thiếu quyền sửa; giữ bản nháp khi ghi nhận 403; phục hồi sau GET chậm/lỗi. Đã sửa nhãn trường gộp hint, fixture thiếu `imageObjectKey`, lựa chọn nhóm có hậu tố và selector alert trùng route announcer Next.js. Đây chưa phải bằng chứng SQL thật.

## Môi trường đang chặn nghiệm thu thật

API và integration test không chạy được do Windows Application Control từ chối tải DLL (`0x800711C7`), gồm `Restaurant.Modules.Identity.Infrastructure.dll` và `Restaurant.IntegrationTests.dll`. Không thay đổi/tắt chính sách bảo vệ Windows.

Đã tải SQL Server 2022 Express LocalDB từ Microsoft và xác minh chữ ký Authenticode hợp lệ. Cài đặt thông thường thiếu quyền quản trị. Chạy lại với UAC vẫn thất bại khi tạo dịch vụ `SQLWriter` (1923), mã kết thúc MSI 1603; thử chọn riêng LocalDB vẫn yêu cầu dịch vụ này. Vì vậy **SQL Server chưa được cài thành công**.

Chưa chạy được vòng UI → API thật → SQL → GET `/menu`. **Cột 4 vẫn chờ nghiệm thu**, không đánh dấu Done dựa trên test fixture. Cần cài SQL thành công và cho phép chạy các assembly của dự án theo chính sách máy, hoặc dùng môi trường phát triển/test đã được cho phép. Thực hiện workflow tại [hợp đồng Catalog](api/catalog-contract.md).
