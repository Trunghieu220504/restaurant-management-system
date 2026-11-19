# Kiểm tra Auth và phân quyền frontend của Bảo Anh

Cập nhật 17/11/2026. Nhánh gửi review:
`codex/web-auth-refresh-verification`. Kiểm chứng ban đầu lúc 14:03 trên
`codex/web-refresh-week1-acceptance`, từ `develop` tại `6ec9a93`.
Tiếp nối công việc trong [biên bản tuần 1](week-one-acceptance.md).

**Kiểm tra cục bộ đạt:** 48/48 unit test, 17/17 browser test trên bundle
production; build, lint và format đạt. Đã tái hiện và sửa lỗi refresh đến muộn
làm thay đổi token trong bộ nhớ của phiên đã kết thúc hoặc phiên mới.
Phép thử âm của bước kiểm tra OpenAPI phát hiện lệch đúng kỳ vọng.

Các browser test dùng API mock, không dùng tài khoản thật. Sáu ca E2E staging,
thu hồi token/cookie thực tế và lần chạy CI remote vẫn chưa được nghiệm thu.
Bản thay đổi được chuẩn bị để gửi PR vào `develop` theo yêu cầu người dùng;
chưa merge hoặc deploy. Khi đồng bộ với PR #40 của Hải, giữ một khai báo build
arg refresh và đặt mặc định `true` theo yêu cầu; CI/compose vẫn truyền rõ `true`.

## 1. Kết quả theo yêu cầu

| Hạng mục | Kết quả cục bộ | Căn cứ |
|---|---|---|
| Đăng nhập | PASS | Mật khẩu sai hiện lỗi chung; login thành công tới `/sales-day`; unit kiểm tra token chỉ trong bộ nhớ |
| Đăng xuất | PASS phía client | Logout về login; vào lại route bảo vệ bị chuyển về login; lỗi server không tự xóa token; không nhận lại token từ refresh cũ |
| Đổi mật khẩu | PASS phía client | Chặn mật khẩu ngắn/xác nhận khác; 400 hiện lỗi chung, không lộ chi tiết validator; 429 khóa nút gửi; 204 về login; không nhận lại token từ refresh cũ |
| Menu/route theo quyền | PASS | Không có quyền thì ẩn menu và chặn URL trực tiếp của 7 khu vực, kể cả tài khoản có nhãn Admin; có quyền được vào menu/báo cáo |
| Quyền bị thu hồi khi đang mở trang | PASS phía client | Khi `/me` trả danh sách quyền rỗng sau focus, link menu biến mất và trang đang mở hiện 403 |
| Refresh khi access bị API từ chối | PASS với 401 giả lập | Refresh và retry thành công, giữ trang đang mở; không coi đây là test JWT thật hết hạn sau 15 phút |
| Nhiều request đồng thời | PASS | 5 request bị 401 đồng thời chỉ phát sinh 1 lần refresh mới; cả 5 retry thành công; unit có cả trường hợp 401 đến muộn |
| Refresh nhiều tab | PASS ở mức unit của Web Locks | Kiểm tra client xin cùng khóa Web Locks; chưa thay cho nghiệm thu 2 browser/tab thật với cookie staging |
| OpenAPI khớp client | PASS | Sinh từ `docs/api/openapi.json`, diff client trả 0 |
| OpenAPI bị lệch | PASS phép thử âm | Thêm trường tạm vào fixture → sinh lại → cùng lệnh diff dùng trong CI trả 1; phục hồi bản chuẩn → diff trả 0 |

7 khu vực được kiểm tra chặn URL: `/tables`, `/orders`, `/menu`, `/inventory`,
`/kitchen`, `/reports`, `/staff`. `/more`, hồ sơ/đổi mật khẩu và trang ngày bán
vẫn mở cho tài khoản đã xác thực theo quy tắc hiện có; thao tác mở ngày cần
`salesday.manage`. Không thay danh mục quyền hoặc nghiệp vụ backend.

## 2. Lỗi refresh đến muộn và bản sửa

Đã viết test trước khi sửa và tái hiện 3 ca thất bại:

1. Refresh đang chờ, logout thành công, refresh mới trả về: token cũ lại được
   ghi vào bộ nhớ.
2. Tương tự sau đổi mật khẩu thành công: token từ refresh cũ lại được ghi.
3. Refresh lỗi của phiên trước trả về sau một login thành công: xóa token của
   lần login mới.

`auth-client.ts` hiện theo dõi phiên hiện tại bằng một số phiên bản trong bộ
nhớ. Login, logout và đổi mật khẩu thành công đổi phiên bản. Refresh kiểm tra
phiên bản trước khi gửi và khi nhận kết quả; response cũ không được ghi/xóa
token hiện tại. `authFetch` cũng không retry hoặc báo hết phiên từ request của
lần đăng nhập trước. Promise refresh được dùng chung trong cùng phiên; promise
cũ hoàn tất không được xóa khóa refresh mới.

Bổ sung 5 unit case: hai ca kết thúc phiên, hai loại response cũ (200/401) sau
login mới, và giữ token khi logout lỗi server. Toàn bộ 17 test của auth client
đạt sau sửa; toàn bộ frontend có 48 test đạt.

Bản sửa chỉ bảo vệ trạng thái client trong bộ nhớ. Việc cookie `HttpOnly`
xoay vòng và thu hồi phiên phải được backend kiểm chứng riêng. Không tuyên bố
đã nghiệm thu thu hồi token/cookie thật hoặc mọi tình huống nhiều thiết bị.

## 3. Xác nhận bước CI kiểm tra lệch OpenAPI

Workflow `.github/workflows/web-ci.yml` có job `Verify API Client Sync`:
checkout → `npm ci` → `npm run gen:api` →
`git diff --exit-code web/src/lib/api/generated/`.
Job build/test phụ thuộc job này qua `needs: verify-api-client-sync`.

Phép thử cục bộ dùng cùng generator và lệnh diff, truyền một bản sao OpenAPI
có thêm trường `LoginRequest.codexDriftProbe`. Không sửa file OpenAPI gốc.
Các file sinh được lưu bản gốc và phục hồi trong `finally`; sau đó sinh lại từ
spec chuẩn và kiểm tra diff sạch.

```text
Baseline generated client diff: exit 0
Generator with temporary changed OpenAPI: exit 0
CI diff with drift: exit 1 (phát hiện trường mới)
Canonical generator: exit 0
CI diff after restore: exit 0
```

[Log kiểm tra OpenAPI](evidence/week-one/openapi-drift-2026-11-17.json).
Đây là bằng chứng tái hiện cơ chế chặn của CI, không phải một GitHub Actions
run mới. Khi nhóm tích hợp, Hải cần lấy link run remote và kiểm tra required
checks/branch protection riêng; không suy ra chúng từ kết quả cục bộ.

## 4. Lệnh kiểm tra và bằng chứng

Chạy từ `web/`, Node 22.16.0 portable khớp dòng Node 22 của Docker/CI:

```powershell
npm test
npm run lint
npm run format:check
$env:NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED = 'true'
npm run build
```

Kết quả: 8 file test, 48 test PASS; lint 0 lỗi/41 cảnh báo sẵn có;
format PASS; Next.js production build PASS, TypeScript PASS, 23 trang được sinh.

Trong process mới, không đặt cờ refresh runtime, không đặt URL staging:

```powershell
$env:PLAYWRIGHT_JSON_OUTPUT_FILE = '..\docs\evidence\week-one\auth-production-2026-11-17.json'
npm run test:e2e:production -- --reporter=list,json
```

Kết quả cuối: **17 expected, 0 unexpected, 0 skipped, 0 flaky**, 11,2 giây.
[Report Playwright](evidence/week-one/auth-production-2026-11-17.json).
Máy đã cài Chromium; lần cài đầu có thể chạy `npx playwright install chromium`.
Trace/HAR không được bật trong bộ test này.

Lần chạy browser đầu có 14 PASS/3 FAIL vì locator `role=alert` trùng với
route announcer của Next.js. Đã sửa test để chọn thông báo bằng nội dung cụ thể;
không thay giao diện vì lỗi locator. Lần chạy lại toàn bộ 17 test đạt.
Local server vẫn in cảnh báo Next khuyến nghị standalone; Dockerfile chạy
standalone bằng `node server.js` như trước.

Các test production nằm trong `web/e2e-production/`; bước smoke image đã thêm
ở workflow sẽ chạy cả bộ này sau khi build Docker với refresh=true. Chưa chạy
Docker/CI remote vì máy không có Docker và chưa push thay đổi.

## 5. Phần cần nhóm hoàn tất

Giữ sáu ca E2E staging trong biên bản tuần 1 ở BLOCKED đến khi Hải/Hiếu xác
nhận image/commit mới, API/migration refresh và cung cấp tài khoản test riêng.
Đặc biệt còn thiếu hết hạn JWT thật, cookie xoay vòng, dùng lại token sau
logout và đổi mật khẩu thu hồi phiên trên thiết bị khác.

Không thay kết quả staging bằng API mock hoặc thời gian giả lập của trình
duyệt. Chưa có số giờ gốc/thực tế để chốt chênh lệch >20%; ước lượng tuần 2
vẫn là đề xuất trong biên bản trước.
