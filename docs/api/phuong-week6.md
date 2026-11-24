# Tuần 6 của Phương

## PHU-6.1 · Hoàn tiền và điều chỉnh nghĩa vụ

Các lệnh POST của Billing dùng `Idempotency-Key` dạng UUID và `businessDayId` của ngày đang bán. Gửi lại cùng khóa và nội dung để nhận kết quả cũ nếu mất phản hồi. Mỗi quyết định mới dùng một khóa mới.

| Thao tác | Endpoint dưới `/api/billing` | Quyền |
| --- | --- | --- |
| Xem các đề nghị của hóa đơn | `GET /invoices/{id}/refunds` | Một trong ba quyền hoàn tiền |
| Đề nghị hoàn một phần hoặc toàn bộ khoản đã thu | `POST /payments/{paymentId}/refunds` | `billing.refunds.request` |
| Duyệt hoặc từ chối đề nghị | `POST /refunds/{refundId}/approve`, `POST /refunds/{refundId}/reject` | `billing.refunds.approve` |
| Ghi kết quả chi hoặc đưa vào chờ đối soát | `POST /refunds/{refundId}/pay` | `billing.refunds.pay` |
| Xác nhận kết quả sau đối soát | `POST /refunds/{refundId}/reconcile` | `billing.refunds.pay` |
| Ghi điều chỉnh nghĩa vụ có dấu | `POST /invoices/{id}/obligation-adjustments` | `billing.waivers.approve` |

Đề nghị gồm `businessDayId`, `amount` theo đồng và `reason`. Hệ thống giữ hạn mức từ lúc duyệt. Nếu hai người cùng duyệt và tổng vượt số tiền còn hoàn được trên khoản thu, một lệnh nhận HTTP 409 với mã `billing.amount.exceeds_available`.

Khi chi, gửi `outcome` là `DANG_CHI`, `THANH_CONG` hoặc `CHO_DOI_SOAT`. Trạng thái chờ đối soát tiếp tục giữ hạn mức và chặn chốt ngày. Khi có bằng chứng, gửi `THANH_CONG` hoặc `HUY` qua endpoint đối soát. Hoàn qua chuyển khoản thành công cần `reference`. API ghi nhận kết quả chi do nhân viên xác nhận. Phiếu hoàn được lập khi ghi nhận thành công và đọc lại qua API chứng từ.

Ví dụ, hóa đơn hôm qua đã thu 100.000 đồng. Chi hoàn 60.000 đồng hôm nay làm số thu ròng của hóa đơn còn 40.000 đồng. Nghĩa vụ vẫn là 100.000 đồng cho đến khi quản lý ghi một điều chỉnh riêng. Khoản chi hoàn và điều chỉnh phát sinh hôm nay xuất hiện ở ngày hôm nay. Snapshot hôm qua giữ nguyên.

## PHU-6.2 · Điều chỉnh tồn và sửa phiếu nhập

Hai lệnh sau yêu cầu vai trò Manager, quyền `inventory.adjustments.approve` và header `Idempotency-Key`.

| Endpoint | Nội dung |
| --- | --- |
| `POST /api/inventory/adjustments` | Gửi `dayId`, `ingredientId`, `quantity` theo đơn vị chuẩn, `value` là giá trị điều chỉnh và `reason`. Có thể ghi thêm `originalDayId` để tham chiếu ngày gốc. |
| `POST /api/inventory/purchases/{id}/corrections` | Gửi `reason` và `replacement` có cùng cấu trúc với lệnh lập phiếu nhập. Dùng ID trả về để mở phiếu thay thế. |

Lượng điều chỉnh phải khác 0. Giá trị bằng 0 hoặc cùng dấu với lượng. Ví dụ, hao hụt 2 đơn vị trị giá 10.000 đồng ghi `quantity = -2`, `value = -10000`. Hai giá trị này đi vào phép tính bình quân và giá vốn khi chốt ngày.

Sửa phiếu sẽ giữ chi tiết gốc, chuyển phiếu cũ sang `HUY` và tạo phiếu thay thế cùng ngày ở trạng thái trước khi sửa. Với phiếu đã có hiệu lực, chỉ phiếu thay thế được tính vào nhập kho và tiền két. Giao dịch ghi cả thay đổi kho và nhật ký có người thực hiện, lý do và mã đối tượng. Nếu ghi nhật ký lỗi, toàn bộ thay đổi được hoàn tác.

## PHU-6.3 · Báo cáo cho quản lý

| Endpoint dưới `/api/reporting` | Kết quả |
| --- | --- |
| `GET /daily?date=2026-11-24` | Tổng kết một ngày đã chốt |
| `GET /range?from=2026-11-01&to=2026-11-24` | Tổng cả kỳ, từng ngày và dòng tiền theo phương thức |
| `GET /items-sold?from=2026-11-01&to=2026-11-24` | Số phần và thành tiền gốc theo tên món đã lưu trên dòng hóa đơn |
| `GET /stock?from=2026-11-01&to=2026-11-24` | Tồn đầu, tồn cuối, giá trị tồn cuối, lượng dùng và giá vốn theo ngày, nguyên liệu và đơn vị |

Khoảng ngày bao gồm cả hai đầu mút. Báo cáo dùng snapshot đã chốt. Ngày chưa có snapshot trả danh sách ngày rỗng. Doanh thu thuần bằng tiền món cộng điều chỉnh có dấu trừ miễn thu. Tiền thu và tiền hoàn được tổng hợp riêng theo phương thức. Chi mua bằng tiền két nằm ở `drawerPurchases`. Lợi nhuận gộp bằng doanh thu thuần trừ giá vốn đã chốt. Bốn endpoint yêu cầu vai trò Manager và quyền `reporting.read`.

## Bằng chứng kiểm thử

Ngày 24/11/2026, bản phát triển dựa trên `develop@7756dd2` đã chạy các ca sau với SQL Server trong Docker.

| Ca | Kết quả kiểm tra |
| --- | --- |
| T09 | Hai đề nghị 60 cùng được tạo trên khoản thu 100. Khi duyệt đồng thời, chỉ một đề nghị được duyệt. Chờ đối soát giữ hạn mức, chặn chốt và chặn chi lại. Gửi lại xác nhận thành công chỉ tạo một phiếu hoàn. |
| T10 | Hoàn hóa đơn đã đóng vào ngày kế tiếp, kiểm tra nghĩa vụ trước và sau điều chỉnh, chốt ngày mới, rồi đối chiếu dòng tiền và doanh thu của cả hai ngày. |
| Kho và audit | Sửa phiếu đã có hiệu lực, gửi lại yêu cầu, điều chỉnh âm, gây lỗi ghi audit để kiểm tra rollback, sau đó đối chiếu tồn và giá vốn với báo cáo. |
| Quyền báo cáo | Qua HTTP, Manager nhận 200. Staff, Kitchen và Bar nhận 403 trên cả bốn endpoint. |

Các ca nghiệp vụ nằm trong `tests/Restaurant.IntegrationTests/PhuongWeekSixSqlServerTests.cs`. Kiểm thử HTTP dùng luồng đăng nhập sẵn có trong `PhuongWeekFiveSqlServerTests.cs`. Môi trường chạy cần các migration Billing, Inventory, SalesDay và Audit hiện có.
