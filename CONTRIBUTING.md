# Quy trình Git và đóng góp

## Nhánh và Pull Request
Dự án dùng GitHub Flow: `main` là nhánh ổn định, nhánh công việc ngắn được đưa về
`main` qua Pull Request. Không cần thêm nhánh `develop` khi chỉ có một luồng phát hành.

Quy ước tên:
- `feature/<module>-<noi-dung>`
- `fix/<module>-<loi>`
- `chore/<cong-viec>`
- `docs/<tai-lieu>`

Ví dụ bắt đầu công việc:

```bash
git switch main
git pull --ff-only origin main
git switch -c feature/catalog-menu
```

Chỉ stage các file liên quan, kiểm tra diff rồi commit:

```bash
git add src/Modules/Catalog tests
git diff --cached --check
git diff --cached
git commit -m "feat(catalog): add menu use case"
git push -u origin feature/catalog-menu
```

Tạo PR vào `main`, mô tả hành vi, dữ liệu và kiểm chứng theo template.
Merge bằng squash sau khi CI đạt và đã xem xét thay đổi. Sau merge, cập nhật main;
chỉ xóa nhánh khi xác nhận công việc đã được đưa vào main.

## Quy ước commit
Dùng Conventional Commits: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `ci`.
Scope là module hoặc thành phần, ví dụ `fix(billing): prevent duplicate refunds`.
Mỗi commit nên là một thay đổi có thể đọc và kiểm chứng độc lập.

## Kiểm tra trước PR
```bash
dotnet restore RestaurantManagement.slnx --locked-mode
dotnet build RestaurantManagement.slnx -c Release --no-restore
dotnet test RestaurantManagement.slnx -c Release --no-build --no-restore
dotnet format whitespace RestaurantManagement.slnx --verify-no-changes --no-restore
git diff --check
```

Sau khi đổi dependency, chạy restore không có `--locked-mode`, xem diff và commit
`Directory.Packages.props` cùng các `packages.lock.json` liên quan.

## GitHub Actions
Workflow `Backend CI` chạy restore khóa phiên bản, format, build, test và publish
artifact. Không deploy hoặc chạy migration vào database thật. PR dùng quyền
`contents: read`; không dùng `pull_request_target`, không cần secret để test.

Actions được pin theo commit; Dependabot đề xuất cập nhật dependency/actions.
Không merge tự động các bản nâng cấp chưa được kiểm chứng.

## Bảo vệ main
**File workflow không tự bật branch protection.** Sau khi workflow đã được push và
chạy trên GitHub, cấu hình Settings → Rules → Rulesets cho `main`:

- Yêu cầu Pull Request trước merge.
- Yêu cầu status check `Build and test` và nhánh cập nhật trước merge.
- Chặn force push và xóa main; yêu cầu giải quyết conversation.
- Dùng squash merge. Với nhóm từ hai người, yêu cầu ít nhất một approval.
- Khi làm một mình, không bắt buộc một approval từ người khác vì tác giả không thể
  tự approve PR; vẫn giữ PR và CI.

Các mục này là hướng dẫn cấu hình, chưa phải cài đặt đã được kích hoạt trên GitHub.
