# Báo cáo tuần 6 của Bảo Anh

Nhánh `feature/bao-week6-reports-accounts` dựa trên `develop@26c1054`. Hai màn mới dùng API Reporting của Phương và API Identity của Hiếu. Các thay đổi thuộc frontend.

| Task | Tệp chính | Kết quả |
| --- | --- | --- |
| BAO-6.1 | `web/src/app/(dashboard)/reports/page.tsx`, `web/src/components/auth/auth-provider.tsx` | Hai cột riêng cho doanh thu và dòng tiền. Chọn cùng ngày gọi daily, khác ngày gọi range. Chỉ hiện lối vào báo cáo và nội dung khi người dùng có cả vai trò Manager và quyền reporting.read. |
| BAO-6.2 | `web/src/app/(dashboard)/staff/page.tsx` | Danh sách có tìm kiếm, phân trang, tạo tài khoản, đổi nhiều vai trò, vô hiệu hóa, mở lại và đặt lại mật khẩu. Manager chỉ có thể chọn Staff, Kitchen, Bar. Thao tác nhạy cảm cần xác nhận trên form. |
| BAO-6.3 | `web/src/features/management/`, trang chủ, AppShell, AppProviders và route orders | Hai màn mới có trạng thái tải, rỗng, lỗi có thể thử lại và diễn giải mã lỗi bằng tiếng Việt. Trang chủ dùng các lối tắt lọc theo quyền. Đã bỏ provider dữ liệu mẫu khỏi layout chung. Chỉ đường dẫn phục vụ/thanh toán tải module cũ theo nhu cầu. |

## Bằng chứng

- Build Next.js production và TypeScript đã đạt. Vitest đạt 71/71, Playwright production đạt 35/35 trên nhánh tuần 6. Bộ test UI dùng API mô phỏng theo hợp đồng.
- Năm bài mới trong `web/e2e-production/week-six.spec.ts` kiểm tra lọc khoảng ngày, quyền nhân viên, quản trị tài khoản và đăng nhập lại sau đổi mật khẩu, trạng thái đang tải/lỗi/rỗng, và báo lỗi tên đăng nhập trùng.
- Kiểm tra chuyển trang qua mục Thêm, mở đường dẫn trực tiếp, quay lại mục Thêm, đăng xuất rồi đăng nhập. Đã chụp và xem cả hai màn ở 1366 px và 360 px. Các thẻ báo cáo xếp cạnh nhau trên desktop và xếp dọc trên mobile. Chiều rộng tài liệu vừa viewport.
- Đã kiểm tra báo cáo TRX của Backend CI trên chính commit develop dùng làm nền. Test SQL Server tạo tài khoản rồi đăng nhập, giới hạn vai trò cấp dưới, đổi vai trò và đặt lại mật khẩu đều đạt. Máy cục bộ chưa có SQL Server để chạy cả UI và API thật cùng lúc. [Backend CI](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/45347219452)

## Điều kiện còn mở

Màn phục vụ và thanh toán của Duy vẫn đọc và ghi đơn mẫu qua `restaurant-context.tsx`. Bản này cô lập phụ thuộc trong `web/src/features/orders/service-view.tsx`. Tiêu chí xóa hẳn import dữ liệu giả của BAO-6.3 còn mở. Cần phối hợp chuyển màn của Duy sang nguồn dữ liệu thật hoặc thống nhất gỡ màn mẫu khỏi luồng sử dụng. Chưa đẩy nhánh tuần 6 lên GitHub.
