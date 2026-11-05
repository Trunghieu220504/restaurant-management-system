# Quy trình Git và đóng góp

## Nhánh và Pull Request
Dự án dùng mô hình hai nhánh dài hạn:

- `main`: nhánh ổn định, chỉ chứa code đã chạy ổn định trên `develop` và sẵn sàng phát hành hoặc triển khai.
- `develop`: nhánh phát triển chung. Mọi chức năng mới được tích hợp vào đây trước.

Thành viên không commit trực tiếp vào `main` hoặc `develop`. Mỗi chức năng làm trên một nhánh
ngắn hạn tách từ `develop`, đẩy lên GitHub rồi tạo Pull Request vào `develop`.

```text
feature/* , fix/* , chore/* , docs/*  --PR-->  develop  --PR (khi ổn định)-->  main
```

Quy ước tên:
- `feature/<module>-<noi-dung>`
- `fix/<module>-<loi>`
- `chore/<cong-viec>`
- `docs/<tai-lieu>`
- `hotfix/<loi>`: chỉ cho lỗi khẩn trên `main`, xem mục Hotfix.

### Làm một chức năng
```bash
git switch develop
git pull --ff-only origin develop
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

Tạo PR **vào `develop`**, mô tả hành vi, dữ liệu và kiểm chứng theo template. Merge bằng squash
sau khi CI đạt và có ít nhất một thành viên khác xem xét. Sau merge, cập nhật `develop` cục bộ;
chỉ xóa nhánh feature khi xác nhận công việc đã vào `develop`. Nếu `develop` đã có thay đổi mới,
cập nhật nhánh feature từ `develop` (rebase hoặc merge) trước khi merge PR.

### Đưa code lên main
Khi `develop` đã ổn định (CI đạt, các chức năng của đợt đã được kiểm tra thủ công hoặc nghiệm thu),
tạo PR từ `develop` vào `main`. Merge bằng **merge commit** (không squash) để giữ lịch sử các PR
feature và để `develop` không lệch khỏi `main`. Chỉ người phụ trách phát hành (TV1) merge PR này.
Sau khi vào `main`, gắn tag phiên bản (ví dụ `v0.1.0`) khi cần đánh dấu mốc.

### Hotfix
Lỗi khẩn trên `main`: tách `hotfix/<loi>` từ `main`, PR vào `main`, sau đó merge `main` ngược
vào `develop` để hai nhánh không lệch nhau.

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
Workflow `Backend CI` chạy khi push lên `main`, `develop` và các nhánh làm việc, và khi có PR
vào `main` hoặc `develop`. Workflow gồm restore khóa phiên bản, format, build, test và publish
artifact. Không deploy hoặc chạy migration vào database thật. PR dùng quyền
`contents: read`; không dùng `pull_request_target`, không cần secret để test.

Actions được pin theo commit; Dependabot đề xuất cập nhật dependency/actions.
Không merge tự động các bản nâng cấp chưa được kiểm chứng.

## Bảo vệ nhánh
**File workflow không tự bật branch protection.** Sau khi workflow đã được push và
chạy trên GitHub, cấu hình Settings → Rules → Rulesets cho **cả `main` và `develop`**:

- Yêu cầu Pull Request trước merge; không push trực tiếp.
- Yêu cầu status check `Build and test` và nhánh cập nhật trước merge.
- Chặn force push và xóa nhánh; yêu cầu giải quyết conversation.
- `develop`: cho phép squash merge, yêu cầu ít nhất một approval từ người khác.
- `main`: chỉ cho phép merge commit từ PR `develop` → `main`; giới hạn người được merge.
- Khi làm một mình, không bắt buộc approval vì tác giả không thể tự approve PR; vẫn giữ PR và CI.
- Đặt `develop` làm default branch của repo để PR mới mặc định trỏ vào `develop` (tùy chọn).

Các mục này là hướng dẫn cấu hình, chưa phải cài đặt đã được kích hoạt trên GitHub.
