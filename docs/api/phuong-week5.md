# Tuần 5 của Phương · Kiểm kê và chốt ngày

## PHU-5.1 · Nhập và duyệt số đếm

| Thao tác | API | Quyền |
| --- | --- | --- |
| Xem danh sách và phiên bản từng dòng | `GET /api/inventory/days/{day}/counts` | `inventory.counts.enter` hoặc `inventory.counts.approve` |
| Ghi nháp | `PUT /api/inventory/days/{day}/counts/{ingredient}` | `inventory.counts.enter` |
| Duyệt số nháp hiện tại | `POST /api/inventory/days/{day}/counts/{ingredient}/approve` | `inventory.counts.approve` |

Nhân viên, bếp và bar nhập số đếm theo đơn vị chuẩn của từng nguyên liệu. Body ghi nháp gồm `quantity` và `version` lấy từ GET. Quản lý đọc lại dòng rồi gửi `version` mới nhất để duyệt. Sửa số nháp sẽ xóa phê duyệt cũ. Khi nhận HTTP 409 với mã `inventory.count.stale`, tải lại dòng trước khi tiếp tục. Giá trị tiền và giá vốn đã chốt chỉ hiển thị cho người có quyền duyệt.

Theo C02, lượng khả dụng bằng tồn đầu cộng nhập hiệu lực và điều chỉnh lượng có dấu. Giá trị khả dụng tính tương ứng theo tiền. Giá bình quân bằng giá trị chia lượng khả dụng. Giá trị cuối bằng lượng duyệt nhân giá bình quân. Giá vốn bằng giá trị khả dụng trừ giá trị cuối. Nguyên liệu tính chi phí khi mua đóng góp giá trị phiếu hiệu lực vào giá vốn ngày. Khoản mua bằng tiền két còn tác động tới đối soát tiền mặt.

Hệ thống chặn chốt nếu lượng bằng 0 nhưng giá trị khác 0, số đếm vượt lượng khả dụng hoặc giá trị khả dụng âm. Phần lượng dùng giữ riêng theo nguyên liệu và đơn vị. Giá vốn ngày cộng các khoản tiền trước khi làm tròn đến đồng, với nửa đồng làm tròn ra xa 0. Snapshot lưu chênh lệch giữa giá vốn đã làm tròn và tổng trước làm tròn. Giá trị tồn mang sang ngày mới giữ 8 chữ số thập phân.

## PHU-5.2 · Đối soát và chốt ngày

Các API dưới đây yêu cầu `salesday.manage`.

| Thao tác | API |
| --- | --- |
| Kiểm tra lý do chặn và bắt đầu chốt | `GET /api/salesday/{dayId}/close-checklist`, sau đó `POST /api/salesday/{dayId}/begin-close` |
| Xem tiền két kỳ vọng, giá vốn, doanh thu và lợi nhuận dự kiến | `GET /api/salesday/{dayId}/settlement-preview` |
| Trở về đang bán để chỉnh số đếm hoặc xử lý vướng mắc | `POST /api/salesday/{dayId}/cancel-close` |
| Chốt chính thức từ trạng thái `CHO_CHOT` | `POST /api/salesday/{dayId}/finalize` |
| Xem snapshot đã chốt và tổng theo phương thức thanh toán | `GET /api/salesday/{dayId}/snapshot` |
| Liệt kê ngày đã chốt, mới nhất trước | `GET /api/salesday/history?offset=0&limit=30` |

Mỗi lệnh bắt đầu, hủy và chốt chính thức dùng một UUID riêng trong header `Idempotency-Key`. Ví dụ body chốt bù khi quản lý quên chốt cuối ca:

```json
{
  "countedCash": 575000,
  "differenceReason": "Két thiếu 5.000 đồng so với sổ",
  "lateClose": true,
  "lateReason": "Ca tối chưa chốt trước khi bàn giao"
}
```

Tiền kỳ vọng bằng tiền đầu ngày cộng thu tiền mặt thành công, trừ hoàn tiền mặt thành công và tiền mua hàng từ két. Khi số đếm thực tế khác số kỳ vọng, quản lý phải ghi lý do. Chốt bù áp dụng cho ngày bán đang chờ chốt và lưu ngày kinh doanh gốc, người chốt, thời điểm thực hiện và lý do. Gửi lại cùng nội dung, người thực hiện và khóa sẽ nhận lại snapshot ban đầu.

Phần tổng hợp áp dụng phương án C06-A trong tài liệu phân công. Doanh thu thuần bằng tiền món đã xác nhận cộng điều chỉnh nghĩa vụ có dấu trừ miễn thu. Dòng tiền theo phương thức bằng thu thành công trừ hoàn thành công trong ngày ghi nhận. Lợi nhuận gộp bằng doanh thu thuần trừ giá vốn và có thể âm.

Một giao dịch SQL giữ khóa độc quyền theo ngày, kiểm tra lại điều kiện, ghi giá trị tồn cuối, tổng hợp ngày, tổng theo phương thức, lịch sử và sự kiện Outbox `salesday.day.closed`, rồi chuyển ngày sang `DA_CHOT`. Mở ngày tiếp theo ghi số đếm đã duyệt và giá trị tồn cuối thành tồn đầu trong cùng giao dịch mở ngày.

## Migration và kiểm thử

Áp dụng `StockCountValuation` cho `InventoryDbContext` và `FinalDaySettlement` cho `SalesDayDbContext` trước khi triển khai API. Migration kho mở rộng độ chính xác số lượng, giá trị tồn và bổ sung dữ liệu điều chỉnh có dấu phục vụ tính giá vốn. API quản lý điều chỉnh tồn thuộc PHU-6.2. Hạ migration kho sẽ thu hẹp lại độ chính xác của dữ liệu đã ghi.

Đặt `IDENTITY_TEST_SQLSERVER` trỏ tới SQL Server thử nghiệm rồi chạy từ thư mục gốc:

```powershell
dotnet test tests/Restaurant.IntegrationTests -c Release --filter FullyQualifiedName~PhuongWeekFiveSqlServerTests
```

Ngày 23/11/2026, 9/9 ca SQL tuần 5 đạt trên SQL Server trong Docker. Toàn bộ backend đạt 5/5 test kiến trúc và 312/312 test tích hợp, không bỏ qua ca nào. Hai ca chốt đồng thời và rollback được chạy thêm 3 lượt, đạt 6/6 lần thực thi. Frontend đạt 71/71 test và kiểm tra kiểu TypeScript thành công.
