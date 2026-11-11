# Spike C03 tuần 1

Trạng thái: các kịch bản C03 hiện có đã đạt trên SQL Server 2022 chạy trong Docker ngày 10/11/2026. File TestResults/PhuongWeek1/phuong-week1.trx xác nhận 5/5 test tuần 1 đạt, không lỗi và không bỏ qua. Kết quả này xác nhận các kịch bản đã viết; chưa thay thế nghiệm thu CI/staging hoặc kiểm thử tải.

`PhuongWeekOneSqlServerTests` kiểm tra hai DbContext SalesDay và Catalog cùng SqlConnection/SqlTransaction, UseTransactionAsync cho mỗi context, ghi cả hai module rồi rollback và đọc lại bằng context độc lập để kiểm tra không còn dữ liệu. Khóa ứng dụng dùng LockOwner=Transaction.

Kịch bản khóa kiểm tra hai Shared cùng tồn tại, Exclusive bị timeout khi Shared còn giữ, Exclusive được cấp sau rollback, Exclusive thứ hai bị từ chối khi người thứ nhất giữ khóa rồi lấy được sau giải phóng. Test mở ngày đồng thời còn kiểm tra trạng thái nghiệp vụ sau khi chờ khóa: chỉ một ngày được tạo.

Chạy:

```powershell
dotnet test tests/Restaurant.IntegrationTests -c Release --filter FullyQualifiedName~PhuongWeekOneSqlServerTests
```

Cần IDENTITY_TEST_SQLSERVER trỏ đến SQL Server thử nghiệm. Đã chạy thành công qua scripts/Test-PhuongWeek1.ps1 từ terminal của người dùng. Nhóm cần chạy lại trên CI và review trước khi áp dụng khóa Shared/Exclusive vào các module tuần sau. Nếu các kịch bản bổ sung thất bại, xem phương án trạng thái chờ bền vững trong thiết kế dữ liệu.
