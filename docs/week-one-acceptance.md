# Nghiệm thu Auth và Frontend tuần 1

Cập nhật ngày 17/11/2026, thay mặt Bảo Anh, phối hợp Hải và Hiếu.
Mã nguồn đối chiếu: `origin/develop` tại `6ec9a93`; nhánh sửa:
`codex/web-auth-refresh-verification` (kiểm tra ban đầu trên
`codex/web-refresh-week1-acceptance`). Bản thay đổi dành cho PR vào `develop`;
không merge hoặc deploy trong task này.

**Bổ sung 14:03 ngày 17/11:** [kiểm tra Auth và phân quyền](baoanh-auth-verification.md)
đã đạt 48 unit test và 17 browser test production với API mock; sửa thêm lỗi
refresh đến muộn và kiểm chứng phép thử lệch OpenAPI. Các số 43/2 trong mục 6
là kết quả lượt đầu, được giữ để truy vết; sáu ca staging vẫn BLOCKED.

## Cập nhật BAO-2.0 ngày 18/11/2026

**BAO-2.0 — Xong.** Đã nghiệm thu cả ba tiêu chí trên staging với API thật.

**Đầu ra đã có.** [PR #41](https://github.com/Trunghieu220504/restaurant-management-system/pull/41)
đã merge vào `develop` tại `79a8984`. [Biên bản triển khai của Hải](reviews/week1-acceptance.md#7-bằng-chứng-staging--hải-18112026)
ghi nhận web image tại commit `7de77d4`, đã bao gồm PR #41. Workflow publish
build web với `NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true`. Lúc 13:35:55 giờ
Việt Nam, Edge mở `/login` staging nhận 200 và bundle tự gửi
`POST /api/identity/refresh`. API staging trả 401 đúng kỳ vọng với trình duyệt
chưa có cookie. Hành vi này xác nhận refresh đã bật trên bản web đang phục vụ.

| Tiêu chí “Xong khi” | Trạng thái | Bằng chứng |
|---|---|---|
| Đăng nhập thật qua giao diện staging | PASS | Lúc 15:49:18 giờ Việt Nam, `POST /api/identity/login` trả 200 và UI chuyển đến `/sales-day`. |
| Qua 15 phút, còn refresh hợp lệ thì giữ phiên | PASS | Sau 15 phút 37 giây, 7 request với access token hết hạn nhận 401. Refresh trả 200, cả 7 lần gửi lại trả 200. UI vẫn ở `/sales-day`, không chuyển về `/login`. |
| Nhiều request đồng thời chỉ gọi refresh một lần | PASS | Đợt 7 request `GET /api/identity/me` đồng thời phát sinh đúng 1 `POST /api/identity/refresh`, trả 200. |

Lượt đạt chạy bằng Playwright/Edge trong một tab. Access token hết hạn lúc
16:04:18, đợt request được gửi lúc 16:04:55 và hoàn tất lúc 16:04:57, giờ Việt
Nam. Công cụ giữ các request `/me` ngay trước thời điểm hết hạn, tạo thêm request
qua sự kiện `focus` rồi gửi cả đợt đến API staging. Phản hồi lấy trực tiếp từ
staging. Cuối lượt, `POST /api/identity/logout` trả 204.

Ba ca trong `web/e2e-production/refresh.spec.ts` đều đạt khi chạy bằng
Playwright/Edge với bundle staging và API mock. Bộ unit test `auth-client.test.ts`
đạt 17/17.

Các mục dưới đây lưu kết quả ban đầu ngày 17/11, trước khi image có PR #41
được triển khai. Mục 4 lưu bảng E2E tuần 1 theo phạm vi và trạng thái của lần
ghi nhận ngày 17/11.

## 1. Đối chiếu tài liệu và phạm vi

Đã đọc nội dung tài liệu Word “Phân tích và thiết kế hệ thống quản lý nhà hàng (1)”
do người dùng cung cấp: UC01 yêu cầu logout thu hồi phiên; UC25 yêu cầu không mở
ngày khi còn ngày chưa chốt; phần 8 phân biệt kế hoạch với kết quả kiểm chứng.
Các đề xuất còn ghi CẦN CHỐT trong Word không thay thế C01–C06 đã duyệt ở repo.
Không đổi nghiệp vụ tiền, kho hoặc quyền trong lần sửa này.

| Hạng mục | Thực tế đối chiếu bằng git/code ngày 17/11 |
|---|---|
| Refresh backend | PR #26 đã merge (`17244f5`); hợp đồng trong `identity-refresh-handoff.md` |
| Frontend Auth/SalesDay | PR #24, #27 đã merge; `/sales-day` dùng API thật; các trang prototype khác vẫn có mock |
| PR #17 | Đã merge (`a13992e`); không còn chờ merge |
| OpenAPI | PR #18 đã merge; generator đọc `docs/api/openapi.json`; Web CI kiểm tra lệch |
| Docker/compose | PR #23 đã merge; có `web/Dockerfile`, `infra/compose.frontend.yml` |
| CI frontend | Có `.github/workflows/web-ci.yml`; trước lần sửa này chưa build Docker web |
| ADR-01/nền tảng | PR #29 và #36 đã merge; không đồng nghĩa đã nghiệm thu staging |
| Phát hành GHCR/CD | Chưa thấy workflow publish/deploy web trong checkout này; Hải cần đối chiếu quy trình thực tế |

## 2. Build và xác nhận image cùng Hải

Dockerfile builder bổ sung `ARG NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true` và
`ENV NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=$NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED`
trước `npm run build`. `.dockerignore` loại `.env.*`, nên không dựa vào `.env`
trên VPS để bật cờ trong image. Compose frontend cũng truyền build arg rõ ràng.

Workflow Web CI được sửa để build Next.js với cờ `true`, build image với
`--build-arg NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true`, chạy image, rồi kiểm tra
bundle bằng trình duyệt. API trong smoke test này là mock; kết quả không thay
cho E2E staging. Workflow không publish image hoặc tự deploy.

Chạy từ gốc repo, sau khi API refresh và migration đã sẵn sàng:

```sh
docker build --build-arg NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true -t restaurant-web:staging web
```

Với cấu hình compose có sẵn (đúng file môi trường của Hải trên VPS):

```sh
docker compose --env-file infra/.env -f infra/compose.backend.yml -f infra/compose.backend.https.yml -f infra/compose.frontend.yml build --build-arg NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true web
docker compose --env-file infra/.env -f infra/compose.backend.yml -f infra/compose.backend.https.yml -f infra/compose.frontend.yml up -d --no-deps web
```

Hải ghi commit/image ID thực sự đã deploy và thời điểm; kiểm tra trang `/login`
và `/health/ready`. Kiểm tra CDN/cache nếu vẫn tải chunk cũ. Không thay đổi TTL
token hay migrate DB trong bước kiểm tra frontend này.

Xác nhận bundle của image bằng smoke test (trên máy đã `npm ci` và cài Chromium):

```sh
docker run --detach --name web-refresh-smoke --publish 127.0.0.1:3100:3000 restaurant-web:staging
cd web
PLAYWRIGHT_BASE_URL=http://127.0.0.1:3100 npm run test:e2e:production
docker rm --force web-refresh-smoke
```

Trong PowerShell dùng `$env:PLAYWRIGHT_BASE_URL = 'http://127.0.0.1:3100'` rồi
`npm run test:e2e:production`. Smoke kiểm tra browser tự gọi refresh để khôi phục
phiên và gọi refresh/retry khi API trả 401. Không truyền cờ refresh lúc chạy
container; vì vậy test bắt được lỗi cờ thiếu trong bundle đã build.

## 3. Bằng chứng trực tiếp staging trước khi sửa được deploy

Lấy bằng HTTP và Playwright Chromium qua mạng ngoài sandbox ngày 17/11/2026,
13:30–13:34 giờ Việt Nam. Lần thử đầu trong sandbox lỗi DNS; retry ngoài sandbox thành công,
không dùng lỗi DNS đó để kết luận staging hỏng.

| Kiểm tra | Kết quả quan sát |
|---|---|
| `GET https://staging.quanlynh.id.vn/login` | HTTP 200; HTML tham chiếu 12 script |
| `POST /api/identity/refresh` không cookie/token | HTTP 401; endpoint phản hồi, chưa chứng minh refresh phiên hợp lệ |
| Chunk chứa Auth khi trình duyệt tải | `/_next/static/chunks/3uy-pa_kfajm4.js` |
| SHA-256 của chunk tải về | `975908a8209751cbc0c26a9d8aea4c704ead151ecca795e14a00aae5c9783599` |
| Giá trị compile | Vẫn còn `a.default.env.NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED` trong hàm refresh |
| Trình duyệt mới, không cookie, không mock | Trang login đã hiện, chờ network idle + 1 giây; 0 request `/refresh` |

[Log trình duyệt và hash bundle](evidence/week-one/staging-refresh-2026-11-17.json),
[ảnh trang đăng nhập](evidence/week-one/staging-login-2026-11-17.png).
HTML tải trực tiếp có 12 script; trình duyệt quan sát 13 script sau khi chạy app.

Đoạn JS đã tải từ staging (không chứa token/mật khẩu):

```js
async function p(){return"true"!==a.default.env.NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED?null:u||(u=h().finally(()=>{u=null}))}
```

Đây là bằng chứng cờ chưa được nhúng thành hằng `true` tại build. Việc endpoint
trả 401 khi thiếu cookie là đúng kỳ vọng; không coi đó là FAIL của backend.
Chưa biết commit/image ID đang chạy trên VPS. Cần kiểm tra lại sau deploy mới.

## 4. Kịch bản E2E thật và bảng kết quả

Điều kiện bắt đầu: Hải/Hiếu xác nhận commit/image mới, migration refresh đã áp
dụng, frontend build cờ true; có tài khoản test riêng với `salesday.manage`.
Không mặc định Admin có quyền mở ngày. Dùng DB/dữ liệu test staging được nhóm
dành riêng; không đóng/xóa ngày có sẵn để ép test chạy.

Mở DevTools Network, bật Preserve log. Ghi method, path, status, thời điểm và
kết quả UI; che Authorization, Cookie/Set-Cookie, mật khẩu và JSON accessToken.
Không lưu HAR/trace chưa lọc hoặc ảnh input mật khẩu. Bằng chứng gồm commit,
image ID, người chạy, thời gian, screenshot UI và log đã che bí mật.

| ID | Bước và tiêu chí PASS | Kết quả hiện tại | Bằng chứng / phần thiếu |
|---|---|---|---|
| W1-E01 | Đăng nhập tài khoản test ở `/login`; login 200, tới `/sales-day`, `/me` đúng người/quyền | BLOCKED | Chờ image sửa cờ, xác nhận Hải/Hiếu và tài khoản test |
| W1-E02 | Khi chưa có ngày mở, nhập tiền đầu ngày và mở; POST open 201, GET current trả đúng ngày `DANG_BAN` | BLOCKED | Như W1-E01; cần dữ liệu ngày sạch |
| W1-E03 | Dùng hai context độc lập cùng tải form trước W1-E02; context còn lại gửi mở với **Idempotency-Key mới**, kỳ vọng 409 `salesday.previous_not_closed`; GET current vẫn cùng ID, không tạo ngày thứ hai | BLOCKED | Không dùng replay cùng khóa để chứng minh chặn trùng; nếu UI tự cập nhật thì gửi request mới qua DevTools trong phiên test |
| W1-E04 | Giữ trang, đợi quá `expiresAt` và clock skew của API (mặc định 15 phút + tối đa 30 giây), bấm tải lại ngày bán; thấy API 401 → POST refresh 200 → retry 200 và vẫn đăng nhập | BLOCKED | Không sửa đồng hồ client để giả lập hết hạn JWT; không dùng mock 401 làm bằng chứng staging |
| W1-E05 | Lưu token/cookie test cũ **chỉ trong bộ nhớ công cụ test**; logout 204 về login; dùng access token cũ gọi me và cookie cũ gọi refresh đều 401 | BLOCKED | Cần chứng minh server thu hồi, không chỉ UI xóa token |
| W1-E06 | Login cùng tài khoản trên hai browser/context A/B; đổi mật khẩu ở A qua `/profile/password`; A phải login lại; access và refresh cũ của B đều 401, thao tác/focus ở B đưa về login; mật khẩu cũ không login được, mật khẩu mới login được | BLOCKED | Đăng nhập lại sau W1-E05; sau test thống nhất mật khẩu test mới với người giữ tài khoản qua kênh riêng |

Nếu test chạy được và lệch kỳ vọng: ghi FAIL, severity, log đã lọc và người xử
lý. BLOCKED/NOT_RUN không phải PASS và không phải lỗi nghiệp vụ đã tái hiện.
Không đánh dấu Done cho BAO-1.4 trước khi W1-E01…E06 đạt trên staging.

## 5. Ước lượng lại phần Bảo Anh và chuyển tuần 2

Repo chỉ có lịch/tiêu chí từng task, chưa có giờ dự kiến và giờ thực tế của
Bảo Anh. Không suy ra giờ làm từ ngày merge hoặc số commit.

| Task tuần 1 | Giờ dự kiến gốc | Giờ thực tế | Chênh lệch >20% | Việc cần bổ sung |
|---|---|---|---|---|
| BAO-1.1 Khung frontend | Chưa cung cấp | Chưa cung cấp | Chưa tính được | Bảo Anh bổ sung timesheet/baseline |
| BAO-1.2 OpenAPI | Chưa cung cấp | Chưa cung cấp | Chưa tính được | Như trên |
| BAO-1.3 Khung ứng dụng | Chưa cung cấp | Chưa cung cấp | Chưa tính được | Như trên |
| BAO-1.4 Auth | Chưa cung cấp | Chưa cung cấp | Chưa tính được | Tách giờ code, tích hợp và nghiệm thu |

Công thức: `(thực tế - dự kiến) / dự kiến × 100%`; báo lệch khi trị tuyệt đối
lớn hơn 20%. Nếu baseline bằng 0 hoặc thiếu thì không tính phần trăm.
Rủi ro đã xác định: cờ build/runtime khác nhau, chưa kiểm chứng image staging,
phụ thuộc backend/migration và điều phối refresh nhiều tab. Đây là nguyên nhân
có thể phát sinh công việc, chưa phải kết luận nguyên nhân chênh lệch giờ thực tế.

Ước lượng **đề xuất**, tính từ lúc đủ API/hợp đồng và quyền truy cập; nhóm chưa
duyệt. Không sửa phạm vi MVP hoặc cam kết deadline từ các số dưới đây.

| Công việc tuần 2 | Giờ đề xuất còn lại | Phụ thuộc / nội dung |
|---|---|---|
| Hoàn tất Auth tuần 1 | 4–6 | Hải deploy image, Hiếu xác nhận; chạy sáu ca và lưu bằng chứng |
| BAO-2.1 Khu vực/bàn | 12–14 | HIE-2.1: API, OpenAPI, xử lý quyền/xung đột và test tích hợp UI |
| BAO-2.2 Thực đơn | 14–16 | PHU-2.1: nhóm/món/biến thể/tùy chọn/cờ hết; ảnh theo kế hoạch |
| Dự phòng tích hợp/sửa lỗi | 4 | Ưu tiên lỗi Auth và tích hợp thật |
| Tổng | 34–40 | Cần xác nhận số giờ khả dụng thực tế của Bảo Anh |

Thứ tự: nghiệm thu Auth còn thiếu → BAO-2.1 → BAO-2.2. Nếu thiếu API thì dùng
mock để tiếp tục UI, nhưng giữ nghiệm thu staging ở BLOCKED. Nhóm điền baseline,
giờ thực tế, nguyên nhân đã xác nhận và người duyệt trước khi chốt biên bản.

## 6. Kiểm tra cục bộ và bàn giao

| Lệnh / phép kiểm tra | Kết quả ngày 17/11 |
|---|---|
| `npm ci --no-audit --no-fund` | PASS, 569 package; không thay lock file |
| `npm run lint` (thực thi ESLint CLI cùng script) | PASS, 0 lỗi; 41 cảnh báo sẵn có |
| `npm test` (thực thi Vitest CLI cùng script) | PASS, 8 file, 43 test |
| `npm run format:check` (Prettier CLI) | PASS toàn bộ `web/` |
| `tsc --noEmit` | PASS |
| `npm run gen:api` (Node script) + `git diff --exit-code -- src/lib/api/generated/` từ `web/` | PASS, không lệch client sinh |
| `NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true npm run build` | PASS, Next.js 16.4.6, Node 22.16.0 portable; sinh 23 trang |
| `npm run test:e2e:production`, không có cờ refresh ở runtime | PASS, 2/2 test trên build production vừa tạo, API mock |
| Cùng smoke với `PLAYWRIGHT_BASE_URL=https://staging.quanlynh.id.vn` | FAIL 2/2 trên bundle staging cũ, tái hiện thiếu refresh; API mock, không phải E2E tài khoản thật |
| `git diff --check` | PASS |
| Parse YAML workflow/compose và đối chiếu build arg/smoke step | PASS |
| `dotnet test tests/Restaurant.ArchitectureTests/Restaurant.ArchitectureTests.csproj -c Release` | BLOCKED: máy không có SDK .NET 10.0.300; không sửa backend |
| Docker image build/smoke, CI remote | NOT_RUN: máy không có Docker; chưa push/dispatch CI |

Các lần thử gọi Next CLI trực tiếp bằng Node 24/22 trước đó lỗi Turbopack khi
tạo tiến trình PostCSS. Lần thử `--webpack` lỗi selector toàn cục có sẵn tại
`web/src/components/ops/ops.module.css:110`. Sau đó lệnh chuẩn `npm run build`
với Node 22 đã PASS; không đổi CSS hoặc cấu hình bundler, chưa kết luận nguyên
nhân các lỗi môi trường trước đó. Smoke local có cảnh báo Next khuyến nghị dùng
standalone server; image Docker vẫn chạy `node server.js` theo Dockerfile.

Kiểm tra production cục bộ dùng mock API chỉ xác nhận bundle frontend, không
xác nhận DB, cookie thật, TTL thực tế hay thu hồi phiên của staging. Bằng chứng
và lệnh kiểm tra được ghi thêm tại
[nhật ký kiểm tra](evidence/week-one/local-checks-2026-11-17.md).

Hải: review thay đổi workflow/compose, build/deploy và cung cấp image ID.
Hiếu: xác nhận migration/API refresh và điều kiện tài khoản test.
Bảo Anh/cả nhóm: chạy bảng E2E, điền giờ thực tế, duyệt ước lượng tuần 2.
