# Backend Foundation Implementation Plan

**Goal:** Base .NET 10 modular monolith và GitHub Flow có thể build/test.
**Architecture:** Một host, mười module với bốn lớp tách project và DbContext/schema riêng.
**Tech Stack:** .NET 10, EF Core SQL Server, ASP.NET Core Identity/JWT, xUnit.
**Spec:** ../specs/2026-10-29-backend-foundation-design.md

## Tasks
- [x] Tạo cấu hình SDK/package, test kiểm tra dependency graph; xác nhận test đỏ.
- [x] Tạo 40 project module, đăng ký 10 DbContext/schema/history và Identity store.
- [x] Tạo API host, JWT validation, health checks, OpenAPI và integration tests.
- [x] Tạo GitHub Actions, PR template, hướng dẫn Git và database/migration.
- [x] Restore, build Release, test, format, smoke HTTP; cập nhật README và kết quả.

## Review focus
- Project reference xuyên module hoặc từ lớp trong sang framework phải bị test chặn.
- JWT thiếu/không hợp lệ, role sai không được truy cập endpoint quản trị.
- Schema của model/history đúng module; không trộn các EF model.
- Startup không kết nối/mutate DB; readiness không báo khỏe khi SQL không truy cập được.
- Tài liệu không nhầm scaffold với nghiệp vụ hay branch protection đã kích hoạt.

## Verification
dotnet restore RestaurantManagement.slnx --locked-mode
dotnet build RestaurantManagement.slnx -c Release --no-restore
dotnet test RestaurantManagement.slnx -c Release --no-build
dotnet format whitespace RestaurantManagement.slnx --verify-no-changes --no-restore
