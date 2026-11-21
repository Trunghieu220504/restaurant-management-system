# Spike C03 tuần 1

Trạng thái: các kịch bản C03 hiện có đã đạt trên SQL Server 2022 chạy trong Docker ngày 10/11/2026. File TestResults/PhuongWeek1/phuong-week1.trx xác nhận 5/5 test tuần 1 đạt, không lỗi và không bỏ qua. Kết quả này xác nhận các kịch bản đã viết; chưa thay thế nghiệm thu CI/staging hoặc kiểm thử tải.

`PhuongWeekOneSqlServerTests` kiểm tra hai DbContext SalesDay và Catalog cùng SqlConnection/SqlTransaction, UseTransactionAsync cho mỗi context, ghi cả hai module rồi rollback và đọc lại bằng context độc lập để kiểm tra không còn dữ liệu. Khóa ứng dụng dùng LockOwner=Transaction.

Kịch bản khóa kiểm tra hai Shared cùng tồn tại, Exclusive bị timeout khi Shared còn giữ, Exclusive được cấp sau rollback, Exclusive thứ hai bị từ chối khi người thứ nhất giữ khóa rồi lấy được sau giải phóng. Test mở ngày đồng thời còn kiểm tra trạng thái nghiệp vụ sau khi chờ khóa: chỉ một ngày được tạo.

Chạy:

```powershell
dotnet test tests/Restaurant.IntegrationTests -c Release --filter FullyQualifiedName~PhuongWeekOneSqlServerTests
```

Cần IDENTITY_TEST_SQLSERVER trỏ đến SQL Server thử nghiệm. Đã chạy thành công qua scripts/Test-PhuongWeek1.ps1 từ terminal của người dùng. Kết quả CI và phần chờ ký được ghi bên dưới. Nếu các kịch bản bổ sung thất bại, xem phương án trạng thái chờ bền vững trong thiết kế dữ liệu.

## Chạy lặp 20 lần (18/11/2026)

Mục này bổ sung cho yêu cầu "lặp mỗi kịch bản ≥ 20 lần" của nghiệm thu tuần 1 (`docs/reviews/week1-acceptance.md`, mục 8.1 việc 5). Nội dung phía trên không đổi.

| Mục | Giá trị |
|---|---|
| Mã nguồn | `develop` tại `3b80703` |
| Máy / công cụ | Windows 11, .NET SDK 10.0.300, Docker 29.7.2 |
| SQL Server | `mcr.microsoft.com/mssql/server:2022-latest`, một container mới, mật khẩu `sa` sinh ngẫu nhiên, xóa sau khi chạy |
| Build | `dotnet restore --locked-mode`, `dotnet build -c Release`: 0 cảnh báo, 0 lỗi |
| Lệnh | 20 lần liên tiếp: `dotnet test tests/Restaurant.IntegrationTests -c Release --no-build --no-restore --filter FullyQualifiedName~PhuongWeekOneSqlServerTests` |
| Kết quả | **20/20 lượt đạt; mỗi lượt 9/9 test đạt, 0 lỗi, 0 bỏ qua** (tổng 180 lần chạy test). Mỗi lượt 23–41 giây. |

Chín test trong mỗi lượt: ràng buộc Catalog; hai khóa Shared cùng tồn tại và Exclusive bị từ chối khi còn Shared; Exclusive chờ đủ thời gian chờ rồi được cấp sau khi Shared giải phóng; hai DbContext dùng chung một giao dịch và rollback cả hai module; lỗi ở module thứ hai làm rollback ghi của module thứ nhất; mở ngày được tuần tự hóa; phát lại cùng khóa idempotency trả đúng ngày cũ và payload khác bị từ chối; nhiều lần thử đồng thời cùng khóa chỉ tạo một ngày; CSDL từ chối ngày hoạt động thứ hai ngay cả khi bỏ qua service.

### Điều đã và chưa chứng minh

- **Đã chứng minh (trên SQL Server 2022 thật, nhiều lần lặp):** `sp_getapplock` với `LockOwner=Transaction` cho phép Shared cùng tồn tại, Exclusive phải chờ Shared, và rollback giải phóng khóa; hai DbContext của hai module dùng chung được một giao dịch và rollback cùng nhau; mở ngày an toàn khi chạy đồng thời.
- **Chưa chứng minh:** luồng chốt ngày thật (PHU-5.2) và các ca "hai người chốt chỉ ra một snapshot", "ghi muộn chờ rồi bị từ chối" ở mức nghiệp vụ, vì chốt ngày chưa được triển khai (tuần 5). Spike mới kiểm tra cơ chế khóa và giao dịch, không phải kiểm thử tải.
- **CI đã xác minh:** [run 44386503780 trên repo nhóm](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44386503780) đạt 20 lượt × 9 test SQL Server, tổng 180/180. Xem [đối chiếu TRX độc lập](reviews/c03-hai-2026-11-19.md).

### Kết luận kỹ thuật và xác nhận nghiệm thu

**ĐẠT** cho phạm vi spike: cơ chế khóa ứng dụng Shared/Exclusive theo giao dịch kết hợp hai DbContext dùng chung giao dịch là khả thi trên SQL Server 2022, không phát hiện lỗi qua 20 lượt lặp. Chưa cần chuyển sang phương án trạng thái chờ bền vững. Các ca chốt ngày hoàn chỉnh vẫn phải được kiểm thử ở PHU-5.2.

Kết quả kỹ thuật đã có bằng chứng CI. Hiếu (`Trunghieu220504`) đã duyệt [PR #68 về bộ chạy và bằng chứng nghiệm thu](https://github.com/Trunghieu220504/restaurant-management-system/pull/68) ngày 19/11. Review GitHub có trạng thái APPROVED và không có nội dung ký riêng cho kết luận C03. Hai ô xác nhận bên dưới dành cho người phụ trách.

- Người ký (PHU-1.1): ______________________ Ngày: ____ / ____ / 2026
- Người review (Hiếu hoặc Hải): ______________________ Ngày: ____ / ____ / 2026
