# Sổ tiến độ và bàn giao cho AI agent (Claude, Codex)

> **File sống.** Mọi agent phải **đọc file này trước khi làm** và **cập nhật trước khi kết thúc**
> (mục 1). Khi file này mâu thuẫn với README hoặc tài liệu thiết kế cũ, ưu tiên theo mục 3.
> File `AGENT_HANDOFF.md` ở thư mục `docs/` ngoài repo (ngày 2026-11-01) chỉ còn là tài liệu nền;
> nó có phần lỗi thời (nhánh `chore/backend-foundation`, tên module cũ).

Cập nhật lần cuối: **2026-11-11** (đối chiếu với GitHub, xem mục 2 và 5). Người cập nhật: Claude.

---

## 1. Giao thức bắt buộc

### Trước khi làm

1. Đọc file này từ đầu đến hết mục 6 (trạng thái, việc đang làm, việc tiếp theo, chặn).
2. Chạy `git status`, `git branch --show-current`, `git log --oneline -n 10`. Không ghi đè hay hoàn tác
   thay đổi chưa rõ nguồn gốc. Nếu có thay đổi của người khác chưa commit trong module bạn định sửa, dừng và hỏi.
3. Đọc tài liệu nguồn liên quan đến task (mục 3). Đọc chi tiết nhiệm vụ trong
   [phan-cong-chi-tiet-7-tuan.md](phan-cong-chi-tiet-7-tuan.md) theo mã việc (ví dụ `HIE-1.3`).
4. Xác định **người giao việc** (Hiếu, Phương, Bảo Anh, Duy hoặc Hải) và module thuộc quyền họ (mục 4).
5. **Nhận việc:** thêm hoặc sửa dòng của task trong bảng mục 5 thành `In Progress`, ghi tên agent và nhánh.
   Việc này tránh hai agent làm trùng một task.

### Trong khi làm

- Giữ trong phạm vi task. Không tự đổi kiến trúc hay phạm vi MVP đã duyệt.
- Không suy diễn thành yêu cầu những điểm "chưa chốt". Gặp chỗ mơ hồ, ghi vào mục 7 (câu hỏi cho người) và làm phần không bị chặn.
- Tuân thủ quy tắc mục 8.

### Trước khi kết thúc (kể cả khi bị ngắt giữa chừng, nếu còn có thể)

1. Chạy các lệnh kiểm tra phù hợp (mục 9) và **ghi kết quả thật**, kể cả khi thất bại.
2. Cập nhật trạng thái task ở mục 5. Chỉ ghi `Done` khi tiêu chí "Xong khi" của task đã được kiểm chứng bằng lệnh hoặc test (ghi lệnh vào nhật ký). Chưa kiểm chứng đủ thì dùng `In Review` hoặc `Blocked` và nói rõ phần thiếu.
3. Thêm **một mục nhật ký** vào mục 10 (mẫu ở đầu mục đó).
4. Cập nhật mục 6 (việc tiếp theo) và mục 7 nếu có câu hỏi hoặc quyết định cần người duyệt.
5. Báo cáo cho người giao việc: file đã sửa, lệnh đã chạy, phần chưa kiểm chứng.

### Khi nhiều agent làm song song

- Mỗi agent làm trên **nhánh riêng**, và sửa file này **trong cùng nhánh/PR** với công việc của mình.
- Xung đột khi merge file này: **giữ cả hai bên** (nhật ký chỉ thêm, không xóa); với bảng mục 5, dòng mới hơn thắng nhưng không làm mất dòng khác.
- Không sửa hay xóa mục nhật ký của agent khác, kể cả khi thấy sai. Thêm mục nhật ký mới đính chính.

---

## 2. Trạng thái dự án (đối chiếu với GitHub ngày 2026-11-11)

**Mục tiêu:** hệ thống quản lý nhà hàng, web chạy trên VPS với HTTPS (`quanlynh.id.vn`), hoàn thành trong 7 tuần
(giả định thứ Hai 09/11/2026 đến Chủ nhật 27/12/2026; nhóm đã bắt đầu làm từ 09/11, **người duyệt vẫn chưa xác nhận chính thức**, mục 7 #1).
Không có gì bảo đảm tuyệt đối hoàn thành đúng hạn; xem cách phát hiện trượt và bậc cắt ở mục 9–10 của kế hoạch 7 tuần.

**Git:** repo công khai `Trunghieu220504/restaurant-management-system`, nhánh `main` và `develop` **chưa được bảo vệ**. Một nhánh cho mỗi task,
squash merge, Conventional Commits. `develop` hiện có Identity (PR #7, #8), SalesDay + Catalog của Phương (PR #16, 10/11) và khung `web/` của Bảo Anh
(commit `8fde101`). Đang mở: PR #17 `feature/duy-week-one` (Duy, 27 file, gồm cả `package-lock.json` và sửa vài file cấu hình thuộc phần của Bảo Anh),
và PR Dependabot #10–#13 (trùng với #2–#5 đã đóng). Nhiều nhánh đã merge chưa xóa; nên bật "Automatically delete head branches".
Trên máy Hiếu có nhánh `docs/identity-api-contract` (commit `e5cdc04`, đã sửa lại message; cần `push --force-with-lease`) và nhánh `docs/ke-hoach-7-tuan` (các tài liệu kế hoạch này).
Thư mục `C:/Users/MSI` (cha của thư mục dự án) cũng là một repo git: chỉ chạy `git` bên trong thư mục `restaurant-management-system`.

**Đã có (đối chiếu code và GitHub; kiểm chứng riêng ghi ở nhật ký):**

- Solution .NET 10 (`RestaurantManagement.slnx`), 10 module × 4 project, kiểm thử kiến trúc, CI backend.
- Identity: `POST /api/identity/login|logout|change-password`, `GET /api/identity/me`, JWT 15 phút + kiểm tra phiên trong SQL, giới hạn tốc độ
  (đăng nhập 10/phút/IP, đổi mật khẩu 5/phút/người dùng, trả 429), 5 vai trò + 26 mã quyền, `--bootstrap-admin`. **Chưa có refresh token.**
- SalesDay (Phương): `GET /api/salesday/current`, `POST /api/salesday/open` (quyền `salesday.manage`, header `Idempotency-Key` là UUID, mã lỗi trong `code`),
  khóa ứng dụng SQL Shared/Exclusive (spike C03: 5/5 test đạt ở máy Phương trên SQL Server 2022 Docker; **chưa chạy trên CI**). Xem `docs/phuong-tuan-1.md`, `docs/spike-c03-result.md`.
- Catalog: migration 9 bảng, chưa có API.
- Web (Bảo Anh): Next.js 16, `output: "standalone"`, rewrite `/api/:path*` → `API_URL`, shadcn/ui, TanStack Table, React Hook Form + Zod, Playwright, MSW,
  `npm run gen:api`. Chưa có đăng nhập thật (`PermissionProvider` nhận danh sách quyền rỗng), chưa có `package-lock.json` trên `develop`.
- OpenAPI chỉ bật ở Development: `/openapi/v1.json` trả 200 (đã kiểm chứng 10/11, 7 đường dẫn).

**Chưa có:**

- Nghiệp vụ của 7 module còn lại (Seating, Sales, Fulfillment, Billing, Inventory, Reporting, Audit): chỉ có khung rỗng.
- Refresh token, API quản lý tài khoản, policy theo mã quyền (chỉ có policy `SystemAdministration` và policy riêng của SalesDay).
- `UseForwardedHeaders` (nguy cơ vòng lặp HTTPS, giới hạn đăng nhập tính theo IP của proxy; suy ra từ đọc `Program.cs`, chưa chạy thử).
- Dockerfile, docker-compose, CD, workflow frontend, VPS, Cloudflare, R2, sao lưu: **chưa thấy trên GitHub** (việc của Hải; trạng thái VPS/DNS chưa được báo cáo).
  **Quyết định hạ tầng 2026-11-07:** thuê VPS ngay tuần 1, tên miền `quanlynh.id.vn`, `staging.quanlynh.id.vn`; không còn máy tạm hay Cloudflare Tunnel.
- DDL vật lý cho các module nghiệp vụ còn lại; ma trận 17 test (mới có test SQL Server của Identity và Phương).
- CI backend mới lọc test SQL Server theo `IdentitySqlServer`; cần mở rộng cho `PhuongWeekOneSqlServerTests` (`HAI-1.3`).

---

## 3. Nguồn sự thật và thứ tự ưu tiên

Khi các tài liệu mâu thuẫn, ưu tiên từ trên xuống:

1. Quyết định ghi trong file này (mục 7 sau khi người duyệt chốt).
2. `docs/superpowers/specs/2026-11-05-restaurant-mvp-scope.md` (phạm vi MVP đã duyệt 2026-11-05) và các quyết định C01–C06 (mục 4 bên dưới).
3. `docs/superpowers/specs/2026-11-05-database-revised-design.md` (mô hình dữ liệu logic, ma trận 17 test, quy tắc giao dịch).
4. [phan-cong-chi-tiet-7-tuan.md](phan-cong-chi-tiet-7-tuan.md) (phân công, tiêu chí xong từng việc). **Đang là đề xuất chờ duyệt**, dùng làm mã việc và tiêu chí.
5. `docs/backlog-va-phan-cong.md` (BL01–BL30), `README.md`, `docs/SYNC_STATUS.md`, `docs/identity-authentication.md`, `CONTRIBUTING.md`.
6. Tài liệu cũ ngoài repo (`../docs/superpowers/specs/2026-10-25-*`, `../docs/AGENT_HANDOFF.md`): chỉ tham khảo; mô hình kho/ca/MinIO ở đó đã lỗi thời.

**Quyết định đã duyệt (tóm tắt, đủ chi tiết ở tài liệu thiết kế):**

- **C01** nghĩa vụ = dòng gốc + điều chỉnh có dấu − miễn thu (≥ 0); hoàn tiền không tự giảm nghĩa vụ; miễn thu chỉ cho phần chưa thu.
- **C02** giá vốn bình quân gia quyền theo kỳ, làm tròn một lần ở tổng hợp.
- **C03** ghi theo ngày dùng khóa ứng dụng SQL Shared; chốt ngày dùng Exclusive; thứ tự khóa ngày → order/phiên → hóa đơn → thanh toán → kho; ngày: `DANG_BAN → CHO_CHOT → DA_CHOT`. **Điều kiện:** spike của Phương ở tuần 1 phải đạt, test 14 và 15 phải qua trên SQL Server thật trước khi tiền/kho chạy thật.
- **C04** chặn lưu trữ/đổi chế độ/đổi đơn vị chuẩn nguyên liệu khi còn tồn.
- **C05** mã truy cập khách gắn với phiên, còn hiệu lực sau chuyển bàn, thu hồi khi đóng phiên.
- **C06** phương án A: doanh thu thuần theo nghĩa vụ bán hàng; dòng tiền theo dõi riêng theo phương thức.
- Mặc định MVP: một order mở mỗi phiên; mỗi tùy chọn chọn một lần mỗi phần món; xác nhận khi giá đổi; không thu dư.
- Duyệt thiết kế **không** đồng nghĩa sẵn sàng migration: vẫn cần DDL vật lý có tên ràng buộc/chỉ mục, danh mục quyền và test ma trận.

---

## 4. Vai trò và sở hữu module

| Người   | Vai trò    | Sở hữu                                                                                               |
| ------- | ---------- | ---------------------------------------------------------------------------------------------------- |
| Hiếu    | Backend A  | Identity, Seating, Sales, Fulfillment, SignalR, nền tảng dùng chung (Contracts, Outbox, idempotency) |
| Phương  | Backend B  | SalesDay, Catalog, Billing, Inventory, Reporting                                                     |
| Bảo Anh | Frontend A | `web/`: khung Next.js, đăng nhập, quản trị tài khoản, bàn, thực đơn, gọi món, trang QR, báo cáo      |
| Duy     | Frontend B | Bếp/bar, phục vụ, thanh toán, hoàn tiền, ngày bán, kho, in chứng từ, realtime/âm thanh               |
| Hải     | DevOps/QA  | Hạ tầng, CI/CD, VPS, Cloudflare/R2, module Audit, chiến lược test, UAT                               |

Agent làm việc **thay mặt người giao việc** và chỉ sửa module của người đó. Muốn sửa module khác: tạo PR nhỏ riêng và ghi vào
mục 7 để chủ module duyệt. File chung (`Program.cs`, `ModuleRegistration.cs`, `Directory.Packages.props`,
`packages.lock.json`, `.github/workflows/*`) chỉ Hiếu hoặc Hải sửa.

---

## 5. Bảng trạng thái công việc

HIE-1.3: In Review (cục bộ, chờ người dùng push/PR) — Codex thay mặt Hiếu trên `feature/identity-refresh-token`.
Đã fetch và fast-forward tới `origin/develop` 258d90a; giữ nguyên thay đổi refresh dở dang và bản sao trong stash. Người dùng tự push sau kiểm chứng.
Kiểm chứng: 83/83 test đạt trên SQL Server thật (81 Integration + 2 Architecture), không skip.
Việc tiếp theo: người dùng push nhánh, mở PR vào develop, Phương review; Hải apply migration rồi Bảo Anh/Duy bật refresh và nghiệm thu staging.

**Quy ước:** mã việc lấy từ [phan-cong-chi-tiet-7-tuan.md](phan-cong-chi-tiet-7-tuan.md) (`HIE-`, `PHU-`, `BAO-`, `DUY-`, `HAI-`, `ALL-`).
**Việc không có dòng trong bảng này = `Todo`.** Chỉ thêm dòng khi bắt đầu. Trạng thái: `In Progress`, `In Review`
(đã có PR, chờ review/CI), `Blocked`, `Done`, `Dropped`.

| Mã              | Việc                                                    | Người giao  | Agent  | Trạng thái  | Nhánh / PR                                                                                                    | Ghi chú                                                                                                                      |
| --------------- | ------------------------------------------------------- | ----------- | ------ | ----------- | ------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| (nền)           | Identity: migration, danh mục quyền, bootstrap admin    | Hiếu        | —      | Done        | PR #7 (đã merge)                                                                                              | Theo `git log`; chưa chạy lại                                                                                                |
| HIE-1.1         | Đưa nhánh xác thực Identity vào `develop`               | Hiếu        | —      | Done        | PR #7, #8 (đã merge 05/11)                                                                                    | Có trên `develop`. Lệnh kiểm chứng: xem `git log origin/develop`                                                             |
| (nền)           | Hợp đồng API Identity: OpenAPI + `identity-contract.md` | Hiếu        | Claude | In Progress | `docs/identity-api-contract` (commit `e5cdc04`, đã push bản cũ; cần `--force-with-lease` sau khi sửa message) | Quá hạn 10/11 17:00; frontend đang chờ. Hiếu push và mở PR vào `develop`                                                     |
| PHU-1.1         | Spike C03: khóa ứng dụng SQL Shared/Exclusive           | Phương      | —      | In Review   | PR #16 (đã merge)                                                                                             | 5/5 test đạt ở máy Phương. **Chưa chạy trên CI, chưa có người khác review kết luận**; chưa coi là nghiệm thu (xem `HAI-1.3`) |
| PHU-1.2         | SalesDay mở ngày                                        | Phương      | —      | In Review   | PR #16 (đã merge)                                                                                             | `GET /current`, `POST /open`; chưa lên staging cho Duy                                                                       |
| PHU-1.3         | Catalog `InitialCatalog`                                | Phương      | —      | In Review   | PR #16 (đã merge)                                                                                             | Migration 9 bảng; chưa có API                                                                                                |
| BAO-1.1         | Dựng `web/`                                             | Bảo Anh     | —      | In Review   | commit `8fde101` trên `develop`                                                                               | Thiếu `package-lock.json`; chưa có workflow CI cho web                                                                       |
| BAO-1.3         | Khung ứng dụng (menu theo quyền, bảng, form)            | Bảo Anh     | —      | In Review   | commit `8fde101` trên `develop`                                                                               | `PermissionProvider` còn nhận danh sách quyền rỗng                                                                           |
| DUY-1.1/1.2/1.3 | Realtime/âm thanh, thành phần vận hành, màn mở ngày     | Duy         | —      | In Review   | PR #17 `feature/duy-week-one`                                                                                 | Đang mở; sửa file cấu hình của Bảo Anh nên cần Bảo Anh duyệt; màn mở ngày làm trên mock                                      |
| (nền)           | Viết kế hoạch 7 tuần                                    | Người duyệt | Claude | In Review   | chưa commit                                                                                                   | `docs/phan-cong-chi-tiet-7-tuan.md`; chờ người duyệt xác nhận mục 11 của file đó                                             |
| HAI-1.2b        | Sửa thứ tự khởi động Nginx HTTPS                        | Hải         | Hải    | In Progress | fix/hai-staging-startup                                                                                       | Đã thêm depends_on cho api/web; Compose config --quiet đạt; chưa thử khởi động lại stack trên VPS                            |

---

## 6. Việc tiếp theo (hàng đợi)

Thứ tự gợi ý cho **tuần 1**; chỉ bắt đầu khi không bị chặn bởi mục 7. Xem tiêu chí chi tiết theo mã việc trong kế hoạch.

**Chặn bởi người (chưa thể làm đến khi có xác nhận):**

- `HAI-1.0`: cần nhóm chốt nhà cung cấp VPS, cấu hình và người thanh toán, thuê trước 12:00 thứ Hai (mục 7, #6, #7).
- `HAI-1.1`: cần quyền sửa DNS `quanlynh.id.vn` cho Hải. Nếu đến trưa thứ Tư vẫn chưa có VPS: báo người duyệt; Hải chạy compose trên máy của mình (không công khai) để FE tích hợp.
- Toàn bộ kế hoạch: cần người duyệt xác nhận ngày bắt đầu và việc đổi chủ Catalog → Phương, Audit → Hải.

**Hiếu (Backend A):**

1. **Push `docs/identity-api-contract` và mở PR vào `develop`** (quá hạn; Bảo Anh đang chờ hợp đồng). `HIE-1.1` đã xong.
2. `HIE-1.3` refresh token (Bảo Anh cần để nối đăng nhập; hợp đồng nháp ở `docs/api/identity-contract.md`).
3. `HIE-1.2` ADR-01: quy ước Contracts, mã lỗi, idempotency, Outbox trừu tượng, helper khóa ngày; cập nhật `DependencyTests`. Nên thống nhất với cách Phương đã làm ở SalesDay (`Idempotency-Key`, trường `code`) thay vì làm lại.
4. forwarded headers (`HAI-1.5` trong kế hoạch; Hiếu sở hữu `Program.cs` nên Hiếu viết, Hải kiểm thử sau proxy), `HIE-1.5` Seating `InitialSeating`, `HIE-1.4` policy theo 26 mã quyền + `RequirePermission` (mềm, có thể sang tuần 2).

**Phương (Backend B):** `PHU-1.1`–`1.3` đã merge (PR #16) nhưng **chưa nghiệm thu**: (1) cùng Hải cho `PhuongWeekOneSqlServerTests` chạy trên CI rồi cập nhật `docs/spike-c03-result.md`; (2) lặp mỗi kịch bản ≥ 20 lần; (3) đưa SalesDay lên staging cho Duy; (4) review PR của Hiếu. Sau đó Catalog API và Billing theo kế hoạch tuần 2.

**Bảo Anh (Frontend A):** `BAO-1.1`, `BAO-1.3` đã có trên `develop`. Còn: `package-lock.json`; duyệt PR #17 của Duy; `BAO-1.2` client OpenAPI đọc từ `docs/api/openapi.json` khi Hiếu push; `BAO-1.4` đăng nhập thật (nối `PermissionProvider` với `permissions` của `/me`; refresh khi `HIE-1.3` xong).

**Duy (Frontend B):** PR #17 đang mở (`DUY-1.1`–`1.3`). Còn: xử lý nhận xét review của Bảo Anh về các file cấu hình; nối màn mở ngày với API SalesDay thật (`Idempotency-Key` UUID, mã lỗi `salesday.*`) khi có staging.

**Hải (DevOps/QA):** _GitHub chưa có Dockerfile, compose, workflow web hay bảo vệ nhánh (11/11); Hải cần báo trạng thái VPS/DNS ở mục 5 và 7._ Danh sách việc: `HAI-1.0` thuê và bảo mật VPS (thứ Hai–Ba); `HAI-1.1` DNS + HTTPS Let's Encrypt; `HAI-1.2` Dockerfile + compose + deploy tay API/SQL (chậm nhất thứ Tư); `HAI-1.2b` Dockerfile `web/` và deploy web (thứ Năm); `HAI-1.3` CI chạy mọi test SQL Server, workflow frontend, bảo vệ nhánh; `HAI-1.4` đẩy image lên GHCR, deploy thủ công; `HAI-1.5` forwarded headers; `HAI-1.6` chiến lược test. Tuần 2: `HAI-2.5` CD tự động + quay lui (thứ Ba 17/11), `HAI-2.6` sao lưu R2 + thử khôi phục.

**Lịch theo ngày của tuần 1 và bảng "ai chờ ai":** xem mục "Giao việc chi tiết tuần 1 theo ngày và theo người" trong [phan-cong-chi-tiet-7-tuan.md](phan-cong-chi-tiet-7-tuan.md). Việc phát sinh ngoài lịch thì ghi vào mục 7.

---

## 7. Câu hỏi mở, chặn và quyết định cần người duyệt

Thêm dòng mới ở cuối. Khi được trả lời, **không xóa**: điền cột "Quyết định" và ngày.

| #   | Ngày       | Người hỏi (agent) | Câu hỏi / chặn                                                                                                                                                                                                                                                                                                  | Quyết định (người duyệt)                                                                                                                                |
| --- | ---------- | ----------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | 2026-11-07 | Claude            | Ngày bắt đầu thực tế (giả định 09/11/2026) và mức độ dành thời gian của từng người                                                                                                                                                                                                                              | _chờ_                                                                                                                                                   |
| 2   | 2026-11-07 | Claude            | Đổi chủ Catalog → Phương và Audit → Hải                                                                                                                                                                                                                                                                         | _chờ_                                                                                                                                                   |
| 3   | 2026-11-07 | Claude            | VPS: nhà cung cấp, cấu hình (đề xuất ≥ 4 vCPU/8 GB RAM), đã mua chưa; quyền quản lý tên miền cho Hải; `.id.vn` có cho đổi nameserver sang Cloudflare không (chưa kiểm chứng)                                                                                                                                    | 2026-11-07: nhóm thuê VPS ngay tuần 1, tên miền đã đăng ký; phương án máy tạm/tunnel bị bỏ (xem #6, #7). Nhà cung cấp, cấu hình và quyền sửa DNS: _chờ_ |
| 4   | 2026-11-07 | Claude            | Tên người duyệt các cổng C01–C06 và có cần tài liệu ghi quyết định (người duyệt, ngày) không                                                                                                                                                                                                                    | _chờ_                                                                                                                                                   |
| 5   | 2026-11-07 | Claude            | Có cập nhật `backlog-va-phan-cong.md` theo 5 vai trò và lịch 7 tuần không                                                                                                                                                                                                                                       | _chờ_                                                                                                                                                   |
| 6   | 2026-11-07 | Claude            | _(Thay thế: câu hỏi máy tạm không còn áp dụng.)_ Ai thanh toán và giữ tài khoản/khóa SSH của VPS; ai là người đăng ký tên miền và cấp quyền sửa DNS cho Hải                                                                                                                                                     | _chờ_ (hạn trước 12:00 thứ Hai 09/11)                                                                                                                   |
| 7   | 2026-11-07 | Claude            | Nhà cung cấp và cấu hình VPS staging (đề xuất Ubuntu LTS, 4 vCPU/8 GB RAM; 4 GB nếu dùng SQL Server Express, chưa đo), ngân sách hằng tháng                                                                                                                                                                     | _chờ_ (hạn thứ Hai 09/11, thuê trước 12:00)                                                                                                             |
| 8   | 2026-11-11 | Claude            | Tài liệu kế hoạch nằm ngoài git; đã tạo nhánh `docs/ke-hoach-7-tuan` để đưa vào repo. Nhóm có đồng ý để các file `docs/phan-cong-chi-tiet-7-tuan.md`, `huong-dan-tuan-1.md`, sổ này, `AGENTS.md`, `CLAUDE.md` vào `develop` không (repo công khai: không chứa bí mật, tên thành viên có nên để công khai không) | _chờ_                                                                                                                                                   |
| 9   | 2026-11-11 | Claude            | Bật bảo vệ `main`/`develop` (bắt buộc PR + CI xanh), bật tự xóa nhánh sau merge, xóa nhánh đã merge; xử lý PR Dependabot #10–#13                                                                                                                                                                                | _chờ_ (cần quyền quản trị repo)                                                                                                                         |

---

## 8. Quy tắc làm việc cho agent

**Git**

- Nhánh `feature|fix|chore|docs/<module>-<nội-dung>` từ `develop`. **Không push thẳng vào `main` hoặc `develop`**; PR vào `develop`, squash merge, CI xanh và ≥ 1 reviewer (xem [CONTRIBUTING.md](../CONTRIBUTING.md)).
- Commit theo Conventional Commits. Agent chỉ commit hoặc push khi người giao việc yêu cầu.
- **Không thêm dòng `Co-Authored-By` của AI (Claude, Codex...) vào commit hay mô tả PR, và không thêm dòng "Generated with Claude Code".** Tác giả commit chỉ là người giao việc. (Quy ước của Hiếu, 11/11/2026.)
- Không `git push --force`, `git reset --hard`, hay xóa nhánh nếu không được yêu cầu rõ.
- Quy tắc Git đầy đủ cho cả nhóm (đặt tên nhánh theo mã việc, thời gian sống nhánh, kích thước PR, xử lý xung đột file khóa/migration, không `git add .`) nằm ở mục "Quy tắc làm việc với Git" trong [phan-cong-chi-tiet-7-tuan.md](phan-cong-chi-tiet-7-tuan.md); khi mâu thuẫn, dùng bản nghiêm ngặt hơn.

**Kiến trúc** (kiểm thử `tests/Restaurant.ArchitectureTests/DependencyTests.cs` bắt buộc)

- Mỗi module có đúng 4 project; Domain không tham chiếu gì, Application chỉ → Domain, Infrastructure → Domain + Application, Presentation → Application.
- Domain và Application không có `PackageReference` hay `FrameworkReference`. Không dùng file `.props` trong module để lách quy tắc.
- Liên module chỉ qua project Contracts (sẽ tạo ở `HIE-1.2`; cần cập nhật `DependencyTests` trong cùng PR) và sự kiện Outbox. Không chung entity EF, không FK vật lý chéo schema.
- Mỗi module có DbContext, schema và lịch sử migration riêng. Migration luôn **tương thích ngược**. Host **không tự migrate**.
- Không migration nghiệp vụ khi chưa có DDL vật lý đã rà soát cho module đó.

**Chất lượng**

- Tuân thủ `Directory.Build.props` (`TreatWarningsAsErrors`) và khóa gói (`packages.lock.json`, restore `--locked-mode`). Thêm gói mới cần cập nhật `Directory.Packages.props` và lock file; đó là file chung (mục 4).
- Hành vi mới hoặc sửa lỗi: viết hoặc cập nhật test trước. Thao tác tiền/kho/chốt ngày cần test SQL Server thật và idempotency.
- Không báo "xong" khi chưa chạy kiểm tra. Nếu không chạy được (thiếu SQL Server, thiếu Docker…), nói rõ trong nhật ký.

**Bảo mật**

- Không commit secret, chuỗi kết nối, khóa JWT, mật khẩu hay token Cloudflare/R2. `.env` đã bị `.gitignore`.
- Không công bố cổng API/SQL ra ngoài; không tắt kiểm tra TLS hay xác thực để "cho chạy được".
- Mã nhạy cảm (đổi quyền, tiền, hoàn tiền, chốt ngày) phải có audit: người thao tác, thời gian, lý do, trước/sau.

**Phạm vi**

- Phạm vi MVP đã duyệt; mục hoãn (thẻ, thanh toán nhiều hóa đơn, hoàn tiền theo món, sửa ngày đã chốt, giao hàng, đa chi nhánh, báo cáo/NCC nâng cao, cảnh báo quá hạn tự động) **không được làm** nếu người duyệt chưa đổi phạm vi.
- Các mục "cắt giảm" ở mục 9 kế hoạch 7 tuần chỉ áp dụng khi người duyệt đồng ý.

---

## 9. Lệnh kiểm tra (theo `.github/workflows/backend-ci.yml`)

Chạy từ thư mục gốc repo `restaurant-management-system`:

```bash
dotnet restore RestaurantManagement.slnx --locked-mode
dotnet format whitespace RestaurantManagement.slnx --verify-no-changes --no-restore
dotnet build RestaurantManagement.slnx -c Release --no-restore
dotnet test RestaurantManagement.slnx -c Release --no-build --no-restore
```

- SDK theo `global.json` (10.0.300; máy khởi tạo sổ có `dotnet 10.0.300`).
- Test SQL Server của Identity: CI đặt biến môi trường `IDENTITY_TEST_SQLSERVER` trỏ tới SQL Server dùng một lần
  (Docker `mcr.microsoft.com/mssql/server:2022-latest`) và lọc `FullyQualifiedName~IdentitySqlServer` trong
  `tests/Restaurant.IntegrationTests`. Xem `IdentitySqlServerAuthenticationTests.cs` để biết cách test tự lấy kết nối.
- Frontend (khi có `web/`): lệnh sẽ được `BAO-1.1` và `HAI-1.3` ghi vào đây.

---

## 10. Nhật ký làm việc (chỉ thêm, mới nhất ở **cuối**)

**Mẫu một mục:**

```markdown
### YYYY-MM-DD HH:mm — <Claude|Codex> — <mã việc> — thay mặt <người giao việc>

- Nhánh / PR:
- Đã làm:
- File chính đã sửa:
- Lệnh đã chạy và kết quả (dán kết quả thật, kể cả thất bại):
- Chưa kiểm chứng / còn thiếu:
- Việc tiếp theo cho người hoặc agent sau:
- Quyết định mới (cần người duyệt? đã thêm vào mục 7?):
```

### 2026-11-07 — Claude — (nền) kế hoạch 7 tuần và sổ này — thay mặt người duyệt

- Nhánh / PR: chưa commit (đang ở nhánh `feature/identity-authentication`).
- Đã làm:
    - Đọc README, backlog, spec MVP, thiết kế dữ liệu C01–C06, `Program.cs`, cấu hình xác thực, endpoint Identity, danh mục quyền, kiểm thử kiến trúc, CI.
    - Viết `docs/phan-cong-chi-tiet-7-tuan.md` (phân công 5 người × 7 tuần, tiêu chí xong, mốc, bậc cắt giảm) và cập nhật theo hướng VPS ngay từ tuần 1.
    - Tạo file này, `AGENTS.md`, `CLAUDE.md`.
- File chính đã sửa: `docs/phan-cong-chi-tiet-7-tuan.md`, `docs/AGENT_PROGRESS.md`, `AGENTS.md`, `CLAUDE.md` (đều mới, chưa commit).
- Lệnh đã chạy: `git branch/log/status`, `dotnet --version` (10.0.300), đọc file. **Không chạy build hay test** trong lần này; các số liệu như "64 test" lấy từ `docs/identity-authentication.md`, chưa được xác nhận lại.
- Chưa kiểm chứng: PR của `feature/identity-authentication` đã tạo/merge chưa; spike C03 khả thi trên SQL Server thật; `.id.vn` đổi được nameserver sang Cloudflare; hành vi âm thanh trên Safari iOS/Android; ước lượng công suất backend (mục 10 kế hoạch, ước lượng của Claude, nhóm chưa xác nhận).
- Việc tiếp theo: người duyệt trả lời mục 7 (#1–#5); sau đó các agent nhận việc tuần 1 theo mục 6.
- Quyết định mới: chạy trên VPS từ tuần 1 (người dùng chọn); phần còn lại ở mục 7. **Mục này đã bị thay thế bởi mục nhật ký ngay bên dưới.**

### 2026-11-11 — Hải — HAI-1.2b

- Nhánh: fix/hai-staging-startup
- Đã làm: Thêm depends_on cho nginx trong infra/compose.backend.https.yml.
- Kiểm tra: git diff --check không báo lỗi.
- Kiểm tra: Docker Compose v5.1.4, config --quiet thành công.
- Kiểm tra: Compose nhận đủ sqlserver, api, web, nginx.
- Chưa kiểm chứng: Docker build, khởi động lại stack và smoke test HTTPS trên VPS.
- Tiếp theo: Commit, push, tạo Pull Request và review.

### 2026-11-11 — Codex — HIE-1.3 — thay mặt Hiếu

- Nhánh: feature/identity-refresh-token; cập nhật từ origin/develop 258d90a, không push.
- Hoàn thiện refresh cookie, hash SQL, rotation, thu hồi chuỗi khi dùng lại, rollback khi issuer lỗi và logout bằng cookie khi access token hết hạn.
- Đối chiếu auth client PR #24; không đưa frontend chưa merge vào nhánh này.
- Cập nhật OpenAPI, hợp đồng API, hướng dẫn migration/rollback, nội dung PR; CI chọn mọi lớp SqlServer, giữ tên job bắt buộc.
- Test lỗi trước sửa: reuse tức thời và logout khi access session hết hạn đều thất bại đúng hành vi; sau sửa toàn bộ 81 Integration + 2 Architecture đạt, 0 lỗi, 0 skip trên SQL Server localhost với database thử nghiệm riêng. 14 test refresh đạt.
- Lệnh: dotnet test RestaurantManagement.slnx -c Release --no-restore --verbosity quiet (có IDENTITY_TEST_SQLSERVER); dotnet restore --locked-mode; dotnet format whitespace --verify-no-changes --no-restore; git diff --check đều đạt.
- EF has-pending-model-changes báo không lệch; sinh script idempotent tại TestResults/Refresh/identity-idempotent.sql; xuất OpenAPI từ host Development tạm và đã dừng host.
- Chưa nghiệm thu: GitHub CI sau push, HTTPS/browser staging, phối hợp refresh giữa nhiều tab. Test SQL không chứng minh các phần này.
- Tiếp theo: người dùng push, mở PR vào develop và nhờ Phương review; apply migration trước deploy API, frontend build với NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true khi endpoint sẵn sàng. Bản stash trước đồng bộ vẫn giữ để dự phòng.

### 2026-11-07 — Claude — (nền) cập nhật hạ tầng tạm và giao việc chi tiết tuần 1 — thay mặt người duyệt

- Nhánh / PR: chưa commit (đang ở nhánh `feature/identity-authentication`).
- Đã làm: theo yêu cầu "làm bản đề xuất này và giao lại việc chi tiết cho 5 người", sửa `docs/phan-cong-chi-tiet-7-tuan.md`:
    - Staging tuần 1 chạy tạm trên máy của nhóm qua Cloudflare Tunnel; chuyển VPS muộn nhất 22/11 (việc mới `HAI-2.5`).
    - Sửa `HAI-1.0`, `HAI-1.1`, `HAI-1.2`, `HAI-1.4`, `HAI-2.3`; thêm `DUY-2.3` (Duy hỗ trợ Hải viết smoke test).
    - Thêm bảng giao việc theo ngày và theo người cho tuần 1 kèm bảng "ai chờ ai"; sửa mốc #1 và mục 11, 12.
    - Sửa sổ này: mục 2, 6, 7 (#3, #6, #7) và nhật ký.
- File chính đã sửa: `docs/phan-cong-chi-tiet-7-tuan.md`, `docs/AGENT_PROGRESS.md`.
- Lệnh đã chạy: chỉ chỉnh sửa tài liệu, không build hay test.
- Chưa kiểm chứng: tunnel có tên cần tên miền nằm trên Cloudflare (chưa biết `.id.vn` có cho đổi nameserver không); tunnel nhanh "không cần tài khoản, URL ngẫu nhiên" lấy từ trí nhớ; SQL Server Express đủ cho staging (chưa đo); độ ổn định máy tạm; DUY-2.3 là thay đổi phân công mới cần Duy và người duyệt đồng ý.
- Việc tiếp theo: người duyệt trả lời mục 7 (#1–#7), nhất là #6 (máy tạm, hạn 09/11) và #7 (ngân sách VPS, hạn 15/11).
- Quyết định mới: staging tạm qua tunnel rồi chuyển VPS trước 22/11; đã ghi ở mục 7 (#3). **Mục này đã bị thay thế bởi mục nhật ký ngay bên dưới.**

### 2026-11-07 — Claude — (nền) viết lại kế hoạch tuần 1–2 theo hướng thuê VPS ngay tuần 1 — thay mặt người duyệt

- Nhánh / PR: chưa commit (đang ở nhánh `feature/identity-authentication`).
- Đã làm: theo yêu cầu "tuần này nhóm thuê VPS, đã đăng ký tên miền, có frontend và backend, có triển khai online", sửa `docs/phan-cong-chi-tiet-7-tuan.md`:
    - Bỏ máy tạm và Cloudflare Tunnel. `HAI-1.0` = thuê và bảo mật VPS; `HAI-1.1` = DNS + Let's Encrypt; `HAI-1.2` bỏ cloudflared; thêm `HAI-1.2b` (deploy `web/` lên VPS, thứ Năm); `HAI-1.4` chỉ đẩy GHCR + deploy thủ công trong tuần 1.
    - Tuần 2: `HAI-2.5` thành CD tự động + quay lui (hạn thứ Ba 17/11); thêm `HAI-2.6` sao lưu R2 + thử khôi phục; `HAI-4.2` chỉ còn hoàn thiện.
    - Viết lại lịch theo ngày tuần 1 (mốc: API+SQL thứ Tư, web thứ Năm, demo thứ Sáu), bảng "ai chờ ai" (thêm dòng VPS và dòng web của Bảo Anh), mốc #0 và #1, mục 11 (#3) và mục 12.
    - Sửa sổ này: mục 2, 6, 7 (#3, #6, #7) và nhật ký.
- File chính đã sửa: `docs/phan-cong-chi-tiet-7-tuan.md`, `docs/AGENT_PROGRESS.md`.
- Lệnh đã chạy: chỉ chỉnh sửa tài liệu, không build hay test.
- Chưa kiểm chứng: tiến độ thuê VPS thực tế; `.id.vn` cho đổi nameserver hay chỉ trỏ bản ghi A; Nginx + Let's Encrypt, SQL Server Docker, Next.js standalone chưa dựng thử trong repo; mốc "API thứ Tư, web thứ Năm" là ước lượng; SQL Server Express đủ cho staging (chưa đo).
- Việc tiếp theo: người duyệt/nhóm trả lời mục 7 #6, #7 trước 12:00 thứ Hai 09/11; mục 7 #1, #2 vẫn chờ.
- Quyết định mới: VPS ngay tuần 1, không máy tạm; đã ghi ở mục 7 (#3).

### 2026-11-07 — Claude — (nền) tạo hướng dẫn chi tiết tuần 1 — thay mặt người duyệt

- Nhánh / PR: chưa commit (đang ở nhánh `feature/identity-authentication`).
- Đã làm: tạo `docs/huong-dan-tuan-1.md` — mục tiêu và 8 kết quả cuối tuần, điểm xuất phát, cách làm việc chung, chuẩn bị máy, hướng dẫn từng người (Hiếu, Phương, Bảo Anh, Duy, Hải) theo ngày kèm cách làm và tiêu chí xong, bảng "ai chờ ai", kịch bản demo thứ Sáu, rủi ro, điểm chưa kiểm chứng.
- Đã đối chiếu với repo: lệnh CI (`--locked-mode`, `dotnet format whitespace`), biến `IDENTITY_TEST_SQLSERVER` (thiếu biến thì test bị skip), `LoginResult(AccessToken, ExpiresAt, Account)`, chính sách mật khẩu ≥ 12 ký tự.
- Lệnh đã chạy: chỉ đọc/đối chiếu, không build hay test.
- Chưa kiểm chứng: xem mục 9 của `docs/huong-dan-tuan-1.md` (đường dẫn OpenAPI, Node cho Next.js 16, Docker/Nginx, forwarded headers, nameserver `.id.vn`, bảo vệ nhánh trên gói repo hiện tại).
- Lưu ý: kế hoạch 7 tuần là bản gốc; nếu hai file lệch nhau, sửa hướng dẫn tuần 1 theo kế hoạch.
- Việc tiếp theo: người duyệt xác nhận ngày bắt đầu 09/11 và các mục 7 #6, #7 trước 12:00 thứ Hai.

### 2026-11-10 — Claude — Hiếu (Backend A) — hợp đồng API Identity

- Nhánh / PR: `docs/identity-api-contract` (1 commit cục bộ, **chưa push**, chưa có PR; người dùng tự push).
- Đã làm: khai báo kiểu response cho 4 endpoint Identity (không đổi logic), xuất `docs/api/openapi.json` từ host Development, viết `docs/api/identity-contract.md` (kiểu dữ liệu, ví dụ, bảng lỗi, bản nháp refresh `POST /api/identity/refresh`, quy tắc FE).
- Lệnh đã chạy: `dotnet build RestaurantManagement.slnx -c Release` (0 lỗi); `dotnet test` (ArchitectureTests 2/2; IntegrationTests 45 đạt, 22 bị bỏ qua vì thiếu `IDENTITY_TEST_SQLSERVER`); `dotnet format whitespace --verify-no-changes` (exit 0); `git diff --check` sạch.
- Đã kiểm chứng: đường dẫn `/openapi/v1.json` trả 200 và chứa 7 đường dẫn.
- Chưa kiểm chứng: test SQL Server của Identity (đã skip), tên cookie `rt` và hình dạng `RefreshResult` chỉ là đề xuất cho HIE-1.3.
- Việc tiếp theo: push + PR vào `develop`, nhờ Phương duyệt; sau đó HIE-1.3 (refresh token).

### 2026-11-11 — Claude — (nền) đối chiếu kế hoạch với GitHub, sửa tài liệu cho cả nhóm — thay mặt Hiếu (Backend A)

- Nhánh / PR: `docs/ke-hoach-7-tuan` (từ `develop` `3c26e5e`; commit cục bộ, **chưa push**, người dùng tự push).
- Đã làm: đối chiếu `develop` trên GitHub với kế hoạch, sửa `docs/phan-cong-chi-tiet-7-tuan.md` (bảng "Tiến độ thực tế", mục 2, 12), `docs/huong-dan-tuan-1.md` (mục 2, dòng "Trạng thái 11/11" cho từng người, mục 4, 9) và file này (mục 2, 5, 6, 7). Giải thích vì sao GitHub có nhiều nhánh (nhánh đã merge chưa xóa + Dependabot).
- Phát hiện: Identity, SalesDay, Catalog, khung `web/` đã vào `develop`; PR #17 của Duy đang mở; chưa thấy hạ tầng của Hải; spike C03 chưa chạy trên CI; repo công khai và chưa bảo vệ nhánh.
- Lệnh đã chạy: `git` (log/branch/diff/show trên `origin/develop`), đọc file. Kiểm chứng trước đó (10/11): `dotnet build` 0 lỗi; `dotnet test` ArchitectureTests 2/2, IntegrationTests 45 đạt, 22 bị bỏ qua vì thiếu `IDENTITY_TEST_SQLSERVER`.
- Chưa kiểm chứng: số liệu GitHub lấy từ ảnh chụp git cục bộ sau `fetch`, không phải từ giao diện GitHub; trạng thái VPS/DNS; PR #17 chưa được xem chi tiết từng file.
- Việc tiếp theo: Hiếu push `docs/identity-api-contract` rồi `docs/ke-hoach-7-tuan`; người duyệt trả lời mục 7 #1, #2, #4–#9.

### 2026-11-11 — Hải — HAI-1.2b

- Nhánh: fix/hai-staging-startup
- Đã làm: Thêm depends_on cho nginx trong infra/compose.backend.https.yml.
- Kiểm tra: git diff --check không báo lỗi.
- Kiểm tra: Docker Compose v5.1.4, config --quiet thành công.
- Kiểm tra: Compose nhận đủ sqlserver, api, web, nginx.
- Chưa kiểm chứng: Docker build, khởi động lại stack và smoke test HTTPS trên VPS.
- Tiếp theo: Commit, push, tạo Pull Request và review.
