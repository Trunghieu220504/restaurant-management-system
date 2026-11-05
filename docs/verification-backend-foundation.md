# Kết quả kiểm chứng base backend

Ngày: 29/10/2026. Môi trường: Windows, SDK 10.0.300, runtime 10.0.8.
Nhánh làm việc: `chore/backend-foundation`.

| Hạng mục | Kết quả |
| --- | --- |
| Restore với `--locked-mode` | Thành công, 43 project có lockfile |
| Build Release | Thành công, 0 warning và 0 error |
| Architecture tests | 2/2 đạt |
| Integration tests | 24/24 đạt |
| `dotnet format whitespace --verify-no-changes --no-restore` | Thành công |
| Publish API Release | Thành công |
| HTTP trên API đã publish | Liveness 200, OpenAPI 200, endpoint thiếu JWT 401 |
| EF CLI `dbcontext info` cho Catalog | SQL Server, schema lịch sử `catalog.__EFMigrationsHistory` |
| Review độc lập mã/config/tài liệu | Không ghi nhận lỗi cần sửa trong phạm vi scaffold |

Architecture tests đã chạy thất bại trước khi tạo cấu trúc module và đạt sau khi
triển khai. Integration tests kiểm tra chữ ký/issuer/audience/hạn token, quyền Admin,
OpenAPI theo môi trường, health checks, DI, model/schema và migration history của
10 context. Identity có model bảy bảng chuẩn.

NuGet audit ban đầu chặn restore vì OpenAPI/Pkcs gián tiếp có cảnh báo. Đã pin bản
vá trong Directory.Packages.props và restore lại thành công, không tắt audit.

Giới hạn kiểm chứng:

- Chưa tạo/chạy migration hoặc kiểm thử transaction với SQL Server thật.
- Chưa có chức năng nghiệp vụ, endpoint đăng nhập, refresh token hoặc frontend.
- Workflow đã có trong repo và các bước .NET đã chạy local; chưa chạy trên GitHub
  Actions và chưa bật branch protection trên GitHub.
- Smoke process được dừng sau kiểm tra; không để lại server nền.
- Sandbox Windows chặn EventLog mặc định của ASP.NET Core, nên integration tests
  cuối cùng được chạy ngoài sandbox sau khi cấp quyền.
