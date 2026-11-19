# Nhật ký kiểm tra ngày 17 tháng 11 năm 2026

Nhánh `codex/web-refresh-week1-acceptance`, dựa trên `origin/develop` `6ec9a93`.
Không commit, push hoặc deploy. Không có tài khoản/secret trong bằng chứng.

Kiểm tra cuối: `git diff --check` đạt; YAML workflow/compose parse được bằng
`js-yaml`, cờ build arg và bước smoke đều có. Lock file, client sinh tự động và
`next-env.d.ts` không có diff ngoài phạm vi.

## Kiểm tra mã nguồn

Từ `web/`, chạy npm portable và các CLI tương ứng với script trong package.json:

```text
npm ci --no-audit --no-fund
added 569 packages

node node_modules/eslint/bin/eslint.js .
41 problems (0 errors, 41 warnings), exit 0

node node_modules/vitest/vitest.mjs run
Test Files 8 passed (8)
Tests 43 passed (43), exit 0

node node_modules/prettier/bin/prettier.cjs --check .
All matched files use Prettier code style!, exit 0

node node_modules/typescript/bin/tsc --noEmit
exit 0

node scripts/gen-api.mjs
Successfully generated API types
git diff --exit-code -- src/lib/api/generated/
exit 0
```

## Bundle production cục bộ

Build bằng Node 22.16.0 và npm đi kèm, cờ chỉ bật lúc build:

```powershell
$env:NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED = 'true'
npm run build
```

```text
Next.js 16.4.6 (Turbopack)
Compiled successfully in 4.8s
Finished TypeScript in 9.8s
Generating static pages (23/23)
exit 0
```

Chạy trong một process khác, không đặt `NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED`
hay `PLAYWRIGHT_BASE_URL`, Chromium đã cài. API được mock trong test:

```text
npm run test:e2e:production
ok production bundle restores a session through refresh
ok production bundle refreshes after a protected API returns 401
2 passed (7.1s), exit 0
```

Trước đó, gọi Next CLI trực tiếp lỗi Turbopack/PostCSS khi tạo tiến trình;
`next build --webpack` lỗi pure selector `:global(body *)` trong CSS hiện có.
Không sửa CSS/bundler; lệnh chuẩn npm ở trên đã thành công. Không suy ra nguyên
nhân chỉ từ việc retry thành công. Local server cảnh báo khuyến nghị chạy
standalone; Dockerfile vẫn dùng standalone như trước.

## Kiểm tra bundle staging cũ

Không đăng nhập, không gửi dữ liệu thật. Smoke chặn API bằng mock trong browser:

```powershell
$env:PLAYWRIGHT_BASE_URL = 'https://staging.quanlynh.id.vn'
npm run test:e2e:production
```

```text
production bundle restores a session through refresh: FAIL
production bundle refreshes after a protected API returns 401: FAIL
getByLabel('Tiền mặt đầu ngày'): not found, vẫn ở trang đăng nhập
2 failed, exit 1
```

Kết quả khớp với
[quan sát không mock](staging-refresh-2026-11-17.json): login HTTP 200, cờ vẫn đọc
runtime trong JS và không có request refresh tự động. Không coi smoke này là
nghiệm thu Auth/SQL thật. Sáu ca E2E vẫn BLOCKED như biên bản chính.

## Giới hạn môi trường

`dotnet test tests/Restaurant.ArchitectureTests/Restaurant.ArchitectureTests.csproj -c Release`
không chạy được vì thiếu SDK 10.0.300. Không có Docker CLI/Engine để chạy image;
workflow mới chưa được chạy remote. Không thay global.json, lock file hoặc
backend để vượt qua các giới hạn này.
