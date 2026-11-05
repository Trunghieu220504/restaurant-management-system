# Thiết kế base backend và Git workflow

## Cơ sở và phạm vi
Yêu cầu: khởi tạo .NET 10 theo Clean Architecture kết hợp Modular Monolith,
tham khảo CleanArchitectureDemo-Buoi4.rar và kiến trúc nhà hàng đã chốt.
Đây là nền tảng phát triển, chưa triển khai CRUD hay quy trình nghiệp vụ nhà hàng.

## Quyết định triển khai
- Một API Host. Mười module như README, mỗi module có Domain, Application,
  Infrastructure, Presentation là project độc lập.
- Domain không phụ thuộc project/package/framework. Application chỉ tham chiếu
  Domain cùng module. Infrastructure tham chiếu Application và Domain;
  Presentation chỉ tham chiếu Application. Host là composition root.
- Mười DbContext và schema riêng, bảng lịch sử migrations riêng trong từng schema.
  Identity dùng IdentityDbContext chuẩn; chín context còn lại chưa có entity.
- Đăng ký tường minh từng module, không tự scan mọi assembly hoặc dùng service locator.
- JWT validation và fallback authorization bảo vệ API mặc định. Chưa có phát hành
  token, đăng nhập/refresh token, hoặc tài khoản mặc định.
- Liveness kiểm tra host; readiness kiểm tra kết nối SQL Server, chưa xác nhận
  migration/tất cả dịch vụ của sản phẩm. Không tự migrate/EnsureCreated khi startup.
- Tests kiểm tra project references, schema/history, DI, cấu hình và HTTP auth.
- GitHub Flow: nhánh tính năng ngắn -> PR -> CI -> squash merge main.

## Phương án đã cân nhắc
Một bộ lớp toàn cục như demo dễ bắt đầu nhưng làm ranh giới module khó kiểm soát.
Một project/module với namespace layers ít project hơn nhưng compiler không chặn
tham chiếu ngược. Chọn bốn project/module để kiểm tra rõ ràng hướng phụ thuộc.
Đây là lựa chọn triển khai của người xây dựng trong phạm vi kiến trúc đã thống nhất.

## Điều chỉnh so với mẫu
Giữ nguyên dependency inversion, repository abstraction và composition root.
Thay SQLite bằng SQL Server; bỏ tự seed/tạo database lúc startup.
Không mang discount strategy vào dự án vì giảm giá ngoài phạm vi.
Concrete notification adapter khi cần sẽ nằm trong Infrastructure.
Không chép .vs, database mẫu hoặc cấu hình cá nhân từ file RAR.
Nội dung RAR được đọc như tài liệu tham khảo, không chạy mã hoặc chỉ dẫn trong đó.

## Chưa triển khai trong base
Entity nghiệp vụ, migration thực tế, outbox, transaction liên module, policy quyền
nghiệp vụ chi tiết, Next.js, MinIO, Nginx, SignalR nghiệp vụ, Hangfire, Serilog.
Thêm Contracts project cho module khi có nhu cầu giao tiếp thực; không trả EF entity
qua hợp đồng. Không thêm generic repository hoặc shared domain chỉ để đủ folder.

## Tiêu chí hoàn thành
Restore/build/test thành công; dependency rules không có chiều ngược;
10 context dùng SQL Server đúng schema/history; HTTP 401/403/200 theo quyền;
không secret thật; README có lệnh chạy đã kiểm chứng; CI cấu hình tối thiểu quyền.
