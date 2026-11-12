# Chiến lược kiểm thử Tuần 1 — HAI-1.6

**Trạng thái: Bản nháp cần nhóm xác nhận, cập nhật 12/11/2026.** Tài liệu thực hiện HAI-1.6 theo kế hoạch 7 tuần. Ma trận 17 test chính thức lấy từ `docs/phan-cong-chi-tiet-7-tuan.md`, mục 8. Các ca kiểm tra hạ tầng bổ sung được giữ tại phụ lục.

## 1. Tháp kiểm thử và điều kiện nghiệm thu

- **Unit (nhiều nhất):** nghiệp vụ ngày bán, validation, mã lỗi, authorization, UI components. Chạy trên mỗi PR.
- **Integration (ít hơn):** Identity, SQL Server thực, migration, transaction C03, ràng buộc SalesDay/Catalog. Chạy với SQL Server thử nghiệm riêng.
- **E2E (ít, tập trung):** đăng nhập/refresh từ web, mở ngày bán, logout và đổi mật khẩu. Khi chưa có backend dùng mock; kết quả mock không thay cho staging.
- **Infrastructure/UAT:** SSH, firewall, DNS, HTTPS, health, CI, GHCR và demo từ mạng 4G.

Yêu cầu PR: CI xanh, không có secret, test tương ứng và review. **SKIP không được coi là PASS.** Mỗi ca ghi trạng thái `PASS/FAIL/SKIP/BLOCKED/NOT_RUN` cùng commit, môi trường và bằng chứng.

## 2. Quy tắc đặt tên, dữ liệu và môi trường

- C#: `<Method>_<Scenario>_<ExpectedResult>`; UI: `<feature> / <action> / <expected>`; định danh ca theo `T01`…`T17`.
- SQL test chạy trên container CI **tạm thời** (biến `IDENTITY_TEST_SQLSERVER`) hoặc instance test riêng có quyền tạo database. Mỗi test chỉ xóa database do chính test tạo. **Không bao giờ trỏ CI vào database staging/production.**
- Chuẩn bị tài khoản test Admin, Manager, Staff; 1 ngày bán, tiền đầu ngày giả, request có Idempotency-Key giống/khác; hai luồng SQL transaction; cookie/token test giả. Không dùng dữ liệu khách thật.
- Lưu bí mật ở environment/secret store; log, screenshots và PR không chứa mật khẩu, token hay private key.
- Migration cần review + xác nhận đúng DB trước khi áp dụng; host không tự migrate. Quét cổng từ ngoài, không phải chỉ `ss` trên VPS.

## 3. Mức độ lỗi S1–S4

| Mức | Ý nghĩa | Ví dụ | Xử lý |
|---|---|---|---|
| S1 — Nghiêm trọng | Rò rỉ secret/dữ liệu, vượt quyền, dữ liệu sai không phục hồi | Public SQL 1433, mật khẩu lộ, transaction rollback không toàn vẹn | Dừng triển khai, báo ngay |
| S2 — Cao | Luồng chính không hoạt động | Login lỗi, mở ngày lần hai vẫn thành công, HTTPS lỗi | Chặn merge/release |
| S3 — Trung bình | Lỗi chức năng phụ, có đường vòng | UI trạng thái sai tạm thời, error message thiếu | Ghi issue, lên lịch sửa |
| S4 — Thấp | Lỗi trình bày hoặc tài liệu | Lỗi chính tả, căn chỉnh | Gộp đợt sửa hợp lý |

## 4. Ma trận 17 test chính thức của nhóm

Nguồn: `docs/phan-cong-chi-tiet-7-tuan.md`, mục 8.
Chủ là người viết test; Hải phụ trách chạy CI, tổng hợp trạng thái và bằng chứng.

| ID | Nội dung kiểm thử | Chủ viết | Tuần | Trạng thái |
|---|---|---|---|---|
| T01 | Chặn trùng vai trò/tên đăng nhập, hóa đơn–đơn, dòng–phần món | Hiếu (Identity), Phương (Billing) | 1, 3 | NOT_RUN |
| T02 | Biến thể/tùy chọn không thuộc món bị từ chối | Phương, Hiếu | 2, 3 | NOT_RUN |
| T03 | Mở/chuyển bàn đồng thời không chiếm hai lần | Hiếu | 2 | NOT_RUN |
| T04 | Mã QR một mình không đặt được; mã hết hạn/thu hồi/khác phiên bị từ chối | Hiếu | 5 | NOT_RUN |
| T05 | Xác nhận đợt lặp: một khoản nghĩa vụ, một phiếu đầu | Hiếu + Phương | 3 | NOT_RUN |
| T06 | Làm lại cần sự cố đã duyệt, không phát sinh doanh thu | Hiếu | 6 | NOT_RUN |
| T07 | Một phần/lần làm không giao hai lần hoặc sau khi hủy | Hiếu | 4 | NOT_RUN |
| T08 | Hai thu ngân không thu dư | Phương | 3 | NOT_RUN |
| T09 | Hoàn tiền đồng thời không vượt đã thu; chưa rõ kết quả giữ hạn mức | Phương | 6 | NOT_RUN |
| T10 | Hoàn đơn ngày cũ ghi vào ngày hiện tại; snapshot cũ không đổi | Phương | 6 | NOT_RUN |
| T11 | Sửa quy đổi không đổi số lượng/giá trị lịch sử của phiếu nhập | Phương | 4 | NOT_RUN |
| T12 | Nguyên liệu lưu trữ còn tồn vẫn đếm được; đổi chế độ/đơn vị bị chặn | Phương | 4 | NOT_RUN |
| T13 | Số lượng 0 mà giá trị khác 0 chặn chốt; điều chỉnh có dấu vào giá vốn | Phương | 5 | NOT_RUN |
| T14 | Hai người chốt chỉ ra một snapshot; ghi muộn chờ rồi bị từ chối | Phương (spike tuần 1, hoàn chỉnh tuần 5) | 1, 5 | NOT_RUN |
| T15 | Rollback khi chốt không để lại dữ liệu nửa chừng | Phương | 1, 5 | NOT_RUN |
| T16 | Lãi/lỗ âm biểu diễn được; không cộng khác đơn vị | Phương | 5 | NOT_RUN |
| T17 | Thu hồi vai trò có hiệu lực với token đang dùng | Hiếu | 1–2 | NOT_RUN |

`NOT_RUN` nghĩa là chưa có bằng chứng nghiệm thu được ghi vào bảng này; không khẳng định test chưa tồn tại. Đặc biệt, bằng chứng spike C03 và SQL CI của T14–T15 phải được đối chiếu trước khi đổi sang `PASS`.

## 5. Mẫu báo lỗi và bằng chứng

**Tên issue:** `[S2][INF15] Mở ngày bán lần hai không bị chặn`

**Các mục bắt buộc:** ngày, người test, commit/PR, môi trường, điều kiện đầu vào, bước tái hiện, mong đợi, thực tế, ảnh/log đã che bí mật, severity S1–S4, người xử lý, cách kiểm lại, trạng thái.

**Bảng bằng chứng:** `ID | Người chạy | Commit/PR | Môi trường | Lệnh/bước | Expected | Actual | PASS/FAIL/SKIP/BLOCKED | Link bằng chứng`.

## 6. Lịch và quy trình

- Mỗi PR: `dotnet restore ... --locked-mode`, build Release, test, format; frontend cần `npm run lint`, `npm run build`, `npm test` khi `web/` sẵn sàng.
- SQL CI phải chạy **các test có `SqlServer` trong tên**, gồm Identity và PhuongWeekOne. Kiểm tra số PASS/FAIL/SKIP trong workflow, không chỉ dấu xanh job.
- Khi blocked > 1 giờ: báo người liên quan, ghi mục Chặn hằng ngày, chuyển sang việc dự phòng.
- Thứ Sáu: Hải thu bằng chứng quét cổng ngoài, HTTPS, PR đỏ bị chặn, runbook deploy thủ công; điền bảng nghiệm thu mục 7 của `hdtuan1.pdf`.

## 7. Phụ lục — Các kiểm tra hạ tầng và tích hợp đề xuất

Trạng thái được cập nhật theo bằng chứng hiện có đến ngày 12/11/2026. Các tiêu chí chưa kiểm tra đầy đủ không được coi là PASS.

| ID | Ca và kỳ vọng | Chủ trì | Mức | Tình trạng ban đầu |
|---|---|---|---|---|
| INF01 | SSH khóa với user `deploy`; root/password login theo cấu hình đã duyệt | Hải | S1 | Chưa nghiệm thu |
| INF02 | Quét từ 4G chỉ thấy 22/80/443, không thấy 1433/8080 | Hải | S1 | Chưa nghiệm thu |
| INF03 | UFW/fail2ban/auto security updates hoạt động | Hải | S2 | Chưa nghiệm thu |
| INF04 | SQL Docker nhận `SELECT 1`; volume tồn tại sau recreate không xóa dữ liệu | Hải | S2 | SELECT 1 đã PASS; volume chưa thử |
| INF05 | `staging.quanlynh.id.vn` phân giải đúng IP VPS | Hải + người giữ DNS | S2 | Domain truy cập được qua HTTPS; chưa đối chiếu trực tiếp bản ghi A với IP VPS |
| INF06 | HTTPS hợp lệ, HTTP→HTTPS, gia hạn được thử | Hải | S2 | HTTPS trả 200; chưa nghiệm thu HTTP→HTTPS và gia hạn chứng chỉ |
| INF07 | `GET /health/live` 200 qua staging Nginx | Hải | S2 | PASS — HTTPS 200 trên VPS ngày 12/11/2026 |
| INF08 | `GET /health/ready` 200 sau migration và kết nối DB | Hải | S2 | HTTPS 200 ngày 12/11/2026; chưa xác minh riêng migration |
| INF09 | Identity migration idempotent và bootstrap Admin 1 lần | Hiếu + Hải | S1 | NOT_RUN trên staging |
| INF10 | Login thật, khóa sau sai mật khẩu, rate limit đúng | Hiếu | S2 | NOT_RUN trên staging |
| INF11 | `/me`, logout, đổi mật khẩu, session cũ bị từ chối | Hiếu | S2 | NOT_RUN trên staging |
| INF12 | Refresh cookie xoay vòng, thu hồi token cũ | Hiếu + Bảo Anh | S2 | Chờ feature refresh |
| INF13 | Frontend mock login/refresh, nhiều 401 chỉ refresh một lần | Bảo Anh | S2 | Web đã có và CI xanh; chưa đối chiếu bằng chứng riêng cho ca này |
| INF14 | E2E web đăng nhập thật, refresh sau 15 phút | Bảo Anh + Hiếu + Hải | S2 | Frontend/HTTPS hoạt động; chưa nghiệm thu refresh sau 15 phút |
| INF15 | SalesDay mở lần đầu, chặn lần hai; đồng thời/idempotency | Phương + Duy | S2 | Chờ bằng chứng staging |
| INF16 | C03 Shared/Exclusive SQL locks; rollback 2 DbContext nguyên vẹn | Phương | S1 | Có test cục bộ, chưa chứng thực CI |
| INF17 | CI test/backend/frontend, PR đỏ bị chặn, GHCR phát hành image | Hải | S2 | Chưa nghiệm thu |

Không được đánh dấu `PASS` cho cả ca nếu mới thực hiện **một phần**. Bảng này là checklist bổ sung, không thay thế ma trận chính thức tại mục 4.
