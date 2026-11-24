# API tuần 3 của Phương

## Catalog · PHU-2.1b

`GET /api/public/menu` cho phép khách đọc. Khi có ngày đang bán, phản hồi chứa `businessDayId` và các nhóm có món bán được. Một món phải có ít nhất một biến thể còn bán và đủ lựa chọn cho từng nhóm bắt buộc. Khi chưa mở ngày, API trả `businessDayId: null` và `groups: []`.

`PUT /api/catalog/availability` cần quyền `catalog.availability.manage`. Gửi `businessDayId`, `kind` (ITEM, VARIANT, TOPPING hoặc OPTION), `targetId` và `soldOut`. Thành công trả 204. ID sai loại trả 404. Ngày đã ngừng bán trả 409. Server ghi ID người thao tác từ token và giờ UTC. Topping và tùy chọn dùng bảng HetTuyChon với loại nhóm tương ứng. Mỗi cờ thuộc một ngày bán nên khi mở ngày mới, các cờ cũ vẫn tra cứu được trong lịch sử.

Sales có thể lấy `IMenuPricing` qua DI và gọi `PriceAsync` khi nhận và khi xác nhận đợt. Giá mỗi phần bằng giá biến thể cộng phụ phí các tùy chọn đã chọn. Nếu một phần vi phạm quan hệ món/biến thể/tùy chọn, cờ hết hoặc min/max, kết quả trả lỗi theo `LineNo`.

## Tích hợp Sales · PHU-3.1

Trong cùng request scope, Sales mở transaction trên `SalesDbContext`, gọi `ISalesDayGate.RequireOpenAsync` trước khi khóa đơn, kiểm tra lại đợt và gọi `IInvoiceCharging.ChargeConfirmedBatchAsync`. Adapter của Host cho Billing dùng cùng SqlConnection và SqlTransaction. Sales commit sau khi cả đợt, hóa đơn và các bản ghi liên quan đã lưu thành công. Khi contract trả lỗi hoặc ném exception, Sales rollback transaction.

Đợt xác nhận đầu tạo hóa đơn. Những đợt kế tiếp thêm dòng vào hóa đơn của cùng đơn. Mỗi `PortionId` chỉ tính tiền một lần. Gửi lại cùng khóa và cùng nội dung trả ID ban đầu. Tái sử dụng khóa cho nội dung khác trả `billing.idempotency.conflict`. Có thể thử lại một transaction bị deadlock từ đầu bằng cùng khóa.

## Thu tiền và miễn thu · PHU-3.2 và PHU-3.3

Các đường dẫn sau bắt đầu bằng `/api/billing`. Lệnh POST cần token, header `Idempotency-Key` là UUID và `businessDayId` trong JSON. Lặp lại lệnh thành công trả cùng `resourceId` với `replayed: true`. Các thao tác cùng hóa đơn được tuần tự hóa trong SQL Server.

| Phương thức | Đường dẫn | Nội dung / kết quả |
|---|---|---|
| GET | `/orders/{orderId}/invoice`, `/invoices/{id}` | Hóa đơn, dòng, khoản thu, miễn thu, chứng từ và số dư. Trả 404 nếu chưa có hóa đơn. |
| POST | `/invoices/{id}/payments` | Tiền mặt dùng `method: "TIEN_MAT"`, `amount`, `tendered`. Chuyển khoản dùng `method: "CHUYEN_KHOAN"`, `amount`, `bankReference`. Các trường riêng của phương thức kia có giá trị null hoặc bỏ qua. |
| POST | `/invoices/{id}/payments/{paymentId}/confirm`, `/cancel` | Xác nhận hoặc hủy một khoản chuyển đang chờ của ngày giao dịch. |
| POST | `/invoices/{id}/waivers` | Gửi `amount` và `reason`. Server kiểm tra quyền `billing.waivers.approve` và số còn được miễn. |
| GET | `/documents/{id}` | Nội dung JSON chốt tại thời điểm lập hóa đơn/phiếu thu. |

Quyền thu và xử lý chuyển khoản là `billing.payments.collect`. Người có quyền thu hoặc duyệt miễn thu có thể đọc hóa đơn, chứng từ. Tiền tính theo đồng nguyên. `due` là nghĩa vụ còn phải thu theo C01. `reserved` là tiền chuyển khoản đang chờ. `available = max(0, due - reserved)` là giới hạn để thu thêm hoặc miễn thu. Miễn thu giảm nghĩa vụ, còn xác nhận chuyển khoản tăng tiền thực thu. Hủy chuyển khoản giải phóng số đã giữ.

Mỗi khoản tiền mặt thành công hoặc chuyển khoản được xác nhận phát hành một phiếu thu. Đợt gọi thêm và miễn thu tạo bản hóa đơn mới. Bản đã phát hành giữ nguyên nội dung kể cả khi số dư sau đó thay đổi. `Billing:StoreName` và `Billing:StoreAddress` được chụp vào chứng từ khi phát hành. D-6 chưa chốt tên cửa hàng, địa chỉ và các trường bắt buộc để in. Trường cấu hình chưa điền có giá trị null. Nhóm sẽ đối chiếu bản in sau khi thống nhất D-6.

## Migration và kiểm thử

Áp dụng migration `BillingCommandIdempotency` sau `InitialBilling` vào cùng database, bằng project `Restaurant.Modules.Billing.Infrastructure` và context `BillingDbContext`. Bảng mới `billing.YeuCauDaXuLy` lưu khóa, dấu vân tay nội dung và ID kết quả để trả lại khi người dùng thử lại. Khi hạ phiên bản ứng dụng, giữ bảng này để bảo toàn lịch sử lệnh.

```powershell
dotnet test tests/Restaurant.IntegrationTests -c Release --filter FullyQualifiedName~PhuongWeekThreeSqlServerTests
```

Đặt `IDENTITY_TEST_SQLSERVER` trỏ đến SQL Server thử nghiệm để các ca SQL thực sự chạy. Bộ test tạo database riêng và xóa sau khi hoàn tất. Ngày 21/11/2026, kiểm thử cục bộ trên SQL Server 2022 trong Docker đạt 5/5 test kiến trúc và 276/276 test tích hợp. Hai ca tranh chấp (cùng xác nhận đợt và hai thu ngân cùng thu) được chạy thêm 5 lượt, đạt 10/10 lần thực thi. Frontend đạt 71/71 unit test. Test HTTP dùng tài khoản thử nghiệm để đối chiếu quyền thu tiền và miễn thu.

OpenAPI và TypeScript client được lưu tại `docs/api/openapi.json` và `web/src/lib/api/generated/schema.d.ts`.
