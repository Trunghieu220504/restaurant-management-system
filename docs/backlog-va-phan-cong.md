# Backlog và phân công dự án quản lý nhà hàng

> Cập nhật 2026-11-01: kho chuyển sang kiểm kê theo ngày bán (nhập nguyên liệu và giá nhập trong ngày, đếm tồn cuối khi chốt ngày bán, tính lượng dùng, giá vốn, lợi nhuận); bỏ công thức/định lượng và trừ kho theo món. Lưu file chuyển từ MinIO sang Cloudflare R2. "Ca" của cửa hàng đổi thành "Ngày bán"; thêm miễn thu, chuyển bàn, hết món đa cấp và chốt ngày bán an toàn (xem spec nghiệp vụ). Cập nhật 2026-11-02: bỏ chấm công nhân viên (BL25 bị bỏ, các ID khác giữ nguyên); module Workforce đổi tên thành SalesDay; nhóm topping/tùy chọn dùng chung nhiều món.

## 1. Phân công nhóm

TV1–TV5 là vai trò phát triển, chưa gắn tên thành viên. Mỗi chủ module phụ trách API, giao diện, migration và kiểm thử của module mình.

| Thành viên | Vai trò | Phạm vi |
| --- | --- | --- |
| TV1 | Tech Lead, Backend nền tảng và DevOps | Identity, Audit, SalesDay; CI/CD, Docker, Nginx, Cloudflare R2; kiến trúc và tích hợp |
| TV2 | Full-stack Order và Bàn | Seating, Sales; giao diện nhân viên và khách đặt món |
| TV3 | Full-stack Thực đơn và Kho | Catalog, Inventory; nhập nguyên liệu, đếm tồn cuối, lượng dùng, giá vốn |
| TV4 | Frontend Lead và Full-stack Bếp/Bar | Khung Next.js, UI dùng chung; Fulfillment, phục vụ và SignalR |
| TV5 | Full-stack Thanh toán và Báo cáo | Billing, Reporting; thu tiền, hoàn tiền, chứng từ và đối soát |

TV4 tạo nền frontend; mỗi chủ module triển khai giao diện nghiệp vụ tương ứng. TV1 hỗ trợ kỹ thuật, không viết toàn bộ backend. Phân công cần điều chỉnh theo năng lực thực tế.

## 2. Product Backlog

P0: nền tảng hoặc luồng bắt buộc. P1: cần hoàn thiện trong phạm vi đề tài. Các mục cần tách thành task API/UI/test trước sprint.

| ID | Hạng mục và tiêu chí nghiệm thu chính | Chủ trì | Ưu tiên |
| --- | --- | --- | --- |
| BL01 | Tích hợp base code vào `develop` qua PR, CI đạt; tạo `develop`, bật bảo vệ nhánh `main`/`develop`, thống nhất quy trình trong CONTRIBUTING.md | TV1 | P0 |
| BL02 | Next.js/TypeScript: layout, API client, biểu mẫu, loading/error, UI dùng chung | TV4 | P0 |
| BL03 | Entity/configuration/migration riêng từng module; không chia sẻ EF entity xuyên module | TV1 điều phối, cả nhóm | P0 |
| BL04 | Đăng nhập/đăng xuất/refresh token, thu hồi phiên; không commit secret | TV1 | P0 |
| BL05 | Tài khoản đa vai trò; backend kiểm tra quyền; Admin không mặc nhiên có quyền vận hành | TV1 | P0 |
| BL06 | Khu vực, bàn, phiên bàn; trống/đang sử dụng; nhân viên mở/kết thúc phiên; chuyển bàn giữ nguyên order | TV2 | P0 |
| BL07 | Thực đơn, giá, size, nhóm topping/tùy chọn dùng chung nhiều món, phân bếp/bar, cờ hết món ở cấp món/biến thể/topping/tùy chọn (bếp, bar, nhân viên, quản lý; đặt lại khi mở ngày), ảnh Cloudflare R2 | TV3; TV1 hỗ trợ R2 | P0 |
| BL08 | Nguyên liệu (chế độ theo dõi tồn/tính vào chi phí khi mua, lưu trữ), đơn vị chuẩn, quy đổi; nhiều phiếu nhập/ngày với giá nhập điền sẵn, cờ trả bằng tiền két; số đếm tồn cuối nháp + duyệt; lượng dùng, giá vốn bình quân theo kỳ có tồn đầu, lợi nhuận gộp theo ngày | TV3 | P0 |
| BL09 | Order nhân viên tại bàn/mang về, ghi chú, gọi thêm nhiều đợt, giữ giá chốt | TV2 | P0 |
| BL10 | QR xem giá, gửi yêu cầu chỉ khi phiên bàn mở, token theo phiên, giới hạn tần suất, ẩn mục hết; theo dõi đúng phiên/order; xác nhận trước chế biến | TV2 | P1 |
| BL11 | Xác nhận order theo từng đợt gọi món; không kiểm tra tồn kho, mục đang đánh dấu hết bị chặn thêm mới; món đã xác nhận mà bếp báo hết đi vào luồng báo lỗi | TV2 + TV3 | P0 |
| BL12 | Hủy/điều chỉnh: nhân viên chỉ hủy trước chế biến; trường hợp khác (kể cả hủy sau khi làm) cần quản lý và lý do; gọi thêm sau thanh toán đưa order về thanh toán một phần | TV2 | P0 |
| BL13 | Phiếu bếp/bar đúng trạm; xem toàn order, chỉ cập nhật món được giao | TV4 | P0 |
| BL14 | Bắt đầu/hoàn thành từng phần theo số lượng; chống lặp và xung đột | TV4 | P0 |
| BL15 | Hoàn thành món chuyển sang Sẵn sàng, chống lặp; không trừ kho theo món | TV4 | P0 |
| BL16 | Giao từng phần; mang về có mã nhận, giờ dự kiến, thông tin liên hệ | TV4 + TV2 | P0 |
| BL17 | Sự cố/trả món/làm lại có quản lý duyệt; giữ món gốc, lần làm lại riêng; không tăng doanh thu | TV4 | P1 |
| BL18 | SignalR cập nhật sau commit; kết nối lại tải trạng thái đúng | TV4 | P1 |
| BL19 | Thu trước/trong/sau phục vụ, nhiều lần, tiền mặt kết hợp chuyển khoản; tiền thừa riêng; ghi người thu; chuyển khoản do người thu xác nhận thủ công | TV5 | P0 |
| BL20 | In/xem chứng từ khớp giao dịch; không phải hóa đơn điện tử thuế | TV5 | P1 |
| BL21 | Hoàn tiền có quản lý duyệt; hạn mức, chống lặp; kết quả chưa rõ chờ đối soát | TV5 | P1 |
| BL22 | Đóng order khi hết món và nghĩa vụ thu/hoàn phải xử lý (thu đủ hoặc quản lý miễn thu, tách khỏi doanh thu); không tự giải phóng bàn | TV2 + TV5 | P0 |
| BL23 | Nhà cung cấp, phiếu nhập, giá nhập và quy đổi lưu theo thời điểm | TV3 | P1 |
| BL24 | Sửa phiếu nhập trước chốt; sau chốt: sửa ngày chốt gần nhất hoặc dòng điều chỉnh tồn ở ngày hiện tại, có lý do, quyền và audit | TV3 | P1 |
| BL26a | Mở ngày bán: chỉ quản lý, tiền đầu ngày, chặn khi ngày trước chưa chốt, tồn đầu tự lấy, đặt lại cờ hết món; order/thanh toán gắn với ngày đang mở | TV1 + TV3 | P0 |
| BL26b | Chốt ngày bán: chặn khi còn order mở/bàn đang dùng, đủ số đếm tồn đã duyệt, đối soát tiền mặt (trừ chi mua bằng tiền két), chốt bù khi quên, tổng hợp doanh thu/giá vốn/lợi nhuận gộp | TV1 + TV3 + TV5 | P1 |
| BL27 | Báo cáo doanh thu, thất thu, giá vốn, lợi nhuận gộp theo ngày/tuần/tháng, món bán, tồn kho; chỉ quản lý xem; phân biệt doanh thu/dòng tiền | TV5 | P1 |
| BL28 | Audit người/thời điểm/lý do; Outbox, chống xử lý lặp ở luồng cần thiết | TV1 và chủ module | P0 |
| BL29 | Docker Compose, Nginx/HTTPS, SQL Server, Cloudflare R2 (bucket ảnh công khai, custom domain, CORS, API token; bucket sao lưu SQL private), log, health, sao lưu ngoài VPS và kiểm tra khôi phục | TV1 | P1 |
| BL30 | UAT order → chế biến → giao từng phần → thanh toán; tồn cuối bất thường, làm lại, hoàn tiền, miễn thu, chuyển bàn, chốt ngày bán | Cả nhóm, TV1 điều phối | P0 |

## 3. Kế hoạch sprint đề xuất

| Sprint | Mục tiêu | Backlog |
| --- | --- | --- |
| 1 | Chạy dự án, frontend, xác thực, dữ liệu nền | BL01–BL05; bắt đầu BL06–BL08 |
| 2 | Nhân viên tạo/xác nhận order, bếp/bar nhận | Hoàn tất BL06–BL09, BL11–BL13; BL26a (mở ngày bán); audit tối thiểu BL28 |
| 3 | Hoàn thành/giao từng phần, chống lặp | BL14–BL16, BL18; transaction/Outbox cần thiết BL28 |
| 4 | QR, thanh toán hỗn hợp, đóng order | BL10, BL19–BL20, BL22 |
| 5 | Ngoại lệ và quản trị vận hành | BL17, BL21, BL23–BL24, BL26b, BL27 |
| 6 | UAT, sửa lỗi, triển khai, bàn giao | BL29–BL30, tài liệu |

Chưa cam kết số tuần; nhóm cần ước lượng theo năng lực/thời gian. BL30 thực hiện tăng dần từ Sprint 2. Tài khoản, bucket và tên miền Cloudflare R2 (chủ dự án sẽ đăng ký) cần được chuẩn bị trước phần ảnh BL07.

## 4. Phối hợp liên module

- TV3 + TV5: chốt ngày bán lấy lượng dùng, giá vốn từ Inventory và doanh thu từ Billing để tính lợi nhuận.
- TV1 + TV3: chốt ngày bán bắt buộc đủ số đếm tồn; thao tác chốt cần transaction và idempotency.
- TV2 + TV5: tiền phải thu, điều chỉnh, hoàn tiền, miễn thu, điều kiện đóng order.
- TV1 + cả nhóm: hợp đồng, transaction, audit, quyền và CI.

Thống nhất ID, DTO, lỗi và chủ điều phối transaction trước khi lập trình. Không trực tiếp dùng DbContext của module khác; hợp đồng không trả EF entity. Migrations chỉ thay đổi schema module sở hữu.

## 5. Điều kiện Done

- API và giao diện cần thiết hoạt động, đạt tiêu chí nghiệm thu.
- Backend kiểm tra quyền và phạm vi dữ liệu.
- Migration được review nếu có thay đổi dữ liệu.
- Test phù hợp đạt, CI xanh; PR có ít nhất một thành viên khác review.
- Chủ module liên quan review hợp đồng/transaction.
- Tài liệu và hợp đồng được cập nhật.

Khi tạo issue, giữ ID BLxx, ghi người thực hiện, sprint, phụ thuộc và task API/UI/test. Theo dõi Backlog → Ready → In Progress → Review → Done; ghi rõ lý do khi bị chặn.

## 6. Trạng thái và phạm vi

Base backend đã push lên nhánh chore/backend-foundation. BL01: tạo nhánh `develop` từ base này, mọi feature tách từ `develop` và PR về `develop`; chỉ khi `develop` ổn định mới PR vào `main`; chưa coi các tính năng nghiệp vụ hoàn thành vì scaffold tồn tại.

Ngoài phạm vi: giao hàng, đa chi nhánh, đặt bàn, chấm công, tính lương/lập lịch, khuyến mãi, VAT/phí phục vụ, lô/hạn sử dụng, công thức/định lượng và trừ kho theo món, lợi nhuận theo món, chi phí vận hành khác, gộp bàn, tách hóa đơn, chế độ offline, công nợ, tích hợp ngân hàng tự động, hóa đơn điện tử thuế.
> MVP OVERRIDE 2026-11-05: The scope in docs/superpowers/specs/2026-11-05-restaurant-mvp-scope.md overrides conflicting backlog text. Keep QR and sound; cash/bank only, one invoice per payment, amount refunds, no closed-day edits. See docs/SYNC_STATUS.md for approval gates.
