# Biên bản nghiệm thu tuần 1 của Hiếu (HIE-2.0)

- Ngày kiểm: **2026-11-17** (Thứ Ba, tuần 2).
- Cơ sở kiểm: `origin/develop` tại `ccc536a` (sau khi merge PR #42, #43 của Hải).
- Người lập: Claude, thay mặt Hiếu. Người duyệt cuối: Hiếu.
- Phạm vi: HIE-1.2, HIE-1.3, HIE-1.4, HIE-1.5 và HIE-1.5b/HAI-1.5 (phần mã của Hiếu). Việc của Phương, Bảo Anh, Duy và Hải chỉ được ghi khi nó ảnh hưởng đến kết luận của Hiếu; không nhận hộ.

> **Cập nhật 18/11/2026:** job `Publish images to GHCR` đã xanh trở lại ở `bafc457` và `restaurant-web` đã có tag; phần còn lại của tuần 1 chuyển sang mục 8. Các mục 1–7 giữ nguyên làm lịch sử của ngày 17/11.

> **Đối chiếu bổ sung của Hải, 18/11:** [Bằng chứng GitHub](../evidence/week-one/github-controls-2026-11-18.md) xác nhận ruleset develop đã áp dụng đúng nhánh, một approval và hai required checks; TRX CI có đủ 9/9 test Phương Passed, không skip. PR thử #50 có required check đỏ, trạng thái blocked và đã đóng không merge. [Bằng chứng VPS](../evidence/week-one/vps-readonly-2026-11-18.md) và [ma trận INF](../test-strategy.md) ghi các phần đã/chưa xác minh. Những ghi chú cũ ở dưới không yêu cầu deploy/migrate lại. Browser E2E và nghiệm thu đồng thời hai IP chưa được kết luận PASS từ các bằng chứng trước.

Quy ước kết quả: **ĐẠT** = đã chạy lệnh hoặc đọc nguồn công khai trong phiên này, kết quả ghi bên dưới. **CHƯA KIỂM CHỨNG** = cần người có quyền (VPS, tài khoản GitHub admin, hai địa chỉ IP) làm. **KHÔNG ĐẠT** = đã kiểm và sai.

## 1. Kết luận ngắn

| Nhóm | Kết luận |
|---|---|
| Mã và test của HIE-1.2 → 1.5 | **ĐẠT.** Cả bốn PR đã merge; trên `develop` `ccc536a` build 0 cảnh báo/0 lỗi, 3 test kiến trúc + 118 test tích hợp (SQL Server 2022 thật) đạt, 0 bỏ qua; frontend 43/43 và `tsc` sạch. |
| CI trên `develop` | **ĐẠT với test, KHÔNG ĐẠT ở bước publish.** Hai job `Build and test` và `Identity SQL Server tests` xanh ở `ccc536a`; job `Publish images to GHCR` đỏ ở bước *Build and push web image* (mục 4). |
| Nghiệm thu trên staging (hai IP, refresh 4 bước, migration Seating) | **CHƯA KIỂM CHỨNG.** Staging đang chạy (mục 3) nhưng ba kiểm tra này cần tài khoản, hai IP và quyền VPS. Hải làm (N3). |
| Quy trình review | **Có hai điểm lệch** so với quy định (mục 5): #37 và #43 merge không có approval; ruleset `develop` có vẻ chưa áp dụng cho nhánh nào. |

Mốc #0 **chưa nên coi là đạt** cho đến khi mục 3 (N3) và mục 4 xong.

## 2. Mã và test (đã kiểm chứng trong phiên này)

### 2.1 PR đã vào `develop`

Nguồn: API công khai của GitHub (`/pulls/{n}`, `/pulls/{n}/reviews`), đọc ngày 17/11.

| Việc | PR | Merge lúc (UTC) | Người merge | Review ghi nhận |
|---|---|---|---|---|
| HIE-1.2 nền tảng và ADR-01 | #36 | 2026-11-16 04:10 | Trunghieu220504 | phuongdtrai: APPROVED. **Chưa có review của Hải** |
| HIE-1.5b/HAI-1.5 proxy tin cậy | #37 | 2026-11-16 04:21 | Trunghieu220504 | **Không có review nào** |
| HIE-1.5 Seating `InitialSeating` | #38 | 2026-11-16 04:21 | Trunghieu220504 | phuongdtrai: APPROVED |
| HIE-1.4 26 policy + `RequirePermission` | #39 | 2026-11-16 04:21 | Trunghieu220504 | phuongdtrai: APPROVED |
| HIE-1.3 refresh token | #26 | 2026-11-11 17:02 | phuongdtrai | phuongdtrai: APPROVED (người duyệt cũng là người merge). **Chưa có review của Hải** |

### 2.2 Lệnh đã chạy trên `develop` `ccc536a`

Máy Hiếu, Windows 11, .NET SDK 10.0.300, Docker 29.7.2, SQL Server `mcr.microsoft.com/mssql/server:2022-latest` chạy một lần trong container `hie-accept-sql` (đã xóa sau khi chạy). Mật khẩu `sa` sinh ngẫu nhiên, không lưu.

| Lệnh | Kết quả |
|---|---|
| `dotnet restore RestaurantManagement.slnx --locked-mode` | Thành công |
| `dotnet build RestaurantManagement.slnx -c Release --no-restore` | **0 cảnh báo, 0 lỗi** (2 phút 7 giây) |
| `dotnet test RestaurantManagement.slnx -c Release --no-build` với `IDENTITY_TEST_SQLSERVER` trỏ vào container | `Restaurant.ArchitectureTests`: **3/3**. `Restaurant.IntegrationTests`: **118/118, 0 bỏ qua** (5 phút 22 giây) |
| `npm test` (trong `web/`) | **8 file, 43/43 đạt** |
| `npx tsc --noEmit` (trong `web/`) | Không có lỗi |

Số 118 + 3 trùng với bằng chứng Codex đã ghi ngày 15/11 trong sổ tiến độ cục bộ của Hiếu (file không nằm trong git), nên hai lần chạy độc lập cho cùng kết quả.

### 2.3 Đối chiếu từng tiêu chí "xong khi" (theo `phan-cong-chi-tiet-7-tuan.md`)

| Việc | Tiêu chí | Kết quả |
|---|---|---|
| HIE-1.2 | PR được Phương **và Hải** duyệt; quy ước được tuân theo; `DependencyTests` cập nhật | Một phần: Phương duyệt, **Hải chưa có review ghi nhận**; 3 test kiến trúc đạt |
| HIE-1.3 | Test thu hồi/hết hạn/dùng lại đạt trên SQL Server | Đạt. Nằm trong 118 test chạy trên SQL Server thật; lớp `IdentityRefreshTokenSqlServerTests` có các ca: dùng lại secret đã xoay vòng (ngay lập tức và sau thời gian ân hạn) thu hồi cả phiên; hết hạn, thiếu hoặc rác bị từ chối; hai request đồng thời chỉ cấp một token mới; đổi mật khẩu thu hồi mọi secret; tài khoản bị khóa không refresh được; lỗi phát token thì rollback giữ secret cũ; refresh trả vai trò và quyền hiện tại sau khi đổi vai trò |
| HIE-1.4 | Test 403 theo từng vai trò cho một endpoint mẫu mỗi nhóm quyền | Đạt. `PermissionEndpointTests.Seeded_role_matrix_returns_401_403_or_200_for_every_permission` và `PermissionPolicyTests.Every_seeded_permission_has_an_authenticated_claim_policy` nằm trong 118 test (bản ghi 15/11: 130 ca có xác thực + 26 ca ẩn danh) |
| HIE-1.5 | Test không mở hai phiên trên một bàn | Đạt. `SeatingSqlServerTests.Concurrent_opens_allow_exactly_one_active_session` (và 5 ca khác: đóng phiên giải phóng bàn, mã khách giữ nguyên sau chuyển bàn, chuyển về cùng bàn bị chặn, rowversion chặn cập nhật cũ, migration áp lại và down/up) nằm trong 118 test. **Ghi chú:** đây là test mức dữ liệu; test đồng thời mức API thuộc HIE-2.1 |
| HIE-1.5b | Proxy tin cậy, giới hạn đăng nhập theo IP thật | Test mức mã đạt; **kiểm tra thật sau proxy chưa làm** (mục 3) |

## 3. Staging (chưa kiểm chứng hết)

Kiểm tra chỉ đọc, không đăng nhập, từ máy Hiếu lúc 08:41–08:42 UTC ngày 17/11:

| Kiểm tra | Kết quả |
|---|---|
| `GET https://staging.quanlynh.id.vn/health/live` | 200, chứng chỉ hợp lệ |
| `GET https://staging.quanlynh.id.vn/health/ready` | 200, nội dung `Healthy` |
| `GET https://staging.quanlynh.id.vn/` | 200, trang web Next.js do Nginx phục vụ |
| `http://staging.quanlynh.id.vn/` | 301 chuyển sang HTTPS |
| `GET /api/system/modules` không token | 401 (đúng: yêu cầu xác thực) |
| Header `Strict-Transport-Security` | **Không có.** Cấu hình HSTS của PR #43 đã merge nhưng **chưa được deploy lên VPS** |

Phần dưới **chưa kiểm chứng**, giao cho Hải (N3) vì cần tài khoản thật, hai IP khác nhau hoặc quyền SSH. Hải ghi lệnh và kết quả thật vào mục "Bằng chứng staging" cuối file này.

| Mã | Kiểm tra | Điều kiện đạt | Kết quả |
|---|---|---|---|
| S1 | Hai IP khác nhau, mỗi IP đăng nhập sai 11 lần trong 1 phút | Mỗi IP bị giới hạn riêng (trả 429 ở lần 11), IP kia không bị ảnh hưởng | CHƯA KIỂM CHỨNG |
| S2 | Refresh 4 bước | Lần 1 → 200, lần 2 (token mới) → 200, dùng lại token cũ → 401, dùng token mới sau khi bị thu hồi chuỗi → 401 | CHƯA KIỂM CHỨNG |
| S3 | Migration `InitialSeating` đã apply, có bản sao lưu trước khi apply | Bảng `seating.*` tồn tại; có tệp sao lưu có ngày | CHƯA KIỂM CHỨNG |
| S4 | Bản deploy hiện tại là `ccc536a` hoặc mới hơn | Container API/web dùng image từ GHCR có tag commit | CHƯA KIỂM CHỨNG (HSTS còn thiếu cho thấy chưa deploy) |

## 4. Hạ tầng liên quan đến kết luận của Hiếu

### 4.1 Backend CI trên `develop`

Nguồn: API công khai `/actions/runs`, `/actions/runs/{id}/jobs`, `/check-runs/{id}/annotations`.

| Run | Commit | Kết quả |
|---|---|---|
| 44032166448 | `0b017cc` | success (cả hai job test) |
| 44043939901 | `bc3199d` | cancelled (bị run mới hơn thay thế, vì #42 và #43 merge cách nhau 31 giây) |
| **44044015151** | **`ccc536a`** | **failure**: `Build and test` success, `Identity SQL Server tests` success, **`Publish images to GHCR` failure ở bước 6 *Build and push web image*** |

Annotation của job chỉ ghi `Process completed with exit code 1.`; log chi tiết cần quyền admin repo, tôi không đọc được.

### 4.2 Đã loại trừ

- Lỗi **không** do mã web: build image y hệt lệnh CI (`docker build -f web/Dockerfile --build-arg API_URL=http://api:8080 --build-arg NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true web`) thành công trên máy Hiếu, kể cả với `--no-cache` (6 phút 24 giây, Next.js compile và tạo 23 trang tĩnh).
- Image API **đã được đẩy lên** và đọc được không cần đăng nhập: `ghcr.io/trunghieu220504/restaurant-api` có tag `ccc536ad…` và `develop` (đọc bằng token ẩn danh của GHCR).

### 4.3 Còn lại (cần chủ repo)

- Gói `ghcr.io/trunghieu220504/restaurant-web` có **0 tag**. Nguyên nhân chưa xác định. Cách làm nhanh nhất: mở Actions → run 44044015151 → job *Publish images to GHCR* → bước *Build and push web image* và đọc dòng lỗi cuối. Hai khả năng cần phân biệt: (a) bước `docker build` lỗi trên runner; (b) bước `docker push` bị từ chối (gói đã tồn tại nhưng chưa liên kết với repo nên `GITHUB_TOKEN` không có quyền ghi). Tôi **chưa có bằng chứng** để chọn giữa hai khả năng.
- Nếu là (b): vào *Packages → restaurant-web → Package settings*, mục *Manage Actions access*, thêm repo này với quyền Write, rồi chạy lại workflow.

## 5. Điểm lệch về quy trình review (cần chủ repo quyết định)

1. **Ruleset `develop` có vẻ không áp dụng cho nhánh nào.** API công khai trả `conditions.ref_name.include = []` cho ruleset `develop` (id 26971142), còn ruleset `main` có `refs/heads/main`. Các luật bên trong là đúng (1 review bắt buộc, `Build and test` và `Identity SQL Server tests` bắt buộc), nhưng hai PR merge không đủ điều kiện: #37 (không review) và #43 (không review), #42 chỉ có comment. Đây là **suy luận từ dữ liệu công khai**, tôi chưa mở trang Settings; nếu Hiếu có quyền bỏ qua (bypass) thì cũng cho kết quả tương tự. Cách kiểm: *Settings → Rules → Rulesets → develop → Target branches*; thêm `refs/heads/develop` (hoặc *Include default branch* nếu `develop` là nhánh mặc định) và kiểm tra danh sách *Bypass*.
2. **HIE-1.2 thiếu review của Hải.** Tiêu chí yêu cầu Phương và Hải duyệt; chỉ có Phương (#36). Đề xuất: Hải đọc `docs/adr/ADR-01-shared-foundation.md` cùng `docs/adr/ADR-01-review-checklist.md` và ghi nhận xét vào PR-E (đã nằm trong hướng dẫn của Hải), không cần mở lại #36. Tương tự #26 (refresh token) chỉ có review của Phương.
3. **#37 và #43 merge không có approval.** Không cần hoàn tác (CI test xanh, kết quả đã được chạy lại ở mục 2). Chỉ cần ghi nhận để lần sau ruleset chặn đúng.

## 6. Việc còn lại để đóng tuần 1 của Hiếu

| # | Việc | Người | Hạn |
|---|---|---|---|
| 1 | Đọc log job *Publish images to GHCR* (mục 4.3) và sửa; đặt gói GHCR Public sau khi `restaurant-web` có tag | Hiếu (log, quyền gói), Hải (sửa workflow nếu là lỗi build) | Th 4 18/11 |
| 2 | Sửa *Target branches* của ruleset `develop` (mục 5.1) | Hiếu | Th 4 18/11 |
| 3 | Deploy bản `ccc536a` (HSTS, healthcheck, non-root) lên staging và ghi S1–S4 | Hải | Th 5 19/11 |
| 4 | Hải ghi nhận xét review ADR-01 | Hải | Th 5 19/11 |
| 5 | Sau khi S1–S4 đạt: đổi HIE-1.5b và HIE-1.5 từ `In Review` sang `Done` trong sổ tiến độ cục bộ của Hiếu | Hiếu | Th 5 19/11 |

## 7. Bằng chứng staging — Hải, 18/11/2026

**Staging:** https://staging.quanlynh.id.vn

**Commit deploy:** `7de77d4cbd15deb474bab146ae6f14dd05937dbe`

### S1 — Rate limiting

- Nguồn IP A (đã ẩn địa chỉ): 10 request login không hợp lệ nhận 400; request 11 nhận 429.
- Nguồn IP B (đã ẩn địa chỉ): 10 request nhận 400; request 11 nhận 429.
- Thời gian kiểm thử IP B: 3,6 giây.
- Hai IP công khai khác nhau đã được xác minh.
- IP A không vượt được giới hạn khi giả mạo `X-Forwarded-For`.
- **PASS:** giới hạn đăng nhập trên từng IP.
- **CHƯA KIỂM CHỨNG:** tính độc lập đồng thời của hai IP trong cùng cửa sổ 60 giây.

**Bổ sung kiểm tra đồng thời 18/11:** [bằng chứng mới](../evidence/week-one/rate-limit-concurrent-2026-11-18.md) **FAIL** kỳ vọng mỗi nguồn có hạn mức riêng: hai nguồn khác outbound IPv4 chỉ nhận tổng 10 request 400 trước 429, tái hiện sau thời gian nghỉ. Kết quả từng IP ở trên chỉ là lịch sử và không chứng minh tính độc lập; cần audit proxy đang chạy và kiểm tra lại sau sửa.

### S2 — Refresh token

Kiểm thử trực tiếp API trên staging:

| Kiểm tra | HTTP |
|---|---|
| Login | 200 |
| Refresh lần 1 | 200 |
| Refresh lần 2 | 200 |
| Replay cookie cũ | 401 |
| Cookie mới nhất sau khi phiên bị thu hồi | 401 |

- Cookie `rt` có HttpOnly, Secure, SameSite=Strict, Path=/api/identity.
- Hai lần refresh đều xoay vòng cookie thành công.
- Kết quả: `R9_3_REFRESH_FLOW_PASS`.
- Sử dụng tài khoản staging hiện có; chưa xác minh là tài khoản test riêng.
- Chưa thay thế cho kiểm thử E2E trình duyệt.
- Không lưu mật khẩu hoặc token trong tài liệu.

### S3 — Seating migration và backup

- Backup trước triển khai: `RestaurantManagement_before_R8_20261118T030953Z.bak`.
- SHA-256: `5f967ab6dcb561ecf29ed0819d8a6aeb6fb1f8e345e0210f7f57ff8ea9a3f858`.
- Đã áp dụng `20261114224201_InitialSeating`.
- Database có 31 user tables và 6 EF migrations.
- Schema seating có 5 bảng nghiệp vụ và bảng lịch sử migration.
- Migration idempotent chạy lại thành công.
- SQL container không bị restart khi deploy.
- **Kết quả: PASS.**

### S4 — Staging deployment

- API: `ghcr.io/trunghieu220504/restaurant-api:7de77d4cbd15deb474bab146ae6f14dd05937dbe`.
- Web: `ghcr.io/trunghieu220504/restaurant-web:7de77d4cbd15deb474bab146ae6f14dd05937dbe`.
- Hai container xác nhận đúng OCI revision.
- `/`, `/health/live`, `/health/ready`: HTTP 200.
- HTTP sang HTTPS: 301.
- HSTS: `max-age=86400`.
- API chạy non-root UID 1654.
- API, web, SQL không publish cổng trực tiếp ra host.
- Identity `/me` không bearer: 401.
- Identity `/refresh` không cookie: 401.
- **Kết quả: PASS.**

### Phần còn mở

- Xác minh rate limit hai IP độc lập trong cùng cửa sổ 60 giây.
- Browser E2E W1-E01 đến W1-E06 cần phối hợp frontend.
- ADR-01, quy tắc review và ruleset `develop` cần người phụ trách xác nhận.

Không commit mật khẩu, refresh cookie hay bearer token.

## 8. Cập nhật 18/11/2026 (đối chiếu `develop` `bafc457`)

Nguồn: API công khai của GitHub (`/actions/runs`, `/rulesets`, `/branches`, `/pulls`), `ghcr.io` bằng token ẩn danh, và `curl` chỉ-đọc tới staging lúc 19:02 UTC ngày 17/11 (02:02 ngày 18/11 giờ Việt Nam). Không đăng nhập, không có quyền admin repo, không vào VPS.

| Mục | Ngày 17/11 | Ngày 18/11 | Bằng chứng |
|---|---|---|---|
| Job `Publish images to GHCR` | Đỏ ở `ccc536a` (bước *Build and push web image*) | **Xanh ở `bafc457`**: cả ba job (`Build and test`, `Identity SQL Server tests`, `Publish images to GHCR`) success | Run 44059105694 |
| Nguyên nhân lỗi ở `ccc536a` | Chưa xác định | **Vẫn chưa xác định.** Log chi tiết cần quyền admin; lỗi không tái hiện ở run sau. Chưa phân biệt được khả năng (a) build lỗi trên runner và (b) push bị từ chối (mục 4.3) | Run 44044015151 |
| Gói GHCR | `restaurant-web` có 0 tag | `restaurant-web`: tag `bafc457…` và `develop`. `restaurant-api`: tag `ccc536a…`, `develop`, `bafc457…`. Cả hai kéo được bằng token ẩn danh, tức đã Public | `ghcr.io/v2/trunghieu220504/<gói>/tags/list` trả 200 |
| Test SQL Server của Phương trên CI | Chưa rõ | Bộ lọc `FullyQualifiedName~SqlServer` (`backend-ci.yml`, bước *Run SQL migration and bootstrap tests*) khớp tên lớp `PhuongWeekOneSqlServerTests`; job SQL xanh ở `bafc457`. **Chưa đọc được TRX** nên chưa xác nhận số test và việc không có test bị bỏ qua | `backend-ci.yml` dòng 96 |
| Spike C03 (N4) | Chưa nghiệm thu | **Vẫn chưa.** `spike-c03-result.md` còn ghi "chưa thay thế nghiệm thu CI/staging"; chưa có kết quả lặp ≥ 20 lần và chưa có người ký review | `docs/spike-c03-result.md` |
| Ruleset `develop` | `include: []` | **Không đổi**: `include: []`, `GET /branches/develop` trả `protected: false`; `main` trả `protected: true`. Mục 5.1 vẫn đúng | Ruleset 26971142 |
| Staging | `/health/ready` 200, thiếu HSTS | **Không đổi**: `/health/ready` 200, vẫn không có `Strict-Transport-Security`, nên bản có PR #43 chưa được deploy. S1–S4 vẫn **CHƯA KIỂM CHỨNG** | `curl -sI https://staging.quanlynh.id.vn/` |
| PR #24 (Duy) | Chưa rõ | Đã merge | `/pulls/24` |
| PR #41 (BAO-1.4, Bảo Anh) | Chưa rõ | Đang mở; 4/4 check xanh; mergeable `clean`; **chưa có review nào** | `/pulls/41`, `/check-runs` |

**Tác động lên S2 (refresh trên staging).** Bằng chứng trong PR #41 (log trình duyệt lúc 06:34 UTC ngày 17/11) cho thấy bundle web đang chạy trên staging còn đọc cờ `NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED` lúc chạy (`a.default.env.NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED`), tức nút refresh phía web chưa được nhúng khi build. Image web trên GHCR dựng với `--build-arg NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true` (`backend-ci.yml`, bước *Build and push web image*), nên cần Hải deploy image mới thì S2 qua trình duyệt mới có ý nghĩa. S2 gọi trực tiếp API (không qua web) không phụ thuộc cờ này. Đây là suy luận từ bằng chứng của Bảo Anh và từ file workflow; tôi chưa kiểm tra bundle đang chạy sau ngày 17/11.

### 8.1 Việc còn lại để đóng tuần 1 của Hiếu

| # | Việc | Người | Trạng thái |
|---|---|---|---|
| 1 | ~~Sửa job GHCR~~ Job đã xanh. Còn lại: theo dõi lần sau; nếu đỏ lại thì đọc log để phân biệt (a)/(b) | Hiếu (log) | Giảm xuống theo dõi |
| 2 | Sửa *Target branches* của ruleset `develop` (mục 5.1) và kiểm *Bypass list* | Hiếu | **Còn mở** (cần quyền admin, giao diện Settings) |
| 3 | Deploy `develop` mới nhất (HSTS, healthcheck, non-root, web có cờ refresh) lên staging và điền S1–S4 ở mục 7 | Hải | **Còn mở** (hạn Th 5 19/11) |
| 4 | Hải ghi nhận xét review ADR-01 | Hải | **Còn mở** |
| 5 | Phương: lặp mỗi kịch bản ≥ 20 lần, kết luận ĐẠT/KHÔNG ĐẠT trong `spike-c03-result.md` kèm người ký (N4) | Phương, Hải | **Còn mở** |
| 6 | Review PR #41 (BAO-1.4). Người duyệt chính theo kế hoạch là Duy; PR sửa `.github/workflows/web-ci.yml`, `web/Dockerfile` và `infra/compose.frontend.yml` nên Hiếu hoặc Hải cần duyệt phần đó | Duy; Hiếu hoặc Hải | **Còn mở** |
| 7 | Sau khi S1–S4 đạt: đổi HIE-1.5b và HIE-1.5 từ `In Review` sang `Done` trong sổ tiến độ cục bộ; HIE-1.2 chỉ đổi khi Hải review xong | Hiếu | Chờ mục 3 và 4 |

## 9. Kiểm tra staging bên ngoài ngày 18/11/2026 (chỉ gọi qua HTTPS)

Người chạy: Claude, thay mặt Hiếu, bằng `curl` từ máy Hiếu, dùng một tài khoản kiểm thử vai trò `Admin` do Hải cấp. Không vào VPS, không dùng hai địa chỉ IP. Không ghi mật khẩu, token hay cookie vào đây. Mốc `develop` lúc kiểm: `9a5d871`. Không biết image đang chạy trên VPS là commit nào (S4 chưa kiểm chứng).

| Mục | Kết quả | Bằng chứng |
|---|---|---|
| `/health/ready`, `/health/live` | ĐẠT | Cả hai trả 200. |
| Header HSTS | ĐẠT | `Strict-Transport-Security: max-age=86400`, nên bản có PR #43 đã chạy. |
| Đăng nhập | ĐẠT | `POST /api/identity/login` trả 200 và cookie `rt`. |
| **S2 refresh 4 bước** | **ĐẠT** | Refresh lần 1: 200. Lần 2: 200. Dùng lại secret cũ: 401. Refresh bằng secret mới nhất sau đó: 401 (cả phiên bị thu hồi). Đúng chuỗi 200, 200, 401, 401. |
| Không token gọi `/api/identity/me` | ĐẠT | 401. |
| `POST /api/identity/logout` | ĐẠT | 204. |
| `GET /api/salesday/current` | ĐẠT | 200, thân rỗng (chưa có ngày bán đang mở). |
| **Seating API trên staging** | **KHÔNG ĐẠT** | `GET /api/seating/board`, `/areas`, `/tables` với token hợp lệ đều trả **404**. Tài khoản `Admin` chỉ có `identity.accounts.manage` và `identity.roles.manage`; nếu route tồn tại thì kết quả phải là 403. Vậy image đang chạy chưa chứa PR #58. |

**Lưu ý về phép thử:** gọi không có token và nhận 401 **không** chứng minh route tồn tại, vì middleware xác thực cũng trả 401 cho đường dẫn không có thật. Chỉ phép thử có token (404 ở trên) mới có giá trị.

Chưa kiểm chứng sau lần kiểm này: S1 (hai IP, 11 lần/phút), S3 (migration Seating và sao lưu), S4 (commit đang chạy), mọi thao tác Seating thật (cần tài khoản `Manager` hoặc `Staff`). Hải cần deploy lại `develop` mới nhất; sau đó chạy lại `GET /api/seating/board` với tài khoản `Staff` hoặc `Manager`, kết quả đúng là 200, và với `Admin` là 403.
