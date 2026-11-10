# Hướng dẫn tuần 1 (thứ Hai 09/11 – thứ Sáu 13/11/2026)

> Trạng thái: **ĐỀ XUẤT, chờ người duyệt xác nhận** (ngày bắt đầu 09/11 chưa được xác nhận). Tài liệu này giải thích
> và hướng dẫn cách làm cho **tuần đầu tiên**, chi tiết theo từng người. Nó bám theo
> [phan-cong-chi-tiet-7-tuan.md](phan-cong-chi-tiet-7-tuan.md) (mã việc `HIE-`, `PHU-`, `BAO-`, `DUY-`, `HAI-`); khi hai
> tài liệu khác nhau, **kế hoạch 7 tuần là bản gốc** và tài liệu này phải được sửa theo.
>
> **Cập nhật 11/11/2026 (thứ Tư):** nhóm đã làm từ 09/11. Mục 2 ghi lại trạng thái thật trên GitHub và các mục 5.x có thêm dòng
> "Trạng thái 11/11" cho từng người. Dòng **gạch ngang** là việc đã xong theo GitHub; dòng khác là việc còn lại.
>
> Cách đọc: mục 1–4 đọc chung cả nhóm; mục 5 mỗi người đọc phần của mình và phần của người mình phụ thuộc; mục 7 là cách nghiệm thu thứ Sáu.

## 1. Tuần này làm gì và kết quả cuối tuần

**Mục tiêu:** cuối tuần 1 có một hệ thống **chạy trên mạng thật** (`https://staging.quanlynh.id.vn`): trang web đăng nhập
bằng tài khoản thật do backend cấp, quản lý mở được ngày bán. Frontend và backend làm **song song** từ thứ Hai (frontend dùng dữ liệu giả
cho đến khi backend lên), rồi ghép với nhau vào thứ Năm. Đồng thời khóa các quyết định kỹ thuật chặn những tuần sau.

**Kết quả phải có vào chiều thứ Sáu 13/11** (mỗi dòng đều kiểm tra được, xem cách kiểm ở mục 7):

| # | Kết quả | Người chịu trách nhiệm | Bằng chứng |
|---|---|---|---|
| 1 | VPS thuê xong, bảo mật cơ bản, truy cập SSH bằng khóa, từ ngoài chỉ thấy cổng 22/80/443 | Hải | Kết quả quét cổng, ghi trong sổ tiến độ |
| 2 | `https://staging.quanlynh.id.vn` mở được bằng HTTPS, chứng chỉ hợp lệ, hiện trang đăng nhập của web | Hải + Bảo Anh | Mở bằng điện thoại dùng 4G |
| 3 | Đăng nhập thật từ web, làm mới phiên (refresh) hoạt động, đăng xuất, đổi mật khẩu | Bảo Anh + Hiếu | Demo trực tiếp |
| 4 | Quản lý mở ngày bán từ giao diện; mở lần hai bị chặn | Duy + Phương | Demo trực tiếp |
| 5 | Có kết luận **đạt/không đạt** cho spike khóa theo ngày (C03) | Phương | `docs/spike-c03-result.md` |
| 6 | Quy ước dùng chung (ADR-01) đã merge | Hiếu | PR đã merge, có 2 người duyệt |
| 7 | `develop` và `main` được bảo vệ (PR + CI bắt buộc); CI chạy được test cho cả backend, frontend | Hải | PR thử bị chặn khi CI đỏ |
| 8 | Cả nhóm ước lượng lại khối lượng từng module và báo người duyệt nếu lệch > 20% | Cả nhóm | Bảng ước lượng sau demo |

**Không nằm trong tuần 1:** gọi món, bếp/bar, thanh toán, QR, SignalR thật, kho, báo cáo. Đừng bắt đầu sớm; việc đó làm lệch ưu tiên.

## 2. Điểm xuất phát (kiểm tra trên GitHub ngày 11/11/2026)

**Đã có trên `develop` (3c26e5e):**
- Identity: login, me, logout, change-password (PR #7, #8, merge 05/11). JWT sống 15 phút. **Chưa có refresh token.**
- SalesDay và Catalog của Phương (PR #16, merge 10/11): migration, `GET /api/salesday/current`, `POST /api/salesday/open`, 5 test SQL Server, `docs/spike-c03-result.md`, `docs/phuong-tuan-1.md`. Catalog chưa có API.
- Khung `web/` (Bảo Anh, commit 8fde101): Next.js 16, `output: "standalone"`, rewrite `/api/*` tới `API_URL` (mặc định `http://localhost:5080`), shadcn/ui, bảng, form, toast, 403/404, Playwright, MSW, `npm run gen:api` (đọc `${API_URL}/openapi/v1.json`). Chưa có `package-lock.json` trên `develop`, chưa có đăng nhập.
- CI: chỉ `backend-ci.yml`; bước SQL Server chỉ chạy test có tên `IdentitySqlServer`, nên 5 test của Phương **chưa chạy trên CI**.

**Đang chờ:**
- PR #17 `feature/duy-week-one` (Duy): realtime, âm thanh, thành phần vận hành, màn mở ngày. Sửa cả file cấu hình của Bảo Anh (`next.config.ts`, `package.json`, `tsconfig.json`, `eslint.config.mjs`) và thêm `package-lock.json`.
- Nhánh `docs/identity-api-contract` (Hiếu, chưa push): `docs/api/openapi.json`, `docs/api/identity-contract.md`, khai báo kiểu response. Frontend cần file này để sinh client.
- PR Dependabot #10–#13.

**Chưa có:** Dockerfile, docker-compose, CD, workflow cho `web/`, project `Contracts`, refresh token, 7 module nghiệp vụ còn lại (Seating, Sales, Fulfillment, Billing, Inventory, Reporting, Audit chỉ là khung rỗng). Chưa thấy trên GitHub dấu hiệu về VPS/DNS.

**Repo:** công khai; `main` và `develop` **chưa được bảo vệ**. Nhánh `chore/backend-foundation`, `chore/sync-salesday-docs`, `feature/identity-initialization`, `feature/identity-authentication` đã merge, có thể xóa.

**Thông tin kỹ thuật cần nhớ:**
- Ở Development API chạy ở `http://localhost:5080`, CORS cho `http://localhost:3000`. OpenAPI (`/openapi/v1.json`, **đã kiểm chứng**) chỉ mở ở Development; staging không có, nên file OpenAPI phải xuất và commit (`docs/api/identity-contract.md` mục 4).
- Giới hạn đăng nhập: 10 lần/phút theo IP; đổi mật khẩu: 5 lần/phút theo người dùng. Phía sau Nginx, mọi người dùng chung một IP cho đến khi thêm `UseForwardedHeaders` (HAI-1.5).
- Cấu hình bắt buộc để API chạy: `ConnectionStrings__Restaurant`, `Jwt__SigningKey` (**ít nhất 32 byte**), `Jwt__Issuer`, `Jwt__Audience` (mặc định `restaurant-api` / `restaurant-web`). Host **không tự migrate**.

## 3. Cách cả nhóm làm việc trong tuần

### 3.1 Nhịp ngày

| Thời điểm | Việc | Thời lượng |
|---|---|---|
| Thứ Hai 09:00 | Kickoff: xác nhận phân công, chỗ đặt `web/`, quy ước nhánh, ai thanh toán VPS | 45' |
| Hằng ngày | Daily (giờ do nhóm chốt ở kickoff) | 15' |
| Thứ Tư | Rà soát contract/API | 30' |
| Thứ Sáu 15:00 | Demo cho người duyệt, sau đó ước lượng lại (planning poker) | 45' + 30' |

**Daily:** mỗi người nói đúng 3 điều: hôm qua xong gì (theo mã việc), hôm nay làm gì, đang bị chặn bởi ai/cái gì. Không bàn thiết kế dài trong daily; hẹn riêng.

**Báo cuối ngày** (một tin trong kênh nhóm, 3 dòng): `Xong:` mã việc + link PR; `Đang làm:`; `Chặn:` (nêu tên người cần giúp).

### 3.2 Quy trình làm một việc (áp dụng cho mọi người, mọi mã việc)

1. **Nhận việc:** chuyển thẻ sang *In Progress* trong bảng công việc (Hải dựng ngày thứ Hai); AI agent ghi vào `docs/AGENT_PROGRESS.md` mục 5.
2. **Tạo nhánh** từ `develop` mới nhất, một nhánh một mã việc (xem "Quy tắc làm việc với Git" trong kế hoạch 7 tuần):
   `git switch develop && git pull --ff-only origin develop && git switch -c feature/<module>-<nội-dung>`.
3. **Làm nhỏ, commit thường xuyên**, mỗi commit một ý, theo Conventional Commits, mã việc cuối tiêu đề. Không `git add .`.
4. **Tự kiểm tra trước khi mở PR** (backend: lệnh ở mục 4.3; frontend: `npm run lint`, `npm run build`, `npm test`).
5. **Mở PR vào `develop`** theo mẫu `.github/pull_request_template.md`; điền mục "Kiểm chứng" bằng **lệnh đã chạy và kết quả thật**. Gọi người review theo cặp: Hiếu ↔ Phương, Bảo Anh ↔ Duy, Hải kiểm CI/secret.
6. **Sửa theo review trong ≤ 1 ngày làm việc**, merge (squash) khi CI xanh và có ≥ 1 approval từ người khác. Tác giả không tự merge.
7. **Báo xong** trong daily, kèm cách kiểm chứng. Một việc chỉ gọi là *Done* khi đáp ứng "Xong khi" trong kế hoạch **và** đã nằm trên `develop`.

### 3.3 Định nghĩa "xong" chung

- Code đã vào `develop`, CI xanh, có test tương ứng (backend: test tích hợp trên SQL Server thật khi đụng database).
- Không có secret, không có file build trong PR.
- API đổi thì OpenAPI/tài liệu liên quan được cập nhật **trong cùng PR**.
- Người dùng của thay đổi (người đang chờ nó) đã được báo.

### 3.4 Khi bị kẹt

- Kẹt **hơn 1 giờ** mà chưa có hướng: nhắn người liên quan ngay, đừng chờ daily.
- Việc bị chặn bởi người khác: ghi vào "Chặn" của báo cuối ngày, đồng thời chuyển sang việc khác không bị chặn (mỗi người đã có việc dự phòng ở mục 5).
- Trượt mốc ngày: báo ở daily hôm sau. **Không dồn sang cuối tuần.**
- Việc nào có nguy cơ làm trượt mốc thứ Sáu: báo người duyệt trong 24 giờ (đặc biệt: spike C03, VPS).

### 3.5 Ai sửa thư mục nào trong `web/` (Bảo Anh và Duy cùng làm một thư mục, cần tránh xung đột)

Đề xuất, chốt ở kickoff:

| Khu vực | Chủ | Ghi chú |
|---|---|---|
| Cấu hình dự án (`package.json`, `next.config`, `tsconfig`, Tailwind, ESLint), `src/components/ui`, layout, `src/lib/api` | Bảo Anh | Duy muốn sửa thì nhờ qua PR nhỏ |
| `src/features/auth`, `src/features/menu`, `src/features/tables`, trang quản trị | Bảo Anh | |
| `src/features/operations` (bếp/bar, phục vụ), `src/components/ops`, `src/lib/realtime`, `src/lib/sound` | Duy | |
| `src/features/sales-day`, `src/features/billing`, `src/features/inventory` | Duy | |
| `e2e/` (Playwright) | Duy viết kịch bản, Hải chạy | |

## 4. Chuẩn bị máy (xong trước sáng thứ Hai)

### 4.1 Cài đặt

| Công cụ | Ai cần | Ghi chú |
|---|---|---|
| Git | Tất cả | |
| .NET SDK **10.0.300** (`global.json`) | Hiếu, Phương, Hải (Bảo Anh và Duy nên có để chạy API cục bộ) | `dotnet --version` phải ra 10.0.x |
| Docker Desktop | Tất cả (chạy SQL Server cục bộ); Hải bắt buộc | |
| Node.js bản LTS | Bảo Anh, Duy, Hải | Nhóm chốt phiên bản ở `BAO-1.1` theo yêu cầu tối thiểu của Next.js 16 (**chưa kiểm tra yêu cầu cụ thể**) |
| Tài khoản GitHub có quyền ghi vào repo | Tất cả | Chủ repo thêm collaborator trước thứ Hai |

### 4.2 Chạy SQL Server và API cục bộ (Windows PowerShell)

SQL Server (đổi mật khẩu thành mật khẩu mạnh của bạn; **không commit**):

```powershell
docker run -d --name sql-dev -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="<mật-khẩu-mạnh>" -p 127.0.0.1:1433:1433 -v sqldev:/var/opt/mssql mcr.microsoft.com/mssql/server:2022-latest
```

Chạy API (từ thư mục gốc repo). `appsettings.Development.json` dùng đăng nhập Windows, nên máy dùng Docker phải đè bằng biến môi trường:

```powershell
$env:ConnectionStrings__Restaurant = "Server=127.0.0.1,1433;Database=RestaurantManagement;User Id=sa;Password=<mật-khẩu-mạnh>;Encrypt=True;TrustServerCertificate=True"
$env:Jwt__SigningKey = "<chuỗi-ngẫu-nhiên-dài-ít-nhất-32-ký-tự>"
dotnet tool restore
dotnet ef database update --project src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure --context IdentityDbContext
dotnet run --project src/Host/Restaurant.Api --launch-profile http
```

Tạo tài khoản Admin lần đầu (không đặt mật khẩu trong lệnh; làm theo `docs/identity-initialization.md`, dùng `Read-Host`):
quy định là dùng ba biến `BootstrapAdmin__UserName`, `BootstrapAdmin__Email`, `BootstrapAdmin__Password` rồi chạy
`dotnet run --project src/Host/Restaurant.Api --no-launch-profile -- --bootstrap-admin`. Mật khẩu tối thiểu 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.

Kiểm tra: `http://localhost:5080/health/live` trả 200; `http://localhost:5080/health/ready` trả 200 khi SQL Server kết nối được.

**Bổ sung 11/11:**

- Migration cho SalesDay và Catalog cũng đã có trên `develop`; chạy `dotnet ef database update` cho từng `--context` tương ứng (xem `docs/phuong-tuan-1.md`), không chỉ Identity.
- Script kiểm tra nhanh SalesDay của Phương: `scripts/Test-PhuongWeek1.ps1` (đọc script trước khi chạy).
- Xem tài liệu API ở môi trường Development: `http://localhost:5080/openapi/v1.json`.
- Frontend: `cd web`, `npm install`, `npm run dev`. Web gọi API qua `/api/*`, được rewrite tới biến môi trường `API_URL` (mặc định `http://localhost:5080`). Sinh kiểu TypeScript: `npm run gen:api` (cần API đang chạy).

### 4.3 Lệnh kiểm tra backend trước khi mở PR (lấy từ CI)

```powershell
dotnet restore RestaurantManagement.slnx --locked-mode
dotnet build RestaurantManagement.slnx -c Release --no-restore
dotnet test RestaurantManagement.slnx -c Release --no-build --no-restore
dotnet format whitespace RestaurantManagement.slnx --verify-no-changes --no-restore
git diff --check
```

Muốn chạy luôn test cần SQL Server thật (các test tên `IdentitySqlServer...`), đặt biến trỏ tới một instance **dành riêng cho test** (mỗi test tự tạo
và xóa database tạm `RestaurantIdentityTest_<ngẫu nhiên>`; tuyệt đối không trỏ tới dữ liệu thật):

```powershell
$env:IDENTITY_TEST_SQLSERVER = "Server=127.0.0.1,1433;User Id=sa;Password=<mật-khẩu-mạnh>;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"
dotnet test tests/Restaurant.IntegrationTests -c Release --filter FullyQualifiedName~IdentitySqlServer
```

Khi thiếu biến này các test đó bị **bỏ qua (skip) chứ không báo lỗi**; ghi rõ trong PR là đã chạy hay bỏ qua.

## 5. Hướng dẫn theo từng người

Mỗi mục có: mục tiêu, việc theo ngày kèm **cách làm**, kết quả bàn giao, người cần chờ, việc dự phòng khi bị chặn, và điều không được làm.

### 5.1 Hiếu (Backend A): nền tảng dùng chung, refresh token, Seating

**Mục tiêu tuần:** đưa Identity lên `develop`; khóa quy ước dùng chung để mọi module sau làm cùng một kiểu; cho frontend refresh token và OpenAPI sớm; có nền Seating.
**Nhánh gợi ý:** `feature/identity-refresh-token`, `docs/adr-01-contracts`, `feature/seating-initial`, `feature/identity-forwarded-headers`.
**Trạng thái 11/11:** HIE-1.1 đã xong (PR #7, #8 merge 05/11). Hợp đồng API Identity đã commit cục bộ trên nhánh `docs/identity-api-contract` (commit `e5cdc04`: OpenAPI, `identity-contract.md`, khai báo kiểu response) và **chờ push + PR**; đã quá hạn 10/11 17:00 và frontend đang chờ. HIE-1.2, 1.3, 1.4, 1.5 chưa bắt đầu theo GitHub. Việc kế tiếp theo thứ tự ưu tiên: push hợp đồng, HIE-1.3 (refresh token), HIE-1.2 (ADR-01), forwarded headers, HIE-1.5, HIE-1.4.

| Ngày | Việc | Cách làm | Xong khi |
|---|---|---|---|
| Thứ Hai | **HIE-1.1** đưa Identity vào `develop` | Kiểm tra PR của `feature/identity-authentication` đã có chưa (nếu chưa, mở PR vào `develop`); xác nhận CI xanh; nhờ Phương duyệt; squash merge. Sau khi merge, **không xóa nhánh cũ nếu chưa chắc** commit đã nằm trong `develop` (`git log origin/develop --oneline`) | `develop` chứa commit 2bca5f6 hoặc bản squash tương đương, CI xanh |
| Thứ Hai–Ba | **HIE-1.2** ADR-01 (mở PR nháp thứ Hai) | Viết `docs/adr/ADR-01-contracts-va-nen-tang-chung.md` gồm: (1) cấu trúc project `Restaurant.Modules.<X>.Contracts` (chỉ interface + DTO, không `PackageReference`); (2) định dạng lỗi: ProblemDetails + trường `code` ổn định (ví dụ `order.batch.already_confirmed`); (3) header `Idempotency-Key` (Guid), cùng khóa + cùng nội dung trả lại kết quả cũ, khác nội dung bị từ chối; (4) trừu tượng Outbox; (5) helper khóa ngày (chờ kết quả spike của Phương); (6) quyền: `RequirePermission("...")`. Cập nhật `tests/Restaurant.ArchitectureTests/DependencyTests.cs` **trong cùng PR**: chỉ cho phép đúng tham chiếu mới (Contracts), không hơn | PR có 2 người duyệt (Phương, Hải); `dotnet test` kể cả test kiến trúc xanh |
| Thứ Ba (hạn 17:00) | **Công bố hợp đồng API Identity** cho frontend | (a) Tạo OpenAPI hiện có: chạy API ở Development, tải `http://localhost:5080/openapi/v1.json` (đường dẫn mặc định, **chưa kiểm chứng**), commit vào một đường dẫn cố định cả nhóm chốt thứ Hai (đề xuất `docs/api/openapi.json`). (b) Viết `docs/api/identity-contract.md` mô tả **bản nháp** các endpoint chưa có code: `POST /api/identity/refresh` (không body; đọc cookie; trả `accessToken`, `expiresAt`), cách login đặt cookie, mã lỗi, ví dụ JSON. Frontend dùng cái này để làm mock | Bảo Anh sinh được client từ file và viết được mock từ tài liệu mà không phải hỏi lại |
| Thứ Ba–Thứ Năm | **HIE-1.3** refresh token | Migration mới (ví dụ `IdentityRefreshTokens`): lưu **hash** của refresh token (không lưu bản gốc), thời hạn, thời điểm thu hồi, cha-con để theo dõi chuỗi xoay vòng. `POST /api/identity/refresh`: xoay vòng mỗi lần dùng; **dùng lại token đã xoay ⇒ thu hồi cả chuỗi**. Cookie `HttpOnly; Secure; SameSite=Strict`, giới hạn đường dẫn `/api/identity`. Login đặt cookie, logout và đổi mật khẩu thu hồi. Chạy migration qua script idempotent (không tự migrate). Cập nhật `identity-authentication.md` (hiện ghi "không có refresh") | Test trên SQL Server thật: hết hạn, dùng lại, thu hồi khi đăng xuất/đổi mật khẩu đều đạt; Hải deploy được lên staging thứ Năm |
| Thứ Tư | **HIE-1.4** (bắt đầu) policy theo 26 mã quyền | Thêm extension `RequirePermission("<mã>")` dựa trên danh mục trong `IdentityCatalog.cs`; mỗi nhóm quyền có một endpoint mẫu và test 403 theo vai trò | Xem chú ý bên dưới |
| Thứ Năm | **HIE-1.5** Seating | Entity + migration `InitialSeating` (khu vực, bàn, phiên, chuyển bàn, quyền truy cập khách, chỉ mục duy nhất có điều kiện). Tham khảo thiết kế dữ liệu trong `docs/superpowers/specs/2026-11-05-database-revised-design.md` | Test: không mở hai phiên trên cùng một bàn |
| Thứ Năm | **HIE-1.5b** (cùng Hải, việc `HAI-1.5`) forwarded headers | Thêm `UseForwardedHeaders` **trước** `UseRateLimiter` và middleware chuyển hướng HTTPS, **chỉ tin proxy trong mạng Docker nội bộ** (không tin mọi nơi). Phối hợp Hải test 11 lần đăng nhập/phút từ hai IP | Hai IP khác nhau bị giới hạn riêng |
| Thứ Sáu | Hoàn tất merge ADR-01; hoàn tất test Seating | | Mốc #6 (mục 1) |

> **Chú ý khối lượng:** tuần này Hiếu có 5 mã việc cộng với nhiệm vụ review và điều phối, và nhiều việc phụ thuộc vào Hiếu (frontend chờ hợp đồng và refresh).
> **Ưu tiên khi quá tải:** `HIE-1.1` → hợp đồng API (thứ Ba) → `HIE-1.3` → `HIE-1.2` → forwarded headers → `HIE-1.5` → `HIE-1.4`. `HIE-1.4` và phần tiếp theo của Seating
> chuyển sang đầu tuần 2, **không chặn** frontend tuần này (tuần 1 chưa cần phân quyền chi tiết). Báo ở daily khi chuyển.

**Bàn giao:** Bảo Anh (hợp đồng thứ Ba 17:00; refresh trên staging thứ Năm); Hải (script migration của refresh trước thứ Năm); cả nhóm (ADR-01 thứ Sáu).
**Việc dự phòng nếu chờ:** viết test cho Seating, viết tài liệu ADR.
**Không làm:** commit trực tiếp vào `develop`; sửa module của Phương (SalesDay, Catalog); gộp tính năng mới của tuần sau (Sales) vào các PR tuần này.

### 5.2 Phương (Backend B): spike C03, SalesDay, Catalog

**Mục tiêu tuần:** trả lời câu hỏi rủi ro lớn nhất của dự án (khóa ứng dụng SQL theo ngày bán có chạy đúng khi nhiều module dùng chung giao dịch không), và mở được ngày bán qua API.
**Nhánh gợi ý:** `feature/salesday-spike-c03` (có thể chỉ để chứa test và tài liệu), `feature/salesday-open-day`, `feature/catalog-initial`.
**Trạng thái 11/11:** PHU-1.1, PHU-1.2, PHU-1.3 đã merge vào `develop` (PR #16, 10/11): 5 test SQL Server đạt ở máy Phương, hợp đồng ở `docs/phuong-tuan-1.md`. **Chưa nghiệm thu** vì: CI chưa chạy các test này (Hải mở rộng bộ lọc), chưa có người khác review kết luận spike, chưa có staging. Việc còn lại: (1) phối hợp Hải để test chạy trên CI, rồi cập nhật `docs/spike-c03-result.md` với kết quả CI; (2) lặp mỗi kịch bản ≥ 20 lần để loại trừ kết quả ngẫu nhiên; (3) đưa SalesDay lên staging cho Duy; (4) duyệt PR của Hiếu (hợp đồng API, ADR-01); (5) kiểm tra `scripts/Test-PhuongWeek1.ps1` không chứa chuỗi kết nối hay mật khẩu thật.

| Ngày | Việc | Cách làm | Xong khi |
|---|---|---|---|
| Thứ Hai | Duyệt PR Identity của Hiếu; **PHU-1.1** dựng khung spike | Trong `tests/Restaurant.IntegrationTests` tạo vùng spike (ví dụ thư mục `Spikes/`). Hai DbContext nhỏ dùng chung **một** `DbConnection` và **một** `DbTransaction`; dùng cùng cách lấy SQL Server thật như các test `IdentitySqlServer` (biến `IDENTITY_TEST_SQLSERVER`; Hải mở rộng CI ở `HAI-1.3`). Đọc phần C03 trong tài liệu thiết kế dữ liệu trước khi viết | Khung chạy được trên SQL Server cục bộ |
| Thứ Ba | **PHU-1.1** các kịch bản | Dùng `sp_getapplock` với chế độ `Shared` và `Exclusive`, gắn với giao dịch (owner = Transaction), có thời gian chờ. Kịch bản cần có: (1) hai luồng cùng xin **Exclusive** (hai người chốt cùng lúc) ⇒ chỉ một vào, người kia chờ rồi bị từ chối; (2) nhiều **Shared** chạy cùng nhau, nhưng **Exclusive** phải chờ hết Shared; (3) **rollback giữa chừng** qua hai DbContext ⇒ không để lại dữ liệu nửa chừng ở cả hai; (4) khóa được nhả khi commit và khi rollback. Đây là nội dung của test 14 và 15 trong ma trận | Mỗi kịch bản có test tự động |
| Thứ Ba–Thứ Tư | **PHU-1.2** SalesDay | Migration `InitialSalesDay`, miền (ngày bán, trạng thái ĐANG BÁN / CHỜ CHỐT / ĐÃ CHỐT, tiền đầu ngày). Endpoint dưới `/api/salesday`: `GET /current`, `POST /open` (chỉ quản lý; tiền đầu ngày; **chặn khi ngày trước chưa chốt**; chỉ một ngày hoạt động tại một thời điểm, bằng ràng buộc của cơ sở dữ liệu chứ không chỉ kiểm tra trong code). Công bố hợp đồng API (JSON, mã lỗi) cho Duy **ngay khi có**, tối đa thứ Tư | Test: chỉ một ngày hoạt động; mở lần hai bị từ chối |
| Thứ Tư | **PHU-1.3** Catalog | Migration `InitialCatalog`: nhóm, món, biến thể, nhóm tùy chọn, tùy chọn, gán, ba bảng hết món theo ngày. Chạy trên SQL Server thật, kiểm tra các ràng buộc | Migration chạy trên SQL Server thật |
| Thứ Năm | Đưa SalesDay lên staging; viết nháp kết luận spike | Merge vào `develop`, nhờ Hải deploy. Viết `docs/spike-c03-result.md`: cách thử, số lần chạy, kết quả từng kịch bản, vấn đề gặp phải | Duy gọi được `POST /open` trên staging |
| Thứ Sáu | **Kết luận spike: đạt / không đạt** | Chạy mỗi kịch bản lặp lại nhiều lần (đề xuất ≥ 20 lần liên tiếp) để loại trừ kết quả ngẫu nhiên. **Nếu không đạt:** báo người duyệt trong 24 giờ và đề xuất phương án thay thế (trạng thái chờ bền vững, mục 5 tài liệu thiết kế dữ liệu) | Có `spike-c03-result.md` và kết luận trình bày trước nhóm |

**Bàn giao:** Duy (hợp đồng và API mở ngày, thứ Tư–Năm); Hiếu (kết quả spike để viết helper khóa ngày ở tuần 2); Hải (SQL Server trên CI từ thứ Hai).
**Việc dự phòng nếu chờ:** viết thêm test ràng buộc cho Catalog; đọc phần Billing (tuần 2–3).
**Không làm:** sửa module của Hiếu; thêm khóa theo ngày vào module khác khi spike chưa đạt; viết code Catalog API (để tuần 2).

> Spike C03 là **rủi ro lớn nhất của tuần**. Nếu đến thứ Tư kịch bản (1) hoặc (3) vẫn lỗi, báo ngay ở daily, đừng chờ thứ Sáu.

### 5.3 Bảo Anh (Frontend A): khung `web/`, đăng nhập

**Mục tiêu tuần:** có ứng dụng Next.js chạy được và lên VPS; đăng nhập thật với refresh hoạt động.
**Nhánh gợi ý:** `feature/web-scaffold`, `feature/web-app-shell`, `feature/web-api-client`, `feature/web-auth`.
**Trạng thái 11/11:** BAO-1.1 và BAO-1.3 phần lớn đã có trên `develop` (commit 8fde101: khung, menu theo quyền, bảng, form, toast, 403/404, Playwright, MSW). Còn lại: (1) `package-lock.json` (đang nằm trong PR #17, hoặc commit riêng); (2) BAO-1.2: `npm run gen:api` hiện đọc từ API đang chạy; khi Hiếu push `docs/api/openapi.json` thì cho script đọc được từ file đó để CI không cần chạy API, và để Hải thêm bước kiểm tra lệch; (3) BAO-1.4 đăng nhập, refresh một lần, route guard client, nối `PermissionProvider` (hiện nhận danh sách quyền rỗng) với `permissions` của `/me`; hợp đồng ở `docs/api/identity-contract.md` (refresh còn là bản nháp); (4) duyệt PR #17 của Duy.

| Ngày | Việc | Cách làm | Xong khi |
|---|---|---|---|
| Thứ Hai | **BAO-1.1** dựng `web/` | Tạo dự án Next.js 16 (App Router, TypeScript strict, Tailwind) trong thư mục `web/` của repo này; thêm shadcn/ui, ESLint, Prettier, Vitest, MSW, khung Playwright; các script `dev`, `build`, `lint`, `test`, `gen:api`. Đặt `output: 'standalone'` trong `next.config` (Hải cần cho Docker). Cấu hình `rewrites` để trình duyệt gọi `/api/...` cùng origin và Next chuyển tới `http://localhost:5080` khi chạy cục bộ (giống cách Nginx làm trên VPS, nên không cần CORS). Địa chỉ API lấy từ biến môi trường, có `web/.env.example`. Không commit `.env` | `npm run build`, `lint`, `test` xanh; mở PR, Hải nối CI |
| Thứ Ba | **BAO-1.3** khung ứng dụng | Layout, menu ẩn/hiện theo quyền, toast, error boundary, skeleton, trang 403/404, bộ form (React Hook Form + Zod), bảng dữ liệu, hộp thoại xác nhận. Dựng sẵn để Duy dùng từ tuần 2 | Duy review và dùng được |
| Thứ Ba | **BAO-1.2** client từ OpenAPI | Ngay khi Hiếu commit file OpenAPI: chọn công cụ sinh client (đề xuất `openapi-typescript`, nhóm chốt), `npm run gen:api`; code sinh nằm trong thư mục riêng, **không sửa tay**. CI báo đỏ khi file OpenAPI đổi mà client chưa sinh lại (Hải nối) | Đổi OpenAPI không sinh lại ⇒ CI đỏ |
| Thứ Tư | **BAO-1.4** đăng nhập trên mock | Dùng MSW giả lập `login`, `refresh`, `me`, `logout`, `change-password` theo `docs/api/identity-contract.md`. Access token **giữ trong bộ nhớ**; refresh bằng cookie. **Làm mới một lần cho nhiều request đồng thời** (nhiều request cùng gặp 401 chỉ gọi refresh một lần, các request còn lại chờ kết quả đó). Vì cookie refresh bị giới hạn đường dẫn `/api/identity`, trang web **không** đọc được nó ở middleware của Next.js, nên route guard làm phía client: khi tải trang gọi refresh để khôi phục phiên rồi mới quyết định cho vào hay chuyển về đăng nhập | Đăng nhập, đăng xuất, guard hoạt động trên mock; test Vitest cho nhiều request đồng thời |
| Thứ Năm | **Sáng: web lên VPS cùng Hải; chiều: chuyển mock sang API thật** | Sửa lỗi build/standalone nếu Hải gặp. Tắt mock khi gọi staging (biến môi trường). Kiểm tra: đăng nhập thật, đổi mật khẩu thật, refresh sau khi hết 15 phút (chờ thật một lần trên staging; phần còn lại kiểm bằng test với MSW) | Đăng nhập thật trên `staging.quanlynh.id.vn`; hết hạn 15 phút không bị đá ra khi còn refresh hợp lệ |
| Thứ Sáu | Sửa lỗi tích hợp, chuẩn bị demo | | Demo mục 7 chạy trơn |

**Bàn giao:** Hiếu → Bảo Anh (hợp đồng API thứ Ba 17:00; refresh trên staging thứ Năm). Nếu trễ: tự viết kiểu dữ liệu tạm theo `identity-authentication.md`, sửa lại khi có OpenAPI. Hải ← Bảo Anh (khung `web/` build được, trước thứ Năm sáng).
**Việc dự phòng nếu chờ:** hoàn thiện bộ form/bảng dữ liệu; viết thêm test Vitest.
**Không làm:** lưu access token hoặc refresh token trong `localStorage`; ghi cứng địa chỉ API; sửa tay code sinh từ OpenAPI.

### 5.4 Duy (Frontend B): realtime, âm thanh, thành phần vận hành, màn mở ngày

**Mục tiêu tuần:** có sẵn bộ thành phần để tuần 2–4 dựng màn bếp/bar/phục vụ ngay; màn mở ngày bán chạy với API thật.
**Nhánh gợi ý:** `feature/web-realtime-sound`, `feature/web-ops-components`, `feature/web-open-sales-day`.
**Trạng thái 11/11:** PR #17 (`feature/duy-week-one`) đang mở với DUY-1.1, DUY-1.2, DUY-1.3 (làm trên mock). Việc còn lại: (1) Bảo Anh duyệt PR vì nó sửa `next.config.ts`, `package.json`, `tsconfig.json`, `eslint.config.mjs` thuộc phần của Bảo Anh (mục 3.5); (2) nối màn mở ngày với API thật khi SalesDay lên staging; hợp đồng đã có ở `docs/phuong-tuan-1.md` (mã lỗi `salesday.previous_not_closed`, `salesday.idempotency.conflict`...); `POST /open` cần header `Idempotency-Key` là UUID, sinh một khóa cho mỗi ý định mở ngày và giữ nguyên khi thử lại.

| Ngày | Việc | Cách làm | Xong khi |
|---|---|---|---|
| Thứ Hai | **DUY-1.1** thành phần realtime + âm thanh (bắt đầu) | Hub thật chỉ có ở tuần 4, nên làm **một giao diện (interface) cho hub** và một bản giả để chạy thử. Bọc `@microsoft/signalr`: tự kết nối lại, **khi nối lại phải tải lại trạng thái đầy đủ** (không phát lại sự kiện cũ), khử trùng lặp bằng `eventId` (nhớ N sự kiện gần nhất). Bộ phát âm thanh: mở khóa sau lần thao tác đầu của người dùng (trình duyệt chặn tự phát), nút tắt tiếng lưu bền, và **hiện báo hiệu thị giác khi âm thanh bị chặn** (chỉ nghe thôi thì dễ bỏ sót món). Chỉ phát âm cho sự kiện **mới phát sinh sau khi nối**, không phát cho dữ liệu của ảnh chụp trạng thái | Test đơn vị (đồng hồ giả, Vitest): nối lại không phát âm cũ; sự kiện trùng chỉ phát một lần |
| Thứ Hai–Thứ Tư | **DUY-1.2** thành phần vận hành | Nhãn trạng thái, đồng hồ chờ (tính từ mốc thời gian của dữ liệu, **không** đếm cộng dồn để khỏi lệch khi tab bị ngủ), ô nhập tiền VND (số nguyên, định dạng `vi-VN`, không số lẻ), bộ tăng giảm số lượng, khung in chứng từ (CSS in khổ hẹp). Dùng token giao diện và `components/ui` của Bảo Anh; thêm vào thư mục của mình (mục 3.5). Trang demo nội bộ chỉ chạy ở môi trường phát triển (ví dụ `/dev/components`) | Có trang demo; Bảo Anh review |
| Thứ Ba–Thứ Tư | Đọc hợp đồng SalesDay của Phương, làm màn mở ngày **trên mock** | MSW cho `GET /api/salesday/current` và `POST /api/salesday/open`, các mã lỗi (đã có ngày chưa chốt, không đủ quyền) | Màn chạy được với mock đủ các trạng thái |
| Thứ Năm | **DUY-1.3** màn mở ngày + banner trạng thái ngày với API thật | Nhập tiền đầu ngày, mở ngày; banner hiển thị ĐANG BÁN / CHỜ CHỐT / ĐÃ CHỐT; xử lý lỗi theo `code` | Mở ngày từ giao diện trên staging; mở lần hai thấy thông báo lỗi đúng |
| Thứ Sáu | Sửa lỗi tích hợp, chuẩn bị demo | | Demo mục 7 |

**Bàn giao:** Phương → Duy (hợp đồng + `POST /open` trên staging, thứ Tư–Năm). Nếu trễ: làm trên mock, nối API thật thứ Sáu hoặc đầu tuần 2.
**Việc dự phòng nếu chờ:** viết thêm test cho thành phần âm thanh và realtime; chuẩn bị kịch bản Playwright (`DUY-2.3`, bắt đầu từ tuần 2).
**Không làm:** sửa `src/components/ui` hay cấu hình dự án của Bảo Anh mà không qua PR; phát âm thanh trong lúc render (chỉ phát từ xử lý sự kiện).

### 5.5 Hải (DevOps kiêm Tester): VPS, HTTPS, CI/CD, chiến lược test

**Mục tiêu tuần:** từ chưa có gì tới web + API chạy trên VPS qua HTTPS, có CI/CD bước 1 và nhánh được bảo vệ. Tuần này Hải có nhiều mã việc nhất và là người mà FE, BE đều chờ, nên cần thứ tự chặt.
**Nhánh gợi ý:** `chore/infra-docker`, `chore/infra-compose-staging`, `ci/frontend-workflow`, `ci/push-images`, `docs/test-strategy`.
**Trạng thái 11/11:** chưa thấy trên GitHub Dockerfile, compose, workflow mới hay thay đổi bảo vệ nhánh (`develop` vẫn chưa được bảo vệ). Hải cập nhật `docs/AGENT_PROGRESS.md` mục 5 với trạng thái thật của VPS/DNS. Việc cụ thể thêm so với bảng bên dưới: (1) mở rộng bộ lọc CI để chạy cả `PhuongWeekOneSqlServerTests` (hoặc bỏ bộ lọc khi đã có SQL Server), vì hiện 5 test của Phương chưa chạy trên CI; (2) thêm workflow cho `web/` (`npm ci`, `lint`, `test`, `build`, lọc theo đường dẫn `web/**`), cần `package-lock.json` từ PR #17; (3) dọn nhánh đã merge và bật tự xóa nhánh sau merge; (4) xử lý PR Dependabot #10–#13 sau khi CI xanh; (5) bảo vệ nhánh: repo công khai nên khả dụng, cần quyền quản trị repo. Việc Dockerfile web đã có sẵn `output: "standalone"` và rewrite `/api/*` tới `API_URL` nên không cần chờ Bảo Anh sửa thêm.

| Ngày | Việc | Cách làm | Xong khi |
|---|---|---|---|
| Thứ Hai | **HAI-1.0** thuê VPS (trước 12:00) | Nhóm chốt nhà cung cấp ở kickoff. Ubuntu LTS, đề xuất 4 vCPU/8 GB RAM, IP tĩnh, bật snapshot/backup của nhà cung cấp. Ghi nhà cung cấp, IP, người giữ khóa vào `docs/AGENT_PROGRESS.md` mục 7 | Truy cập SSH được |
| Thứ Hai | **HAI-1.1** DNS | Tạo bản ghi A `staging` → IP VPS tại nơi quản lý DNS của `quanlynh.id.vn` (cần người đăng ký tên miền cấp quyền; cần trước thứ Hai trưa). Nếu nhà đăng ký `.id.vn` cho đổi nameserver sang Cloudflare thì có thể dùng Cloudflare; **chưa kiểm chứng**, nếu không thì dùng DNS của nhà đăng ký | `nslookup staging.quanlynh.id.vn` ra đúng IP (DNS có thể mất thời gian lan truyền) |
| Thứ Hai | Dựng bảng công việc theo mã việc; bắt đầu **HAI-1.3** CI | Bảng Backlog → Ready → In Progress → Review → Done. CI: chạy mọi test `SqlServer` chứ không chỉ Identity (hiện bộ lọc là `FullyQualifiedName~IdentitySqlServer` trong `.github/workflows/backend-ci.yml`) | Bảng có đủ mã việc tuần 1 |
| Thứ Ba | **HAI-1.0** bảo mật VPS | Tạo user thường có sudo, đăng nhập SSH bằng khóa; **kiểm tra đăng nhập bằng khóa thành công ở một phiên khác trước khi tắt** đăng nhập mật khẩu và đăng nhập root (tránh tự khóa mình); tường lửa chỉ mở 22/80/443; fail2ban; tự cập nhật bảo mật; swap; cài Docker và Compose | Quét từ ngoài (từ mạng 4G) chỉ thấy 22/80/443 |
| Thứ Ba | **HAI-1.1** Nginx + HTTPS | Cài Nginx, xin chứng chỉ Let's Encrypt cho `staging.quanlynh.id.vn`, bật tự gia hạn, chuyển HTTP sang HTTPS, bật HSTS khi chắc chắn HTTPS ổn | Trình duyệt mở `https://staging.quanlynh.id.vn` thấy trang tạm, chứng chỉ hợp lệ |
| Thứ Ba | **HAI-1.2** bắt đầu | Dockerfile API nhiều tầng (SDK để build, ASP.NET runtime để chạy), không chạy root. `docker-compose.staging.yml` gồm: `sql` (volume, **không publish cổng 1433**), `api` (**không publish cổng**), `nginx` (cổng 80/443). Bí mật nằm trong `.env` trên VPS, **không commit**; có `.env.example` | Image API build được |
| Thứ Tư | **HAI-1.2** deploy tay lần đầu (**hạn: hôm nay**) | Đặt biến cấu hình: `ConnectionStrings__Restaurant`, `Jwt__SigningKey` (≥ 32 byte), `Jwt__Issuer`, `Jwt__Audience`. Sinh script migration idempotent từ máy dev (`dotnet ef migrations script --idempotent --project ... --context IdentityDbContext --output artifacts/identity.sql`), **xem lại** rồi chạy lên SQL của VPS (trong image SQL Server 2022, `sqlcmd` ở `/opt/mssql-tools18/bin/sqlcmd`, dùng cờ `-C`, theo cách CI đang làm). Tạo Admin bằng `--bootstrap-admin` với ba biến `BootstrapAdmin__*` (nhập mật khẩu bằng cách không lưu vào lịch sử lệnh). Kiểm tra `/health/live`, `/health/ready` | `https://staging.quanlynh.id.vn/health/live` trả 200 |
| Thứ Tư | **HAI-1.4** tài liệu deploy tay; **HAI-1.2b** bắt đầu | Ghi từng bước deploy thủ công (kéo image, migration, khởi động lại, kiểm tra sức khỏe, quay lui thủ công). Viết Dockerfile cho `web/` (dựa trên `output: 'standalone'` của Bảo Anh) và dịch vụ `web` trong compose; Nginx định tuyến `/api` tới `api`, `/` tới `web` | Có tài liệu; Dockerfile `web/` build được |
| Thứ Năm | **HAI-1.2b** deploy web (sáng, cùng Bảo Anh) | Deploy khung web lên VPS | `https://staging.quanlynh.id.vn/` hiện trang đăng nhập |
| Thứ Năm | **HAI-1.5** forwarded headers (cùng Hiếu) | Sau khi Hiếu thêm code: deploy và thử đăng nhập quá 10 lần/phút từ hai IP khác nhau (một IP bị chặn, IP kia vẫn dùng được). Nginx truyền `X-Forwarded-For`, `X-Forwarded-Proto`; **API chỉ nằm trong mạng Docker nội bộ** vì cấu hình tin proxy có thể bị lợi dụng nếu API mở cổng trực tiếp | Test sau proxy đạt |
| Thứ Năm | **HAI-1.3** bảo vệ nhánh; **HAI-1.4** GHCR | Cấu hình ruleset cho `main` và `develop` (PR + check `Build and test` + chặn force push; `develop` cần 1 approval). **Cần quyền quản trị repo; chủ repo cấp hoặc tự cấu hình.** Workflow đẩy image API và web lên GHCR sau CI xanh trên `develop` | PR thử CI đỏ bị chặn; image xuất hiện trên GHCR |
| Thứ Năm–Sáu | **HAI-1.6** `docs/test-strategy.md` | Tháp test, quy ước đặt tên, dữ liệu mẫu, mức độ lỗi S1–S4, mẫu báo lỗi, phân người cho ma trận 17 test | Cả nhóm đọc, xác nhận ở họp thứ Hai tuần 2 |
| Thứ Sáu | Kiểm tra nghiệm thu mục 7; chuẩn bị demo | Quét cổng từ ngoài; kiểm tra chứng chỉ; chạy checklist; bắt đầu thiết kế CD tự động + quay lui (làm tiếp tuần 2, `HAI-2.5`) | Mục 1 các dòng 1, 2, 7 đạt |

**Bàn giao:** Hải cần từ nhóm/người duyệt (VPS, thanh toán, quyền DNS, trước thứ Hai 12:00), từ Hiếu (script migration refresh, forwarded headers), từ Bảo Anh (`web/` build được, trước thứ Năm sáng). Hải bàn giao cho mọi người: URL staging (thứ Tư), quy trình deploy.
**Nếu VPS trễ:** trưa thứ Tư vẫn chưa có VPS thì báo người duyệt; Hải chạy cùng compose trên máy mình (không công khai) để Bảo Anh và Duy tích hợp; mốc "online" lùi theo. Không quay lại phương án tunnel.
**Không làm:** mở cổng 1433 hoặc cổng API ra ngoài; đặt bí mật vào git; bật "Production" với mật khẩu mặc định; tắt đăng nhập mật khẩu SSH trước khi kiểm chứng đăng nhập bằng khóa.

## 6. Bảng "ai chờ ai" trong tuần 1

| Người nhận | Cần từ | Cái gì | Hạn | Nếu trễ |
|---|---|---|---|---|
| Hải | Nhóm / người duyệt | VPS (IP, SSH) và quyền sửa DNS | Thứ Hai 12:00 | Chạy cục bộ không công khai; báo người duyệt thứ Tư |
| Bảo Anh | Hiếu | Hợp đồng API Identity và file OpenAPI | Thứ Ba 17:00 | Tự viết kiểu dữ liệu tạm theo `identity-authentication.md` |
| Bảo Anh | Hiếu | API refresh trên staging | Thứ Năm | Tiếp tục dùng mock |
| Duy | Phương | `POST /open` SalesDay trên staging | Thứ Năm | Làm trên mock, nối thật thứ Sáu hoặc tuần 2 |
| Hải | Bảo Anh | `web/` build được | Thứ Năm sáng | Hải chạy web cục bộ; không chặn việc khác |
| Hải | Hiếu | Script migration refresh; forwarded headers | Thứ Tư–Năm | Giới hạn đăng nhập tính theo IP proxy cho đến khi sửa |
| Hiếu, Phương | Hải | SQL Server dùng được trong CI/Docker | Thứ Hai | Dùng SQL Server Docker cục bộ |
| Cả nhóm | Hiếu | ADR-01 đã merge | Thứ Sáu | Họp contract tuần 2 lùi theo |
| Hiếu | Phương | Kết luận spike C03 | Thứ Sáu | Helper khóa ngày tuần 2 lùi theo |

## 7. Nghiệm thu thứ Sáu (demo 15:00 cho người duyệt)

**Kịch bản demo** (mở bằng điện thoại 4G hoặc mạng ngoài, không dùng wifi nhà để chứng minh là online thật):

1. Mở `https://staging.quanlynh.id.vn` ⇒ trang đăng nhập, ổ khóa chứng chỉ hợp lệ. *(Hải, Bảo Anh)*
2. Đăng nhập bằng tài khoản Admin/quản lý đã tạo bằng `--bootstrap-admin` ⇒ vào được; xem thông tin tài khoản. *(Bảo Anh, Hiếu)*
3. Mở ngày bán với tiền đầu ngày ⇒ banner "ĐANG BÁN"; thử mở lần hai ⇒ báo lỗi. *(Duy, Phương)*
4. Chứng minh refresh: để phiên quá 15 phút (hoặc dùng bản ghi video đã quay trước) mà vẫn không bị đá ra. *(Bảo Anh, Hiếu)*
5. Đổi mật khẩu ⇒ các phiên khác bị thu hồi. *(Hiếu)*
6. Hải trình bày: quét cổng ngoài chỉ thấy 22/80/443; thử PR có CI đỏ bị chặn; kết quả deploy tay có tài liệu.
7. Phương trình bày kết luận spike C03 (đạt/không đạt, bằng chứng).
8. Hiếu trình bày ADR-01 (3–5 quy ước chính).

**Sau demo (30'):** planning poker lại khối lượng từng module; **lệch > 20% so với mục 10 của kế hoạch ⇒ báo người duyệt ngay** để chọn: cắt phạm vi, kéo dài, hoặc thêm người.

**Bảng kết quả** (Hải điền thứ Sáu 17:00):

| Mục | Đạt / không đạt | Bằng chứng / link PR | Ghi chú, việc dồn sang tuần 2 |
|---|---|---|---|
| 1 VPS + bảo mật | | | |
| 2 HTTPS + web | | | |
| 3 Đăng nhập + refresh | | | |
| 4 Mở ngày | | | |
| 5 Spike C03 | | | |
| 6 ADR-01 | | | |
| 7 Bảo vệ nhánh + CI | | | |
| 8 Ước lượng lại | | | |

**Nếu không đạt:**
- Trượt ≤ 2 ngày và chỉ một mục: dồn sang đầu tuần 2, ghi vào bảng, không cắt phạm vi.
- Spike C03 không đạt, hoặc nhiều mục trượt: **họp khẩn với người duyệt trong 24 giờ** (mốc #0 trong kế hoạch).
- Chỉ hạ tầng trượt (VPS, DNS): báo ngay, không dùng bậc cắt giảm phạm vi để bù.

## 8. Rủi ro tuần 1

| Rủi ro | Dấu hiệu | Cách xử lý |
|---|---|---|
| Hiếu quá tải (5 mã việc + review + điều phối) | Thứ Tư vẫn chưa xong hợp đồng API hoặc refresh | Theo thứ tự ưu tiên ở mục 5.1; `HIE-1.4` sang tuần 2; Phương nhận review để Hiếu viết code |
| Spike C03 không đạt | Test đồng thời không ổn định | Báo trong 24 giờ; chuyển phương án trạng thái chờ bền vững; có thể làm chậm tuần 2 |
| VPS/tên miền trễ | Chưa có IP trưa thứ Hai | Chạy cục bộ không công khai; báo người duyệt; không bỏ cấu hình biến môi trường |
| Frontend chờ API, nhận hợp đồng muộn | Bảo Anh/Duy không có gì để làm | Làm trên mock, việc dự phòng ở mục 5; hợp đồng trước thứ Ba 17:00 là hạn cứng |
| Xung đột trong `web/` | PR của Bảo Anh và Duy sửa cùng file | Theo bảng thư mục mục 3.5; file cấu hình chỉ Bảo Anh sửa |
| Hải bị dồn việc, deploy trễ | Thứ Tư chưa có API trên VPS | Bảo Anh hỗ trợ việc `web/`, Phương hỗ trợ phần SQL/migration khi cần |
| Lỡ commit secret | Phát hiện token/mật khẩu trong git | Báo Hải ngay, **đổi bí mật đó**; xóa commit không đủ |

## 9. Điểm chưa kiểm chứng

**Đã kiểm chứng ngày 10–11/11:** `/openapi/v1.json` trả 200 ở môi trường Development (chỉ có ở Development, `app.MapOpenApi()`); Identity đã merge vào `develop` (PR #7, #8); repo `Trunghieu220504/restaurant-management-system` là **công khai**, `main` và `develop` **chưa được bảo vệ**; `web/next.config.ts` đã có `output: "standalone"` và rewrite `/api/:path*` tới `API_URL`.

**Còn chưa kiểm chứng:**

- Yêu cầu phiên bản Node.js của Next.js 16 (xem `engines` trong `web/package.json` nếu có) và việc build `standalone` chạy trong container.
- 5 test SQL Server của Phương (spike C03) mới chạy ở máy Phương, **chưa chạy trên CI**.
- Dockerfile của API, compose, Nginx + Let's Encrypt, `sqlcmd` trong image SQL Server (đường dẫn lấy từ CI hiện tại, dùng được cho CI), `docker compose run ... --bootstrap-admin` trên VPS: chưa dựng thử; thời lượng là ước lượng.
- `UseForwardedHeaders` đặt đúng vị trí giữa các middleware và việc hành vi chuyển hướng HTTPS khi đứng sau proxy: suy ra từ `Program.cs`, chưa chạy thử.
- Nhà đăng ký `.id.vn` có cho đổi nameserver sang Cloudflare hay không.
- Khối lượng tuần 1 của Hiếu và Hải sát hoặc vượt công suất; đây là ước lượng của người soạn, nhóm chưa xác nhận.
