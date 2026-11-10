# Kế hoạch phân công chi tiết 7 tuần

> Trạng thái: **ĐỀ XUẤT, chờ người duyệt xác nhận** các điểm ở mục 11. Tài liệu này chi tiết hóa
> [backlog-va-phan-cong.md](backlog-va-phan-cong.md) cho nhóm 5 người; khi mâu thuẫn, phạm vi MVP
> trong `superpowers/specs/2026-11-05-restaurant-mvp-scope.md` và các quyết định C01–C06 đã duyệt
> (mục 3) được ưu tiên.
>
> **Cập nhật 11/11/2026:** nhóm đã bắt đầu làm từ 09/11. Bảng "Tiến độ thực tế" ở đầu mục 6 ghi lại phần đã có trên GitHub
> (kiểm tra ngày 11/11); các nội dung kế hoạch bên dưới giữ nguyên mục tiêu, chỉ sửa chỗ đã khác thực tế.

## 1. Mục tiêu, giả định và giới hạn

**Mục tiêu:** sau 7 tuần có một ứng dụng web chạy trên HTTPS (`quanlynh.id.vn`) để một nhà hàng vừa
vận hành thật trong ngày: mở ngày bán → gọi món (nhân viên và khách qua QR) → bếp/bar chế biến có
thông báo âm thanh → giao món → thu tiền → đóng order → đếm tồn, chốt ngày bán, xem báo cáo cơ bản.

**Giả định (cần xác nhận):**
- Tuần 1 bắt đầu **thứ Hai 09/11/2026**, tuần 7 kết thúc **Chủ nhật 27/12/2026**.
- Mỗi người làm việc toàn thời gian cho dự án. Nếu thực tế ít hơn, các mốc phải lùi tương ứng.
- Phạm vi theo MVP đã duyệt 2026-11-05 (QR và âm thanh bắt buộc; không thẻ, không hoàn tiền theo món,
  không sửa ngày đã chốt, không giao hàng, không đa chi nhánh).

**Giới hạn cần nói rõ:**
- Đây là **web vận hành nội bộ + trang QR cho khách**, không phải website thương mại điện tử.
  Không có trang giới thiệu/SEO, thanh toán cổng online, ứng dụng di động hay chế độ offline.
- **Không có gì bảo đảm tuyệt đối hoàn thành đúng hạn.** Khối lượng backend ở mức sát công suất
  (mục 10). Kế hoạch được thiết kế để *phát hiện trượt sớm* (3 mốc kiểm tra) và có *bậc cắt giảm* đã
  thỏa thuận trước (mục 9), thay vì dồn rủi ro đến tuần 7.

## 2. Hiện trạng đầu vào (đã kiểm tra trong repo)

| Hạng mục | Hiện trạng |
|---|---|
| Backend | .NET 10, 10 module × 4 project, 64 test (theo identity-authentication.md), CI build/test/publish |
| Identity | Có login, me, logout, change-password; JWT 15 phút; **chưa có refresh token**, chưa có API quản lý tài khoản, chưa có policy theo quyền. Đã seed 5 vai trò (Admin, Manager, Staff, Kitchen, Bar) và 26 mã quyền |
| SalesDay, Catalog | Đã có (PR #16, merge 10/11): SalesDay có migration, `GET /api/salesday/current`, `POST /api/salesday/open` (quyền `salesday.manage`, `Idempotency-Key`); Catalog có migration 9 bảng, chưa có API |
| 7 module còn lại (Seating, Sales, Fulfillment, Billing, Inventory, Reporting, Audit) | Khung rỗng (DbContext, nhóm endpoint trống), chưa có entity/migration |
| Frontend | Có khung `web/` trên `develop` (Next.js 16, shadcn/ui, bảng, form, toast, trang 403/404, Playwright, MSW, `npm run gen:api`); **chưa có đăng nhập**, dữ liệu trên màn hình còn là mẫu. Phần realtime/âm thanh/màn mở ngày của Duy nằm ở PR #17 (đang mở) |
| Triển khai | Chưa có Dockerfile, compose, CD, workflow cho `web/` trên `develop`. Host không tự migrate; có lệnh `--bootstrap-admin` |
| Git | Repo **công khai** trên GitHub; `main` và `develop` **chưa được bảo vệ**. PR #7, #8 (Identity) đã merge vào `develop` (05/11). File build (`bin/obj/artifacts`) đã được `.gitignore`, không bị track |
| Thiết kế dữ liệu | `2026-11-05-database-revised-design.md`; C01–C06 đã duyệt (mục 3) nhưng DDL vật lý và test ma trận chưa chạy |

## 3. Quyết định đã chốt

| Mã | Quyết định |
|---|---|
| C01 | Nghĩa vụ = dòng gốc + điều chỉnh có dấu − miễn thu (≥ 0); còn phải thu = max(nghĩa vụ − thu ròng, 0); hoàn tiền không tự giảm nghĩa vụ |
| C02 | Giá vốn bình quân gia quyền theo kỳ có tồn đầu; điều chỉnh tồn có dấu và có giá trị; làm tròn một lần ở tổng hợp, lưu chênh lệch |
| C03 | Mọi thao tác ghi theo ngày lấy khóa ứng dụng SQL **Shared**; chốt ngày lấy **Exclusive**. **Điều kiện:** spike kỹ thuật tuần 1 phải đạt (mục 6, PHU-1.1) |
| C04 | Chặn lưu trữ/đổi chế độ/đổi đơn vị chuẩn nguyên liệu khi còn tồn hoặc có phát sinh trong ngày |
| C05 | Mã truy cập của khách gắn với phiên, còn hiệu lực sau chuyển bàn, thu hồi khi đóng phiên |
| C06 | **Phương án A:** doanh thu thuần theo nghĩa vụ bán hàng; dòng tiền theo dõi riêng theo phương thức |
| Mặc định MVP | Một order mở mỗi phiên; mỗi tùy chọn chọn một lần mỗi phần món; xác nhận khi giá đổi; không thu dư |

## 4. Vai trò, sở hữu module và quy tắc làm việc

| Người | Vai trò | Sở hữu |
|---|---|---|
| **Hiếu** | Backend A (điều phối contract) | Identity, Seating, Sales, Fulfillment, SignalR, nền tảng dùng chung (Contracts, Outbox, idempotency) |
| **Phương** | Backend B | SalesDay, Catalog, Billing, Inventory, Reporting |
| **Bảo Anh** | Frontend A (trưởng UI) | Khung Next.js, đăng nhập, quản trị tài khoản, bàn, thực đơn, gọi món nhân viên, trang QR khách, báo cáo |
| **Duy** | Frontend B | Bếp/bar, phục vụ, thanh toán, hoàn tiền, ngày bán, kho, in chứng từ, realtime/âm thanh |
| **Hải** | DevOps kiêm Tester | Hạ tầng, CI/CD, môi trường staging/production, module Audit, chiến lược và chạy test, UAT |

> **Thay đổi so với đề xuất trước:** Catalog chuyển từ Hiếu sang Phương và Audit chuyển sang Hải để cân
> tải (Hiếu giữ chuỗi Sales → Fulfillment → QR là đường găng). Cần người duyệt xác nhận (mục 11).

**Quy tắc chung**
1. Nhánh: `feature/<module>-<nội-dung>` từ `develop`, PR vào `develop`, squash merge, CI xanh, ≥ 1 reviewer
   (xem [CONTRIBUTING.md](../CONTRIBUTING.md)). `main` chỉ nhận từ `develop` qua PR do Hiếu merge.
2. Review chéo: Hiếu ↔ Phương (contract, migration, transaction); Bảo Anh ↔ Duy (UI dùng chung, API client);
   Hải kiểm tra CI xanh, có test, không lộ secret, không phá kiểm thử kiến trúc. Thời gian trả lời review ≤ 1 ngày làm việc.
3. **Mỗi người chỉ sửa module của mình.** Sửa chéo phải qua PR và được chủ module duyệt. File chung (`Program.cs`,
   `ModuleRegistration.cs`, `Directory.Packages.props`, `packages.lock.json`) chỉ Hiếu hoặc Hải sửa; người khác nhờ qua PR nhỏ riêng.
4. **Contract trước, code sau.** Thứ Ba mỗi tuần chủ module công bố bản nháp OpenAPI (endpoint, DTO, mã lỗi, quyền) cho tuần đó.
   Frontend dùng mock (MSW) đến khi API thật lên staging (mục tiêu thứ Năm).
5. Migration: mỗi module chỉ đổi schema của mình, tên `Initial<Module>` rồi `<Module><NộiDung>`, luôn **tương thích ngược**
   (thêm trước, xóa sau). Không tự migrate khi khởi động; chạy script idempotent trong bước deploy.
6. Ghi issue theo mã nhiệm vụ trong tài liệu này (ví dụ `HIE-3.2`) và mã BLxx; bảng Backlog → Ready → In Progress → Review → Done (Hải dựng ngày 1).
7. Nhịp họp: daily 15 phút; **thứ Hai** lập kế hoạch tuần (45'); **thứ Tư** rà soát contract/API (30'); **thứ Sáu** demo cho người duyệt + kiểm tra mốc (45').

### Quy tắc làm việc với Git

Phần này **bổ sung** cho [CONTRIBUTING.md](../CONTRIBUTING.md) (mô hình `main` + `develop`, squash vào `develop`, merge commit vào `main`, Conventional Commits) và không thay thế nó. Điểm nào dưới đây là đề xuất mới so với CONTRIBUTING.md được đánh dấu **(mới)**; nhóm xác nhận ở họp thứ Hai tuần 1.

**1. Nhánh**
- Chỉ có hai nhánh dài hạn: `main` và `develop`. **Không ai commit hay push thẳng vào hai nhánh này**, kể cả Hiếu và Hải.
- Mọi việc làm trên nhánh riêng tách từ `develop` mới nhất: `feature/<module>-<nội-dung>`, `fix/<module>-<lỗi>`, `chore/<việc>`, `docs/<tài-liệu>`. Frontend dùng `web` làm module (`feature/web-login`), hạ tầng dùng `chore/infra-...` hoặc `ci/...` **(mới: thêm `ci/`)**.
- **(mới)** Một nhánh = một mã việc trong kế hoạch này (ví dụ `HIE-1.3`). Không gộp hai việc không liên quan vào một nhánh.
- **(mới)** Nhánh sống tối đa **2 ngày làm việc**. Việc dài hơn thì cắt nhỏ (ví dụ migration một PR, API một PR, test một PR) để không ai giữ code chưa tích hợp quá lâu; đây cũng là cách giảm xung đột trong tuần có hai backend và hai frontend làm song song.
- Mỗi người chỉ đẩy lên nhánh của mình. Muốn sửa nhánh của người khác thì hỏi trước; không viết lại lịch sử (`rebase`, `commit --amend`) trên nhánh đã đẩy mà người khác đang dùng.

**2. Bắt đầu và đồng bộ**
- Đầu mỗi ngày làm việc: `git switch develop && git pull --ff-only origin develop`, sau đó cập nhật nhánh đang làm.
- Cập nhật nhánh từ `develop` bằng **`git merge develop`** (mặc định cho cả nhóm, không cần force push). Chỉ dùng `rebase` khi nhánh chưa ai khác dùng và bạn biết cách xử lý; sau rebase dùng `git push --force-with-lease` **chỉ trên nhánh của chính mình**.
- **Cấm** `git push --force` lên `main`/`develop`, `git reset --hard` trên nhánh có commit chưa đẩy, và xóa nhánh của người khác.

**3. Commit**
- Conventional Commits, scope là module hoặc thành phần: `feat(identity): add refresh token rotation (HIE-1.3)`. Loại: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `ci`. **(mới)** Mã việc để cuối tiêu đề.
- Mỗi commit là một thay đổi đọc được, biên dịch được; không commit code đang vỡ build (dùng nhánh riêng và PR nháp nếu cần chia sẻ).
- **Không dùng `git add .` hoặc `git add -A`.** Chỉ stage đúng file liên quan, rồi chạy `git diff --cached --check` và xem `git diff --cached` trước khi commit.
- **Không bao giờ commit:** `.env`, chuỗi kết nối, khóa JWT/SSH, mật khẩu, token Cloudflare/R2/GHCR, `bin/`, `obj/`, `node_modules/`, `.next/`, `appsettings.Local.json`. `.gitignore` đã chặn phần lớn; nếu lỡ commit secret, **báo Hải ngay và xoay (đổi) secret đó**, vì xóa commit không đủ khi đã push.
- Sau khi đổi package, chạy restore không có `--locked-mode` rồi commit `Directory.Packages.props` cùng các `packages.lock.json` (backend), hoặc `package.json` cùng `package-lock.json` (frontend).

**4. Pull Request**
- PR luôn vào `develop`, theo mẫu `.github/pull_request_template.md`. Tiêu đề giống tiêu đề commit (squash dùng tiêu đề này), mô tả nêu hành vi thay đổi, dữ liệu/migration và cách đã kiểm chứng.
- **Mở PR nháp (Draft) sớm** khi việc cần nhìn từ người khác; chỉ chuyển "Ready" khi tự kiểm tra xong.
- **Điều kiện merge:** CI xanh, nhánh đã cập nhật từ `develop`, ≥ 1 approval từ người **khác** tác giả (cặp review ở quy tắc chung số 2), mọi bình luận đã giải quyết. **Tác giả không tự merge khi chưa có approval.**
- **(mới) Kích thước:** PR nên dưới khoảng 400 dòng thay đổi (không tính file sinh tự động, migration và lock file); lớn hơn thì cắt. Người review trả lời trong ≤ 1 ngày làm việc; nếu quá hạn, báo ở daily.
- **(mới) Kiểm tra bắt buộc trước khi mở PR:**
  - Backend: restore khóa phiên bản, format, build, test và test kiến trúc (lệnh ở CONTRIBUTING.md).
  - Frontend: `npm run lint`, `npm run build`, `npm test`; nếu OpenAPI đổi thì chạy `npm run gen:api` và commit client đã sinh.
  - Đổi API: cập nhật OpenAPI trong cùng PR và báo người dùng API (Bảo Anh hoặc Duy) trong daily.
- Sau khi merge: xóa nhánh trên GitHub (sau khi xác nhận việc đã vào `develop`), cập nhật `develop` cục bộ.

**5. Xử lý xung đột và file dùng chung**
- File dùng chung (`Program.cs`, `ModuleRegistration.cs`, `Directory.Packages.props`, `packages.lock.json`, `.github/workflows/*`) chỉ Hiếu hoặc Hải sửa; người khác nhờ qua PR nhỏ riêng, tránh sửa chung một file trong cùng ngày.
- **Không sửa tay** file khóa (`packages.lock.json`, `package-lock.json`) hay client OpenAPI sinh tự động khi gặp xung đột: lấy bản của `develop`, rồi chạy lại restore/`npm install`/`gen:api` để sinh lại.
- Migration EF: mỗi module chỉ migration trong schema của mình. Nếu hai nhánh cùng tạo migration cho một module, người **merge sau** phải cập nhật từ `develop`, xóa migration của mình (chưa merge) và tạo lại để thứ tự đúng; không sửa tay `ModelSnapshot`. Không bao giờ sửa migration đã merge vào `develop`, hãy thêm migration mới.
- Xung đột mà bạn không chắc: dừng, không chọn bừa "giữ bản của tôi"; hỏi chủ module.

**6. Bảo vệ nhánh và phát hành** (Hải cấu hình ở `HAI-1.3`)
- `develop` và `main` đều qua PR, bắt buộc CI (`Build and test`; thêm check frontend khi có), chặn force push và xóa nhánh, giải quyết hết bình luận. `develop`: squash merge + 1 approval. `main`: chỉ merge commit từ PR `develop → main`.
- Ghi chú trạng thái: CONTRIBUTING.md nói đây là hướng dẫn chứ chưa phải cài đặt đã bật; chưa kiểm tra trên GitHub. Hải xác nhận lại ở `HAI-1.3`.
- Phát hành: **Hiếu** merge `develop → main` (CONTRIBUTING.md gọi người này là TV1; xác nhận đó là Hiếu), gắn tag `vMAJOR.MINOR.PATCH`. Hotfix: nhánh `hotfix/<lỗi>` từ `main`, PR vào `main`, sau đó merge `main` ngược vào `develop`.
- **(mới)** Staging chỉ deploy từ `develop` (CD ở `HAI-2.5`), production chỉ deploy từ tag trên `main`. Không deploy từ nhánh feature.

**7. AI agent (Claude, Codex) làm việc trong repo**
- Agent đọc `docs/AGENT_PROGRESS.md` trước, và **không commit, push hoặc tạo PR nếu người giao việc chưa yêu cầu**. Không làm việc trực tiếp trên `main`/`develop`.
- Commit do agent tạo có dòng `Co-Authored-By` của agent đó ở cuối; PR do agent tạo ghi rõ trong mô tả. Người giao việc vẫn là người chịu trách nhiệm review và merge.
- Agent không dùng `reset --hard`, `push --force` hay xóa nhánh khi chưa được yêu cầu rõ; không sửa file ngoài phạm vi module của người giao việc.

**Tóm tắt lệnh hằng ngày**

```bash
git switch develop && git pull --ff-only origin develop
git switch -c feature/<module>-<nội-dung>          # bắt đầu việc mới
git add <các file liên quan> && git diff --cached --check
git commit -m "feat(<module>): <nội dung> (<MÃ-VIỆC>)"
git push -u origin feature/<module>-<nội-dung>     # rồi mở PR vào develop
git merge develop                                  # cập nhật nhánh khi develop đã đổi
```

**Đưa tài liệu kế hoạch vào repo:** các file `docs/phan-cong-chi-tiet-7-tuan.md`, `docs/huong-dan-tuan-1.md`, `docs/AGENT_PROGRESS.md`, `docs/giai-thich-du-an-hien-tai.md`, `AGENTS.md`, `CLAUDE.md` đi vào `develop` qua nhánh `docs/ke-hoach-7-tuan` và một PR (Hiếu push và mở PR, Phương hoặc Hải duyệt). Nhánh `docs/identity-api-contract` là PR riêng, nên đi trước vì frontend đang chờ.

**Dọn nhánh (Hải hoặc Hiếu, sau khi xác nhận bằng `git branch -r --merged origin/develop`):** `chore/backend-foundation`, `chore/sync-salesday-docs`, `feature/identity-initialization`, `feature/identity-authentication` đã merge vào `develop` nên có thể xóa trên GitHub; bật "Automatically delete head branches" trong Settings. Bốn PR Dependabot (#10–#13) trùng với bản cập nhật cũ: Hải merge hoặc đóng sau khi CI xanh.

## 5. Quy ước kỹ thuật dùng chung (Hiếu công bố ở HIE-1.2, cả nhóm tuân theo)

- **API:** `/api/<module>/...`; JSON camelCase; enum dạng chuỗi; tiền là số nguyên VND; thời gian ISO-8601 UTC; phân trang `page`/`pageSize` (≤ 100).
- **Lỗi:** RFC 7807 ProblemDetails kèm trường `code` ổn định (ví dụ `order.batch.already_confirmed`) để FE hiển thị đúng thông báo.
- **Chống lặp:** header `Idempotency-Key` (Guid) bắt buộc cho thao tác tiền và đổi trạng thái; cùng khóa + cùng nội dung trả lại kết quả cũ, khác nội dung bị từ chối.
- **Xung đột cập nhật:** dùng row version (`ETag`/`If-Match` hoặc `expectedState`) cho mọi chuyển trạng thái.
- **Quyền:** mỗi endpoint khai báo mã quyền (26 mã đã seed) bằng một extension `RequirePermission("...")`; ghi quyền vào OpenAPI.
- **Liên module:** chỉ qua project `Restaurant.Modules.<X>.Contracts` (interface + DTO, không phụ thuộc gói) và sự kiện Outbox. Cập nhật `DependencyTests` trong cùng PR để cho phép đúng các tham chiếu này và không hơn.
- **Realtime:** một SignalR hub `/hubs/operations`; sự kiện gồm `eventId`, `occurredAt`, `version` để client khử trùng lặp; gửi **sau commit** qua Outbox; client kết nối lại phải tải lại trạng thái, không phát lại âm thanh cũ.
- **Frontend:** Next.js 16 App Router, TypeScript strict, Tailwind + shadcn/ui, TanStack Query, Zustand, React Hook Form + Zod; client API sinh từ OpenAPI. Đặt trong thư mục `web/` của repo này, CI riêng lọc theo đường dẫn (chốt thứ Hai tuần 1 giữa Bảo Anh và Hải).
- **Token phía web:** access token giữ trong bộ nhớ; refresh token trong cookie `HttpOnly; Secure; SameSite=Strict` giới hạn đường dẫn `/api/identity`. Cùng một origin (`staging.quanlynh.id.vn`) nên không cần CORS.

## 6. Kế hoạch theo tuần

Cột "Xong khi" là tiêu chí kiểm tra được. Mọi nhiệm vụ backend bao gồm migration, test tích hợp trên SQL Server thật và cập nhật OpenAPI.

### Tuần 1 (09/11 – 15/11): Nền móng, hạ tầng, khóa quyết định

**Tiến độ thực tế (kiểm tra trên GitHub ngày 11/11/2026; "Đã merge" chỉ nghĩa là có trên `develop`, chưa phải nghiệm thu):**

| ID | Trạng thái | Căn cứ / việc còn lại |
|---|---|---|
| HIE-1.1 | Đã xong | PR #7, #8 đã merge vào `develop` ngày 05/11. Còn dọn nhánh cũ đã merge (xem "Dọn nhánh" ở mục Git) |
| API Identity (hợp đồng + OpenAPI) | Chờ push/PR | Commit `e5cdc04` trên nhánh `docs/identity-api-contract`: `docs/api/openapi.json`, `docs/api/identity-contract.md`, khai báo kiểu response. Đã quá hạn 10/11 17:00 vì chưa push |
| HIE-1.2, HIE-1.3, HIE-1.4, HIE-1.5 | Chưa bắt đầu (theo GitHub) | Không có nhánh hay PR |
| PHU-1.1 (spike C03) | Đã merge, **chưa nghiệm thu** | PR #16. `docs/spike-c03-result.md`: 5/5 test đạt trên SQL Server 2022 Docker, chạy ở máy Phương. **Chưa chạy trên CI**: bộ lọc CI hiện chỉ chọn `IdentitySqlServer` (HAI-1.3 cần mở rộng) |
| PHU-1.2 (SalesDay) | Đã merge, **chưa nghiệm thu** | PR #16. Hợp đồng ở `docs/phuong-tuan-1.md`; mã lỗi có `code` (ví dụ `salesday.previous_not_closed`) |
| PHU-1.3 (Catalog) | Đã merge, **chưa nghiệm thu** | PR #16. Migration 9 bảng; chưa có API (để tuần 2) |
| BAO-1.1, BAO-1.3 | Phần lớn đã merge | Khung `web/` trên `develop` (commit 8fde101). Còn thiếu: `package-lock.json` (đang nằm trong PR #17), workflow CI cho `web/` |
| BAO-1.2 | Một phần | Có script `npm run gen:api` (đọc `${API_URL}/openapi/v1.json`). Chưa có file OpenAPI trong repo (nằm trong commit `e5cdc04`), chưa có CI báo lệch |
| BAO-1.4 | Chưa bắt đầu (theo GitHub) | Chưa có đăng nhập; `PermissionProvider` còn nhận danh sách quyền rỗng |
| DUY-1.1, DUY-1.2, DUY-1.3 | PR #17 đang mở | Realtime, âm thanh, thành phần vận hành, màn mở ngày (trên mock). PR này sửa cả `web/next.config.ts`, `web/package.json`, `web/tsconfig.json`, `web/eslint.config.mjs` là file cấu hình thuộc Bảo Anh: cần Bảo Anh duyệt |
| HAI-1.x | Chưa thấy trên GitHub | Chưa có Dockerfile, compose, workflow mới hay thay đổi bảo vệ nhánh. Có thể Hải đang làm cục bộ hoặc trên VPS; cần Hải cập nhật sổ tiến độ |

Các dòng bên dưới là kế hoạch gốc; xem bảng trên để biết việc nào đã làm.

| ID | Người | Việc | Xong khi | Phụ thuộc |
|---|---|---|---|---|
| HIE-1.1 | Hiếu | ~~Đưa PR `feature/identity-authentication` vào `develop`~~ **(đã xong 05/11, PR #8)**; còn dọn nhánh đã merge | PR merged, CI xanh | |
| HIE-1.2 | Hiếu | **ADR-01:** quy ước Contracts, nền tảng dùng chung (mã lỗi, idempotency, Outbox trừu tượng, helper khóa ngày), cập nhật `DependencyTests` | PR được Phương và Hải duyệt; tài liệu quy ước ở mục 5 được tuân theo | |
| HIE-1.3 | Hiếu | Refresh token: `POST /api/identity/refresh`, xoay vòng, phát hiện dùng lại thì thu hồi chuỗi, cookie như mục 5; cập nhật `identity-authentication.md` | Test thu hồi/hết hạn/dùng lại đạt trên SQL Server | HIE-1.2 |
| HIE-1.4 | Hiếu | Policy theo 26 mã quyền + `RequirePermission` | Test 403 theo từng vai trò cho một endpoint mẫu mỗi nhóm quyền | |
| HIE-1.5 | Hiếu | Seating: entity + migration `InitialSeating` (khu vực, bàn, phiên, chuyển bàn, quyền truy cập khách, chỉ mục duy nhất lọc) | Test: không mở hai phiên trên một bàn | C03, C05 đã duyệt |
| PHU-1.1 | Phương | **Spike C03:** hai DbContext chung `DbConnection/DbTransaction`, `sp_getapplock` Shared/Exclusive, hai người chốt cùng lúc, rollback giữa chừng | Ghi `docs/spike-c03-result.md` + test 14, 15 dạng khung chạy trên CI. **Quyết định đạt/không đạt vào thứ Sáu.** Nếu không đạt: dùng trạng thái chờ bền vững theo thiết kế (mục 5 của tài liệu thiết kế dữ liệu) và báo ngay | Hải: SQL Server trên CI |
| PHU-1.2 | Phương | SalesDay: migration `InitialSalesDay`, `GET /current`, `POST /open` (chỉ quản lý, tiền đầu ngày, chặn khi ngày trước chưa chốt) | Test chỉ một ngày hoạt động; staging mở được ngày | |
| PHU-1.3 | Phương | Catalog: migration `InitialCatalog` (nhóm, món, biến thể, nhóm tùy chọn, tùy chọn, gán, 3 bảng hết món theo ngày) | Migration chạy trên SQL Server thật; ràng buộc kiểm tra | |
| BAO-1.1 | Bảo Anh | Dựng `web/`: Next.js 16, TS strict, Tailwind, shadcn/ui, ESLint, Prettier, Vitest, MSW, khung Playwright, cấu trúc thư mục | `npm run build/lint/test` xanh; Hải nối CI | |
| BAO-1.2 | Bảo Anh | Sinh client từ OpenAPI (`npm run gen:api`); CI báo lỗi khi lệch | Đổi OpenAPI mà không sinh lại thì CI đỏ | HAI-1.3 |
| BAO-1.3 | Bảo Anh | Khung ứng dụng: layout + menu theo quyền, responsive, toast, error boundary, skeleton, trang 403/404, bộ form, bảng dữ liệu, hộp thoại xác nhận | Duy review và dùng được ở tuần 2 | |
| BAO-1.4 | Bảo Anh | Đăng nhập, làm mới token một lần cho nhiều request đồng thời, route guard theo quyền, đăng xuất, đổi mật khẩu | Đăng nhập thật trên staging; hết hạn 15 phút không đá người dùng ra khi còn refresh | HIE-1.3 (mock trước) |
| DUY-1.1 | Duy | Thành phần realtime: bọc SignalR (tự kết nối lại, tải lại trạng thái, khử trùng lặp bằng `eventId`) + bộ phát âm thanh (mở khóa sau thao tác người dùng, tắt tiếng lưu lại, **báo hiệu thị giác khi bị chặn âm thanh**) | Test đơn vị: không phát lại âm thanh khi kết nối lại; sự kiện trùng chỉ phát một lần | |
| DUY-1.2 | Duy | Thành phần vận hành: nhãn trạng thái, đồng hồ chờ, nhập tiền VND, bộ tăng giảm số lượng, khung in chứng từ | Có trang demo nội bộ | BAO-1.3 |
| DUY-1.3 | Duy | Màn mở ngày bán + banner trạng thái ngày | Mở ngày từ giao diện trên staging | PHU-1.2 |
| HAI-1.0 | Hải (nhóm thuê) | **Thuê và dựng VPS staging (thứ Hai–Ba):** nhóm chốt nhà cung cấp và người thanh toán ở họp kickoff, thuê ngay thứ Hai (Ubuntu LTS, đề xuất 4 vCPU/8 GB RAM, IP tĩnh, bật snapshot/backup của nhà cung cấp). Thứ Ba tăng cường bảo mật: user không root, SSH bằng khóa và tắt đăng nhập mật khẩu, tường lửa chỉ mở 22/80/443, fail2ban, tự cập nhật bảo mật, swap, cài Docker + Compose | VPS truy cập được bằng SSH khóa; quét cổng từ ngoài chỉ thấy 22/80/443; `docker run hello-world` chạy được; ghi nhà cung cấp, IP, người giữ khóa vào sổ tiến độ (mục 7) | Nhóm thanh toán thuê VPS (mục 11) |
| HAI-1.1 | Hải | DNS + HTTPS cho `quanlynh.id.vn` (đã đăng ký): bản ghi A `staging` trỏ về IP VPS (thứ Hai), Nginx + chứng chỉ Let's Encrypt trên VPS (thứ Ba). Nếu DNS đặt ở Cloudflare thì bật proxy sau khi HTTPS đã chạy ổn; nếu nhà đăng ký `.id.vn` không cho đổi nameserver, giữ DNS ở nhà đăng ký và bỏ proxy Cloudflare. Mọi cấu hình (FE, CI, smoke test) lấy địa chỉ gốc từ biến môi trường | `https://staging.quanlynh.id.vn/health/live` (hoặc trang tạm của Nginx nếu API chưa lên) trả 200 qua HTTPS với chứng chỉ hợp lệ | Người đăng ký tên miền cấp quyền sửa DNS cho Hải; HAI-1.0 |
| HAI-1.2 | Hải | Dockerfile API (nhiều tầng, không chạy root, healthcheck), `docker-compose.staging.yml` (SQL Server có volume, API, Nginx, về sau thêm `web`), `.env.example`; chỉ Nginx mở cổng ra ngoài, API và SQL chỉ nằm trong mạng nội bộ Docker (không publish 1433 hay cổng API); Nginx định tuyến `/api` và `/hubs` tới API, `/` tới `web` (cùng một origin) | Deploy tay lần đầu **chậm nhất thứ Tư**; chạy migration Identity + `--bootstrap-admin` | HAI-1.0, HAI-1.1 |
| HAI-1.2b | Hải (Bảo Anh hỗ trợ) | Dockerfile cho `web/` (Next.js, đầu ra standalone, không chạy root) và thêm dịch vụ `web` vào compose; deploy khung Next.js có trang đăng nhập lên VPS. Địa chỉ API đọc từ biến môi trường, không ghi cứng | `https://staging.quanlynh.id.vn/` hiển thị trang đăng nhập từ VPS; `/api/identity/login` qua cùng domain | HAI-1.2, BAO-1.1 |
| HAI-1.3 | Hải | CI: chạy mọi test `SqlServer` (không chỉ Identity), workflow frontend lọc theo đường dẫn, bảo vệ nhánh `develop`/`main` (CI + 1 review) | PR thử bị chặn khi CI đỏ | |
| HAI-1.4 | Hải | CD bước 1: sau CI xanh trên `develop` → build image API và web → đẩy GHCR. Deploy **thủ công** trong tuần 1 bằng `docker compose pull && up -d` + script migration idempotent, rồi kiểm tra `/health/ready`. Deploy tự động + quay lui làm ở HAI-2.5 (tuần 2) | CI đẩy được image lên GHCR sau mỗi merge vào `develop`; có tài liệu deploy thủ công từng bước | HAI-1.2 |
| HAI-1.5 | Hải + Hiếu | Header chuyển tiếp từ proxy tin cậy (Nginx; thêm Cloudflare nếu bật proxy), kiểm tra không vòng lặp HTTPS, giới hạn đăng nhập theo IP thật; không mở cổng API trực tiếp | Test sau proxy: 11 lần đăng nhập/phút từ hai IP khác nhau bị chặn đúng người | HAI-1.2 |
| HAI-1.6 | Hải | `docs/test-strategy.md`: tháp test, quy ước, dữ liệu mẫu, mức độ lỗi S1–S4, mẫu báo lỗi, phân người cho ma trận 17 test (mục 8) | Cả nhóm đọc và xác nhận ở họp thứ Hai tuần 2 | |

**Mốc tuần 1 (thứ Sáu):** đăng nhập từ web trên HTTPS thật (`https://staging.quanlynh.id.vn`, web và API cùng chạy trên VPS); có kết quả spike C03; `develop` được bảo vệ; ADR-01 đã merge; **cả nhóm ước lượng lại** khối lượng từng module (planning poker) và báo người duyệt nếu lệch > 20% so với mục 10.

#### Giao việc chi tiết tuần 1 theo ngày và theo người

Tuần 1 nhóm **thuê VPS ngay thứ Hai**, tên miền `quanlynh.id.vn` đã đăng ký, và **frontend với backend chạy song song rồi cùng lên mạng trong tuần**:

- Thứ Hai–Ba: backend công bố OpenAPI nháp (hạn thứ Ba 17:00); frontend dựng khung và làm trên mock (MSW); Hải thuê và bảo mật VPS, trỏ DNS, dựng HTTPS.
- Thứ Tư: API + SQL Server lên VPS (deploy tay lần đầu).
- Thứ Năm: web lên VPS cùng domain; frontend chuyển từ mock sang API thật; tích hợp đăng nhập thật qua HTTPS.
- Thứ Sáu: sửa lỗi tích hợp, demo cho người duyệt trên `https://staging.quanlynh.id.vn`.

Mỗi ô liệt kê việc cần **xong trong ngày đó**; việc nào chưa xong thì báo ở daily hôm sau, không dồn sang cuối tuần. Thứ Bảy và Chủ nhật không giao việc (đệm).

| Ngày | Hiếu (BE-A) | Phương (BE-B) | Bảo Anh (FE-A) | Duy (FE-B) | Hải (DevOps/QA) |
|---|---|---|---|---|---|
| **Thứ Hai 09/11** | Họp kickoff 09:00 (45'): xác nhận phân công, chỗ đặt `web/`, quy ước nhánh. ~~Xác nhận CI của PR Identity và merge vào `develop`~~ (HIE-1.1 đã xong từ 05/11). Mở PR nháp ADR-01: cấu trúc project Contracts, mã lỗi `code`, header `Idempotency-Key` (HIE-1.2) | Duyệt PR Identity của Hiếu. Dựng skeleton spike C03: hai DbContext nhỏ dùng chung `DbConnection/DbTransaction` trên SQL Server Docker cục bộ (PHU-1.1) | Tạo `web/`: Next.js, TS strict, Tailwind, shadcn/ui, ESLint, Prettier, Vitest, MSW; mở PR (BAO-1.1) | Khung SignalR hook + giao diện bộ phát âm thanh; test đơn vị với đồng hồ giả (DUY-1.1) | Cùng nhóm chốt nhà cung cấp VPS ở kickoff, **thuê VPS trước 12:00**, bật snapshot/backup, nhận IP và SSH vào lần đầu (HAI-1.0). Trỏ bản ghi A `staging` về IP VPS (HAI-1.1). Dựng bảng công việc theo mã việc. Bắt đầu mở rộng CI (HAI-1.3) |
| **Thứ Ba 10/11** | Hoàn tất bản nháp ADR-01. **Công bố OpenAPI nháp** cho login, me, logout, change-password, refresh (hạn 17:00). Bắt đầu refresh token: migration + miền (HIE-1.3) | Kịch bản spike: hai người chốt cùng lúc, rollback giữa chừng (PHU-1.1). SalesDay: migration `InitialSalesDay` và miền (PHU-1.2) | Khung ứng dụng: layout, menu theo quyền, toast, error boundary, trang 403/404 (BAO-1.3). Sinh client từ OpenAPI nháp của Hiếu ngay khi có (BAO-1.2) | Thành phần vận hành: nhãn trạng thái, đồng hồ chờ, nhập tiền VND (DUY-1.2) | Tăng cường bảo mật VPS: user không root, SSH khóa, tường lửa 22/80/443, fail2ban, cập nhật tự động, swap, Docker (HAI-1.0). Nginx + Let's Encrypt, `https://staging.quanlynh.id.vn` trả trang tạm (HAI-1.1). Bắt đầu Dockerfile API và `docker-compose.staging.yml` (SQL Server, API, Nginx), `.env.example` (HAI-1.2) |
| **Thứ Tư 11/11** | Họp rà soát contract 30'. Refresh token: API xoay vòng + phát hiện dùng lại + test trên SQL Server (HIE-1.3). Bắt đầu policy theo 26 mã quyền (HIE-1.4) | `GET /current`, `POST /open` của SalesDay + test (PHU-1.2). Migration `InitialCatalog` (PHU-1.3) | Đăng nhập, làm mới token một lần cho nhiều request, route guard, trên mock (BAO-1.4) | Hoàn tất thành phần vận hành và trang demo nội bộ (DUY-1.2) | **Deploy tay lần đầu, chậm nhất hôm nay:** API + SQL Server lên VPS, chạy migration Identity + `--bootstrap-admin`; `https://staging.quanlynh.id.vn/health/live` trả 200 (HAI-1.2). Ghi các bước deploy thủ công (HAI-1.4). Viết Dockerfile `web/` và dịch vụ `web` trong compose, sẵn sàng deploy sáng mai (HAI-1.2b) |
| **Thứ Năm 12/11** | Merge refresh token vào `develop`; Hải deploy lên staging. Migration `InitialSeating` + test không mở hai phiên một bàn (HIE-1.5). Cùng Hải làm forwarded headers (HAI-1.5) | Đưa SalesDay lên staging (qua `develop` + Hải deploy). Viết nháp `docs/spike-c03-result.md` | **Chuyển từ mock sang API thật:** đăng nhập thật trên staging, kiểm tra làm mới token khi quá 15 phút (BAO-1.4) | Màn mở ngày bán + banner trạng thái dùng API thật (DUY-1.3) | **Sáng: deploy `web/` lên VPS** cùng Bảo Anh, `https://staging.quanlynh.id.vn/` hiện trang đăng nhập (HAI-1.2b). Forwarded headers: test sau proxy, 11 lần đăng nhập/phút từ hai IP khác nhau bị chặn đúng người (HAI-1.5). Hoàn tất bảo vệ `develop` và `main` (HAI-1.3). CI đẩy image API và web lên GHCR (HAI-1.4). Bắt đầu `docs/test-strategy.md` (HAI-1.6) |
| **Thứ Sáu 13/11** | Merge ADR-01 (Phương và Hải duyệt). Hoàn tất test Seating | **Kết luận spike C03 đạt/không đạt**, trình bày trước nhóm; nếu không đạt báo người duyệt trong 24 giờ | Sửa lỗi tích hợp đăng nhập; sẵn sàng demo | Sửa lỗi tích hợp màn mở ngày; sẵn sàng demo | Hoàn tất `test-strategy.md`. Quét cổng từ ngoài (chỉ 22/80/443), kiểm tra chứng chỉ và checklist mốc #0. Chuẩn bị demo staging. Bắt đầu thiết kế CD tự động + quay lui (làm tiếp tuần 2) |
| | **Cả nhóm 15:00:** demo cho người duyệt (45'): đăng nhập trên HTTPS, mở ngày bán. Sau đó **ước lượng lại** khối lượng từng module (planning poker) và báo người duyệt nếu lệch > 20% | | | | |

**Bàn giao giữa người với người trong tuần 1 (ai chờ ai):**

| Người nhận | Cần từ | Cái gì | Hạn | Nếu trễ |
|---|---|---|---|---|
| Bảo Anh | Hiếu | OpenAPI nháp (login, me, logout, change-password, refresh) | Thứ Ba 17:00 | Bảo Anh tự viết kiểu dữ liệu tạm theo `identity-authentication.md`, sửa lại khi có OpenAPI |
| Bảo Anh | Hiếu | API refresh chạy trên staging | Thứ Năm | Tiếp tục dùng mock; đăng nhập thật chưa dùng được lâu vì JWT chỉ 15 phút |
| Duy | Phương | `POST /open` của SalesDay trên staging | Thứ Năm | Duy làm màn mở ngày trên mock, nối API thật thứ Sáu hoặc đầu tuần 2 |
| Phương, Hiếu | Hải | SQL Server dùng được cho test tích hợp (CI hoặc Docker cục bộ) | Thứ Hai | Dùng SQL Server Docker cục bộ như `Restaurant.IntegrationTests` đang làm |
| Hải | Nhóm / người duyệt | VPS đã thuê (IP, quyền SSH) và quyền sửa DNS `quanlynh.id.vn` | Thứ Hai 12:00 | Nếu đến trưa thứ Tư vẫn chưa có VPS: báo người duyệt ngay; Hải chạy compose trên máy của mình (không công khai) để Bảo Anh và Duy tích hợp, mốc "online" lùi tương ứng |
| Bảo Anh | Hải | Khung `web/` chạy được trên VPS cùng domain | Thứ Năm sáng | Bảo Anh tiếp tục chạy `web/` cục bộ với API cục bộ; không chặn việc khác |
| Hải | Hiếu | Xác nhận API hoạt động sau proxy, `Program.cs` thêm forwarded headers | Thứ Tư–Năm | Giới hạn đăng nhập sẽ tính theo IP của proxy cho đến khi sửa |
| Cả nhóm | Hiếu | ADR-01 đã merge trước khi bắt đầu Contracts/Outbox ở tuần 2 | Thứ Sáu | Họp contract tuần 2 (HIE-2.3) lùi theo |

### Tuần 2 (16/11 – 22/11): Dữ liệu nền — bàn, thực đơn, ngày bán

| ID | Người | Việc | Xong khi | Phụ thuộc |
|---|---|---|---|---|
| HIE-2.1 | Hiếu | Seating API: khu vực, bàn, bảng trạng thái bàn, mở/đóng phiên, chuyển bàn | Test 3 (mở/chuyển đồng thời không chiếm một bàn hai lần) | HIE-1.5 |
| HIE-2.2 | Hiếu | Sales: migration `InitialSales`; mô hình miền (đơn, đợt, phần món, ảnh chụp tên/giá/tùy chọn); kiểm thử máy trạng thái | Unit test mọi chuyển trạng thái hợp lệ và không hợp lệ | |
| HIE-2.3 | Hiếu + Phương | **Họp contract (thứ Ba):** `Sales→Catalog` (kiểm tra và tính giá), `Sales→Seating` (phiên), `Sales→SalesDay` (cổng ngày), `Sales→Billing` (ghi nghĩa vụ khi xác nhận đợt), `Billing→Sales` (đơn đã xử lý xong chưa) | Interface + DTO merge vào các project Contracts; sơ đồ phụ thuộc cập nhật vào `backend-foundation.md` | HIE-1.2 |
| PHU-2.1 | Phương | Catalog API: nhóm, món, biến thể, nhóm tùy chọn, gán tùy chọn (min/max), cờ hết món 4 cấp theo ngày, `GET /menu` (nhân viên), `GET /public/menu` (khách, ẩn món hết) | Test 2 (biến thể/tùy chọn sai món bị từ chối); đổi cờ hết món có ghi người và giờ | PHU-1.3 |
| PHU-2.2 | Phương | Helper khóa ngày (Shared/Exclusive) vào nền tảng dùng chung từ kết quả spike; `ISalesDayGate` | Test 14 đạt ở mức thư viện | PHU-1.1 |
| PHU-2.3 | Phương | Billing: migration `InitialBilling` (hóa đơn, dòng, điều chỉnh, miễn thu, thanh toán, hoàn tiền, chứng từ) | Migration + ràng buộc: một hóa đơn/đơn, một dòng/phần món | C01 |
| BAO-2.1 | Bảo Anh | Quản lý khu vực và bàn; bảng trạng thái bàn (màn hình chính của nhân viên) | Thao tác trên staging; trống/đang dùng đúng | HIE-2.1 |
| BAO-2.2 | Bảo Anh | Quản lý thực đơn: nhóm, món, biến thể, nhóm tùy chọn, gán, cờ hết món (chỗ ảnh để trống) | Tạo trọn một món có biến thể và topping từ giao diện | PHU-2.1 |
| DUY-2.1 | Duy | Bảng bếp/bar (Kanban Mới → Đang làm → Sẵn sàng) trên dữ liệu mock, tích hợp thành phần realtime và âm thanh | Demo có âm thanh với sự kiện giả; hiển thị cảnh báo khi trình duyệt chặn âm thanh | DUY-1.1 |
| DUY-2.2 | Duy | Màn đóng/chốt ngày: khung và các trạng thái (ĐANG BÁN / CHỜ CHỐT / ĐÃ CHỐT) | Có mock đủ trạng thái | |
| DUY-2.3 | Duy | **Hỗ trợ Hải:** viết kịch bản smoke test Playwright (đăng nhập → mở ngày → đọc thực đơn), chạy bằng một lệnh, địa chỉ gốc lấy từ biến môi trường | Hải chạy được sau mỗi lần deploy; Duy không phải sửa hạ tầng | BAO-1.1 |
| HAI-2.1 | Hải | Dữ liệu mẫu cho staging: lệnh `--seed-demo` (tài khoản mỗi vai trò, khu vực, bàn, thực đơn, nguyên liệu) + script reset | Reset và nạp lại dưới 5 phút | Phương, Hiếu cung cấp dữ liệu |
| HAI-2.2 | Hải | Cloudflare R2: bucket ảnh công khai + `img.quanlynh.id.vn` + CORS (tên miền riêng cho R2 cần DNS nằm ở Cloudflare; nếu không, dùng địa chỉ R2 công khai hoặc phục vụ ảnh qua Nginx); bucket sao lưu **private**; token quyền tối thiểu | Tải ảnh thử lên và xem qua địa chỉ ảnh; bucket sao lưu truy cập công khai bị từ chối | HAI-1.1 |
| HAI-2.3 | Hải | Chạy smoke test sau mỗi deploy (đăng nhập, mở ngày, đọc thực đơn; kịch bản do Duy viết ở DUY-2.3); uptime check `/health/ready`; xoay vòng log container | Smoke test chạy bằng một lệnh và báo đạt/không đạt; khi có CD (HAI-2.5), deploy hỏng thì tự quay lui và báo | HAI-1.4, DUY-2.3 |
| HAI-2.4 | Hải | Bộ dựng dữ liệu test và hạ tầng test tích hợp dùng chung cho các module | Hiếu, Phương dùng được trong test của mình | HAI-1.6 |
| HAI-2.5 | Hải | **CD tự động + quay lui (chậm nhất thứ Ba 17/11):** sau CI xanh trên `develop` → GHCR → VPS kéo image mới (khóa SSH deploy chỉ có quyền chạy script deploy) → chạy script migration idempotent → kiểm tra `/health/ready` → smoke test; hỏng thì tự quay lui về image trước và báo vào kênh nhóm | Một lần deploy tự động thành công và một lần cố ý hỏng được quay lui đúng, ghi trong nhật ký | HAI-1.4 |
| HAI-2.6 | Hải | Sao lưu SQL hằng ngày lên bucket R2 private và **thử khôi phục một lần** vào DB sạch (bản hoàn chỉnh có mã hóa, xoay vòng, cảnh báo ở HAI-4.2) | Có tệp sao lưu hằng ngày kèm nhật ký; khôi phục thử thành công | HAI-2.2 |

**Mốc tuần 2 (thứ Sáu 22/11, kiểm tra #1):** trên staging, quản lý tạo bàn và thực đơn, mở ngày, nhân viên mở phiên bàn; FE dùng API thật; **CD tự động + quay lui và sao lưu hằng ngày đã chạy trên VPS (HAI-2.5, HAI-2.6)**. **Nếu bất kỳ mục Tier 1 nào trượt > 2 ngày → kích hoạt bước 1 của bậc cắt giảm (mục 9).** Nếu chỉ hạ tầng (CD, sao lưu) trượt, đó là vấn đề hạ tầng chứ không phải phạm vi: báo người duyệt ngay trong ngày.

### Tuần 3 (23/11 – 29/11): Gọi món và thanh toán nền

| ID | Người | Việc | Xong khi | Phụ thuộc |
|---|---|---|---|---|
| HIE-3.1 | Hiếu | Sales API: tạo đơn (tại bàn bắt buộc có phiên; mang về có tên/điện thoại/giờ), thêm đợt, xác nhận/từ chối/hủy đợt, hủy phần món (nhân viên trước chế biến; còn lại cần quản lý + lý do), gọi thêm đến khi đóng đơn, kiểm tra giá đổi và trả giá mới để xác nhận | Test 5: xác nhận lặp chỉ tạo một khoản nghĩa vụ và một phiếu đầu | HIE-2.2, PHU-3.1 |
| HIE-3.2 | Hiếu | Xác nhận đợt gọi món **cùng transaction** với ghi nghĩa vụ (Billing) theo mẫu của spike; áp dụng khóa ngày Shared cho mọi lệnh ghi của Sales | Test 14 mức module: lệnh ghi chờ rồi bị từ chối khi ngày đang chốt | PHU-2.2 |
| HIE-3.3 | Hiếu | Đóng đơn: điều kiện món xử lý xong + nghĩa vụ (qua Billing); không tự giải phóng bàn | Test điều kiện đóng | PHU-3.1 |
| HIE-3.4 | Hiếu | Khởi tạo Fulfillment: migration `InitialFulfillment`, consumer Outbox nhận "đợt đã xác nhận" và tạo phiếu chế biến, có hộp thư đến chống xử lý lặp | Test: sự kiện giao hai lần chỉ tạo một phiếu | HIE-3.1 |
| PHU-3.1 | Phương | Billing: tạo hóa đơn khi có đợt xác nhận đầu tiên; dòng theo phần món; tính nghĩa vụ/còn phải thu theo C01; `IInvoiceCharging` cho Sales dùng **chậm nhất thứ Tư** | Hiếu tích hợp được vào thứ Năm | HIE-2.3 |
| PHU-3.2 | Phương | Thanh toán: tiền mặt (khách đưa/tiền thừa), chuyển khoản (chờ xác nhận → thành công), nhiều lần, không thu dư dưới khóa; idempotency | Test 8: hai thu ngân không thu dư | PHU-3.1 |
| PHU-3.3 | Phương | Miễn thu (quản lý, có lý do, chỉ cho phần chưa thu); dữ liệu chứng từ/phiếu thu | Miễn thu không làm thay đổi tiền đã thu | PHU-3.2 |
| BAO-3.1 | Bảo Anh | Màn gọi món của nhân viên: chọn bàn → duyệt thực đơn theo nhóm + tìm kiếm → hộp thoại biến thể/tùy chọn → giỏ → gửi đợt | Gọi một order có topping từ giao diện thật | HIE-3.1 |
| BAO-3.2 | Bảo Anh | Hàng đợi đợt chờ xác nhận, chi tiết order theo đợt, đơn mang về | Xác nhận một đợt và thấy trạng thái đổi | HIE-3.1 |
| DUY-3.1 | Duy | Màn hóa đơn và thu tiền: danh sách khoản, hộp thoại thu tiền mặt (tính tiền thừa), chuyển khoản (mã tham chiếu, xác nhận), nhiều lần thu | Thu đủ → hóa đơn "đã thu" | PHU-3.2 |
| DUY-3.2 | Duy | Hộp thoại miễn thu; trang xem/in chứng từ (CSS in khổ hẹp) | In thử ra PDF đúng nội dung | PHU-3.3 |
| HAI-3.1 | Hải | Module Audit: migration `InitialAudit`, `IAuditLog` trong `Audit.Contracts`, API tra cứu (chỉ quản lý, lọc theo người/đối tượng/thời gian) | Hiếu và Phương gọi được từ các thao tác nhạy cảm | HIE-1.2 |
| HAI-3.2 | Hải | Playwright: kịch bản đăng nhập → mở ngày → gọi món → xác nhận → thu tiền, chạy sau mỗi deploy | Chạy tự động trên staging | |
| HAI-3.3 | Hải | Quét bảo mật trong CI: lỗ hổng gói NuGet/npm, phát hiện secret; danh sách kiểm tra header/CORS/giới hạn tốc độ | Pipeline đỏ khi có lỗ hổng mức cao | |

**Mốc tuần 3 (thứ Sáu 29/11, kiểm tra #2):** trên staging chạy được **gọi món → xác nhận → hóa đơn → thu tiền → chứng từ** qua giao diện. Chưa đạt thì kích hoạt bước 2 của bậc cắt giảm.

### Tuần 4 (30/11 – 06/12): Bếp/bar, giao món, thời gian thực

| ID | Người | Việc | Xong khi | Phụ thuộc |
|---|---|---|---|---|
| HIE-4.1 | Hiếu | Fulfillment: máy trạng thái phiếu (Mới → Đang làm → Sẵn sàng → Đã giao; Hủy), thao tác từng phần món, bếp/bar chỉ sửa món của trạm mình, chống lặp và xung đột | Test 7: một phần không giao hai lần hoặc sau khi hủy | HIE-3.4 |
| HIE-4.2 | Hiếu | SignalR hub `/hubs/operations` (nhóm theo trạm, phục vụ, đơn), Outbox dispatcher (thử lại, khóa thuê), API lấy ảnh chụp trạng thái khi kết nối lại | Test: sự kiện chỉ gửi sau commit; mất kết nối rồi nối lại nhận đúng trạng thái | HIE-4.1 |
| HIE-4.3 | Hiếu | Hết món theo thời gian thực: đổi cờ → sự kiện `AvailabilityChanged`; Sales chặn thêm mới mục đang hết, món đã xác nhận mà bếp báo hết đi vào luồng sự cố | Test chặn đúng | PHU-2.1 |
| PHU-4.1 | Phương | Inventory: đơn vị, nguyên liệu (chế độ theo dõi tồn / tính khi mua), quy đổi, nhà cung cấp cơ bản, phiếu nhập (nháp/hiệu lực/hủy, cờ trả bằng tiền két), snapshot khi nhập | Test 11 (sửa quy đổi không đổi dữ liệu lịch sử), test 12 (C04) | C02, C04 |
| PHU-4.2 | Phương | Khung chốt ngày: danh sách điều kiện chặn (còn đơn mở/phiên/đợt chưa xác nhận/số đếm chưa duyệt…), chuyển CHỜ CHỐT, hủy chốt | Test: ghi mới bị từ chối khi CHỜ CHỐT; hủy chốt trả về ĐANG BÁN | PHU-2.2 |
| PHU-4.3 | Phương | Billing: gọi thêm sau khi đã thu đủ đưa đơn về thu một phần; điều kiện đóng đơn đầy đủ | Test kịch bản gọi thêm sau thanh toán | HIE-3.3 |
| BAO-4.1 | Bảo Anh | Hoàn thiện màn nhân viên: sửa/hủy phần món theo quyền, bảng trạng thái bàn cập nhật realtime, cờ hết món tức thời, chuyển bàn | Hai trình duyệt thấy thay đổi trong ≤ 2 giây | HIE-4.2 |
| BAO-4.2 | Bảo Anh | Ảnh món: tải lên R2 (URL ký sẵn), tối ưu hiển thị | Thêm ảnh một món và thấy trên menu | HAI-2.2, PHU-2.1 |
| DUY-4.1 | Duy | Màn bếp/bar thật: nhận phiếu mới (kèm âm thanh), bắt đầu/hoàn thành từng phần, thời gian chờ, nối lại giữ đúng trạng thái | Kịch bản thủ công + test: không phát âm thanh lịch sử | HIE-4.1/4.2 |
| DUY-4.2 | Duy | Màn phục vụ: món sẵn sàng theo bàn/đơn (kèm âm thanh), xác nhận giao từng phần, mã nhận cho đơn mang về | Giao đủ → phần món "đã giao" | HIE-4.1 |
| HAI-4.1 | Hải | Cấu hình Nginx/Cloudflare cho WebSocket (thời gian chờ, kích thước), kiểm tra tải 50 kết nối SignalR đồng thời | Không rớt kết nối trong 30 phút | HIE-4.2 |
| HAI-4.2 | Hải | Hoàn thiện sao lưu từ HAI-2.6: mã hóa, giữ theo chính sách xoay vòng; giám sát dung lượng đĩa/RAM của VPS và cảnh báo | Bản sao lưu hằng ngày có mã hóa kèm nhật ký; cảnh báo đĩa > 80% hoạt động | HAI-2.6 |
| HAI-4.3 | Hải | Playwright: bếp → giao; test kết nối lại/khử trùng lặp; kiểm thử thủ công âm thanh (Chrome, Safari iOS, Android) | Bảng kết quả theo trình duyệt | DUY-4.1 |

**Mốc tuần 4 (thứ Sáu 06/12, kiểm tra #3):** chạy được **order → bếp/bar (có âm thanh) → giao → thu tiền → đóng order** trên staging, hai thiết bị cùng lúc.

### Tuần 5 (07/12 – 13/12): QR, đếm tồn, chốt ngày — **backend Tier 1 hoàn tất**

| ID | Người | Việc | Xong khi | Phụ thuộc |
|---|---|---|---|---|
| HIE-5.1 | Hiếu | QR backend: nhận mã truy cập khách theo phiên (giới hạn tốc độ, hết hạn, thu hồi khi đóng phiên), thực đơn công khai, gửi đợt của khách (trạng thái chờ xác nhận), theo dõi đơn của chính mình | Test 4: mã định vị bàn một mình không đặt được; mã hết hạn/thu hồi/phiên khác bị từ chối | HIE-2.1, PHU-2.1 |
| HIE-5.2 | Hiếu | API quản lý tài khoản (danh sách, tạo, vai trò, kích hoạt, đặt lại mật khẩu; quản lý chỉ quản lý cấp dưới) | Test: không tự nâng quyền; quản lý không sửa Admin/Quản lý | HIE-1.4 |
| HIE-5.3 | Hiếu | Gắn Audit vào mọi thao tác nhạy cảm của Identity/Seating/Sales/Fulfillment; xác nhận mọi lệnh ghi theo ngày đã dùng khóa Shared | Danh sách thao tác nhạy cảm kèm test ghi audit | HAI-3.1 |
| PHU-5.1 | Phương | Kiểm kê tồn: nhập nháp (nhân viên/bếp/bar), duyệt (quản lý); công cụ tính giá vốn theo C02 (bình quân, giá trị tồn cuối, lượng dùng, làm tròn một lần) | Test 13 (số lượng 0 mà có giá trị chặn chốt), 16 (âm vẫn biểu diễn được, không trộn đơn vị) | PHU-4.1 |
| PHU-5.2 | Phương | Chốt ngày hoàn chỉnh: đối soát tiền mặt (đầu ngày + thu − hoàn − chi mua bằng tiền két), snapshot tổng hợp ngày/phương thức theo C06-A, mở ngày kế tiếp lấy tồn đầu từ ngày chốt, khóa chốt idempotent, chốt bù | Test 14, 15 trên SQL Server: hai người chốt chỉ ra một snapshot; rollback không để lại dữ liệu nửa chừng | PHU-4.2, PHU-5.1 |
| BAO-5.1 | Bảo Anh | **Trang QR cho khách** (công khai, mobile-first, 360 px trở lên): vào qua mã bàn → xem menu → giỏ → gửi → theo dõi trạng thái; xử lý phiên hết hạn, bị giới hạn tốc độ, món hết | Lighthouse mobile ≥ 80 (hiệu năng), ≥ 90 (truy cập); chạy thật bằng điện thoại | HIE-5.1 |
| BAO-5.2 | Bảo Anh | Trải nghiệm theo quyền (ẩn/khóa chức năng), rà soát truy cập bàn phím/tương phản trên màn nhân viên | Không có màn trắng khi 401/403/500; kiểm tra thủ công danh sách | |
| DUY-5.1 | Duy | Màn kho: nguyên liệu/đơn vị/quy đổi, nhà cung cấp, phiếu nhập, nhập số đếm | Nhập trọn một ngày dữ liệu kho từ giao diện | PHU-4.1, PHU-5.1 |
| DUY-5.2 | Duy | Trình hướng dẫn chốt ngày: danh sách điều kiện chặn và lý do, duyệt số đếm, đối soát tiền mặt, xác nhận/hủy chốt, lịch sử ngày | Chốt được một ngày mẫu và xem lại snapshot | PHU-5.2 |
| HAI-5.1 | Hải | Chạy toàn bộ ma trận 17 test trên SQL Server thật và công bố bảng trạng thái; bổ sung test đồng thời còn thiếu | Bảng trạng thái cập nhật hằng ngày; test chưa có chủ bị gán người | |
| HAI-5.2 | Hải | Quét ZAP baseline trên staging; kiểm tra header bảo mật, giới hạn tốc độ, CORS | Không còn lỗi mức cao | |
| HAI-5.3 | Hải | **Diễn tập khôi phục lần 1** từ bản sao lưu R2; chốt kịch bản UAT và đề cương hướng dẫn sử dụng theo vai trò | Khôi phục thành công vào DB sạch và API chạy được | HAI-4.2 |

**Mốc tuần 5 (thứ Sáu 13/12, kiểm tra #4 — quan trọng nhất):** **đóng băng tính năng Tier 1.** Luồng đầy đủ chạy được, gồm QR, âm thanh và chốt ngày. Từ đây chỉ sửa lỗi Tier 1 và làm Tier 2.

### Tuần 6 (14/12 – 20/12): Tier 2, tích hợp, chất lượng — **đóng băng toàn bộ vào thứ Sáu**

| ID | Người | Việc | Xong khi | Phụ thuộc |
|---|---|---|---|---|
| HIE-6.1 | Hiếu | Sự cố và làm lại: báo sự cố, quản lý duyệt (làm lại/hủy), phiếu làm lại gắn món gốc, không phát sinh doanh thu | Test 6; không phục vụ lần làm cũ chưa làm lại | HIE-4.1 |
| HIE-6.2 | Hiếu | Sửa lỗi từ UAT/E2E, hiệu năng truy vấn nóng (chỉ mục theo mục 6 tài liệu thiết kế dữ liệu), tài liệu API | Không còn lỗi S1/S2 mở thuộc module của Hiếu | |
| PHU-6.1 | Phương | Hoàn tiền theo số tiền (đề nghị → duyệt → chi), giữ hạn mức, `CHO_DOI_SOAT` khi chưa rõ kết quả; điều chỉnh nghĩa vụ riêng (C01) | Test 9 (đồng thời không vượt số đã thu), 10 (hoàn đơn ngày cũ ghi vào ngày hiện tại, snapshot cũ không đổi) | PHU-3.3 |
| PHU-6.2 | Phương | Điều chỉnh tồn có dấu và giá trị (quản lý, có lý do, audit); sửa phiếu nhập trước chốt | Điều chỉnh được đưa vào giá vốn | PHU-5.1 |
| PHU-6.3 | Phương | Reporting cơ bản: doanh thu thuần (C06-A) và dòng tiền theo ngày/khoảng ngày, giá vốn, lợi nhuận gộp, món bán, tồn; chỉ quản lý | Số liệu khớp snapshot chốt ngày; test quyền | PHU-5.2 |
| BAO-6.1 | Bảo Anh | Màn báo cáo (hai cột tách biệt "doanh thu" và "dòng tiền") | Quản lý xem được, nhân viên bị chặn | PHU-6.3 |
| BAO-6.2 | Bảo Anh | Màn quản trị tài khoản (danh sách, tạo, vai trò, khóa, đặt lại mật khẩu) | Tạo nhân viên mới và đăng nhập được | HIE-5.2 |
| BAO-6.3 | Bảo Anh | Hoàn thiện: trạng thái rỗng/lỗi/đang tải, thông báo lỗi theo `code`, hiệu năng bundle | Danh sách kiểm tra giao diện đạt | |
| DUY-6.1 | Duy | Màn sự cố (báo, duyệt) và hoàn tiền (đề nghị, duyệt, chi, chờ đối soát) | Chạy trọn luồng hoàn tiền trên staging | HIE-6.1, PHU-6.1 |
| DUY-6.2 | Duy | Điều chỉnh tồn, trạng thái chốt ngày ở trường hợp biên, chứng từ in hoàn thiện | Danh sách kiểm tra đạt | PHU-6.2 |
| HAI-6.1 | Hải | **UAT vòng 1** (thứ Tư–Năm) với kịch bản mục 7; phân loại lỗi S1–S4, theo dõi bảng lỗi | Báo cáo UAT 1 gửi người duyệt | |
| HAI-6.2 | Hải | Tách môi trường production (`quanlynh.id.vn`) trên **VPS riêng** (hoặc cùng VPS nếu đủ RAM, mục 11): compose riêng, xoay vòng secret, tài khoản admin riêng, bảo vệ nhánh `main` | Production dựng được nhưng chưa mở cho người dùng thật | |
| HAI-6.3 | Hải | Ngân sách hiệu năng (Lighthouse CI, k6 cho luồng nóng), cảnh báo giám sát, diễn tập khôi phục lần 2 | Đạt ngưỡng mục 8; tài liệu runbook | |

**Mốc tuần 6 (thứ Sáu 20/12):** **đóng băng toàn bộ tính năng.** Từ thứ Bảy chỉ sửa lỗi.

### Tuần 7 (21/12 – 27/12): UAT, sửa lỗi, phát hành — đồng thời là **bộ đệm của cả dự án**

| ID | Người | Việc | Xong khi |
|---|---|---|---|
| ALL-7.1 | Cả nhóm | Sửa lỗi theo mức độ (S1 trong ngày, S2 trong 2 ngày); ưu tiên lỗi chặn luồng chính | Không còn S1/S2 mở khi phát hành |
| HAI-7.1 | Hải | **UAT vòng 2** cùng người duyệt (thứ Hai–Ba) và hồi quy sau sửa lỗi (thứ Năm) | Báo cáo UAT 2: mọi kịch bản mục 7 đạt |
| HAI-7.2 | Hải | Phát hành: PR `develop` → `main` (merge commit), gắn tag `v1.0.0`, deploy production, smoke test, diễn tập quay lui | Production chạy, quay lui đã thử |
| ALL-7.3 | Cả nhóm | Tài liệu bàn giao: hướng dẫn sử dụng theo vai trò (1 trang mỗi vai), runbook vận hành, sơ đồ kiến trúc, danh sách tồn đọng | Người duyệt nhận đủ gói |
| HAI-7.3 | Hải | Buổi tập huấn người dùng, ghi nhận phản hồi, tạo danh sách việc sau phát hành | Tập huấn xong trước 27/12 |

Nếu không trượt, tuần 7 có dư; nếu trượt, đây là nơi hấp thụ. **Không dùng tuần 7 cho tính năng mới.**

## 7. Kịch bản UAT (người duyệt xem trước ở tuần 4)

1. Quản lý mở ngày với tiền đầu ngày; mở lần hai khi ngày chưa chốt bị chặn.
2. Nhân viên mở phiên bàn, gọi 3 món (một món có size và topping), gửi, xác nhận; bếp và bar nhận đúng trạm có âm thanh.
3. Khách quét QR, gửi đợt gọi thêm; nhân viên xác nhận; khách thấy trạng thái cập nhật; mã hết hạn sau đóng phiên.
4. Bếp hoàn thành một phần; nhân viên phục vụ nghe âm thanh, giao từng phần; mất mạng rồi nối lại không phát lại âm thanh cũ.
5. Thu nhiều lần (tiền mặt + chuyển khoản), có tiền thừa; không thu dư khi hai thu ngân cùng thao tác.
6. Gọi thêm sau khi đã thu đủ: đơn quay về còn phải thu.
7. Quản lý miễn thu phần không thu được; đơn đóng; chứng từ in đúng.
8. Món lỗi: báo, quản lý duyệt, làm lại không tăng doanh thu.
9. Hoàn tiền một phần theo số tiền có duyệt và hạn mức; thử vượt hạn mức bị chặn.
10. Chuyển bàn khi phiên mở; mã QR của khách vẫn dùng được.
11. Nhập phiếu nhập, đếm tồn cuối bất thường (số đếm lớn hơn tồn khả dụng, giá trị 0), điều chỉnh có lý do.
12. Chốt ngày: bị chặn khi còn đơn mở; hai người cùng chốt; hủy chốt; chốt thành công; mở ngày sau lấy đúng tồn đầu; báo cáo khớp snapshot.
13. Phân quyền: nhân viên không xem được báo cáo; bếp không sửa món của bar; Admin không tự có quyền vận hành.
14. Phiên: mật khẩu đổi thì các phiên khác bị thu hồi; đăng nhập quá 10 lần/phút bị giới hạn.

## 8. Chuẩn "dùng được như web hiện nay" và ma trận test

**Tiêu chí chấp nhận phát hành** (Hải kiểm tra, người duyệt xác nhận):

| Nhóm | Tiêu chí |
|---|---|
| Truy cập | HTTPS bắt buộc trên `quanlynh.id.vn`, HSTS bật, không chứng chỉ lỗi |
| Giao diện | Trang QR dùng tốt từ 360 px; màn nhân viên từ 768 px; màn bếp/bar từ 1024 px; có trạng thái đang tải, rỗng, lỗi trên mọi màn |
| Hiệu năng | Trang QR: Lighthouse mobile ≥ 80; API luồng nóng p95 < 500 ms ở tải thử (k6) |
| Thời gian thực | Sự kiện đến giao diện ≤ 2 giây (p95) trong staging; nối lại không mất/trùng sự kiện |
| Truy cập (a11y) | Lighthouse a11y ≥ 90 ở trang QR; thao tác chính dùng được bằng bàn phím trên màn nhân viên |
| Phiên | Không bị đăng xuất giữa ca khi còn refresh hợp lệ; đổi mật khẩu thu hồi phiên |
| Bảo mật | Không secret trong git; quét lỗ hổng NuGet/npm và ZAP không còn mức cao; cổng SQL/API không mở ra ngoài |
| Dữ liệu | Sao lưu hằng ngày sang R2 private; đã khôi phục thử thành công ít nhất 2 lần |
| Tài liệu | Hướng dẫn theo vai trò, runbook, danh sách tồn đọng |

**Ma trận 17 test** (nguồn: mục 7 tài liệu thiết kế dữ liệu). Chủ = người viết; Hải chạy trên CI và giữ bảng trạng thái.

| Test | Nội dung rút gọn | Chủ | Tuần |
|---|---|---|---|
| 1 | Chặn trùng vai trò/tên đăng nhập, hóa đơn–đơn, dòng–phần món | Hiếu (Identity), Phương (Billing) | 1, 3 |
| 2 | Biến thể/tùy chọn không thuộc món bị từ chối | Phương, Hiếu | 2, 3 |
| 3 | Mở/chuyển bàn đồng thời không chiếm hai lần | Hiếu | 2 |
| 4 | Mã QR một mình không đặt được; mã hết hạn/thu hồi/khác phiên bị từ chối | Hiếu | 5 |
| 5 | Xác nhận đợt lặp: một khoản nghĩa vụ, một phiếu đầu | Hiếu + Phương | 3 |
| 6 | Làm lại cần sự cố đã duyệt, không phát sinh doanh thu | Hiếu | 6 |
| 7 | Một phần/lần làm không giao hai lần hoặc sau khi hủy | Hiếu | 4 |
| 8 | Hai thu ngân không thu dư | Phương | 3 |
| 9 | Hoàn tiền đồng thời không vượt đã thu; chưa rõ kết quả giữ hạn mức | Phương | 6 |
| 10 | Hoàn đơn ngày cũ ghi vào ngày hiện tại; snapshot cũ không đổi | Phương | 6 |
| 11 | Sửa quy đổi không đổi số lượng/giá trị lịch sử của phiếu nhập | Phương | 4 |
| 12 | Nguyên liệu lưu trữ còn tồn vẫn đếm được; đổi chế độ/đơn vị bị chặn | Phương | 4 |
| 13 | Số lượng 0 mà giá trị khác 0 chặn chốt; điều chỉnh có dấu vào giá vốn | Phương | 5 |
| 14 | Hai người chốt chỉ ra một snapshot; ghi muộn chờ rồi bị từ chối | Phương (spike tuần 1, hoàn chỉnh tuần 5) | 1, 5 |
| 15 | Rollback khi chốt không để lại dữ liệu nửa chừng | Phương | 1, 5 |
| 16 | Lãi/lỗ âm biểu diễn được; không cộng khác đơn vị | Phương | 5 |
| 17 | Thu hồi vai trò có hiệu lực với token đang dùng | Hiếu | 1–2 |

## 9. Mốc kiểm tra và bậc cắt giảm

| Mốc | Ngày | Điều kiện đạt | Nếu không đạt |
|---|---|---|---|
| #0 | 15/11 | Spike C03 đạt; web + API chạy trên VPS qua HTTPS (`staging.quanlynh.id.vn`); ADR-01 merge; ước lượng lại không lệch > 20% | Họp khẩn với người duyệt trong 24 giờ: đổi phương án C03 hoặc lùi mốc |
| #1 | 22/11 | Seating, Catalog, SalesDay trên staging, FE dùng API thật, CD tự động + quay lui và sao lưu hằng ngày chạy trên VPS | Kích hoạt **bước 1** (phần phạm vi); hạ tầng trượt thì báo người duyệt ngay, không dùng bậc cắt để bù |
| #2 | 29/11 | Gọi món → hóa đơn → thu tiền chạy qua UI | Kích hoạt **bước 2** |
| #3 | 06/12 | Bếp/bar → giao → đóng đơn, có âm thanh, realtime | Kích hoạt **bước 3** |
| #4 | 13/12 | Tier 1 hoàn tất gồm QR và chốt ngày | Kích hoạt **bước 4** và hoãn toàn bộ Tier 2 |

**Bậc cắt giảm (chỉ áp dụng khi người duyệt đồng ý; các mục này nằm trong MVP đã duyệt nên cắt là đổi phạm vi):**

1. Reporting thu gọn: chỉ một màn tổng kết ngày từ snapshot chốt, bỏ khoảng ngày và món bán.
2. Bỏ điều chỉnh tồn có dấu và sửa phiếu nhập (làm sau).
3. Sự cố/làm lại rút xuống: quản lý hủy món có lý do, chưa có luồng làm lại.
4. Hoàn tiền: ghi nhận thủ công ngoài hệ thống có audit, chưa có hạn mức tự động và chờ đối soát.
5. Chuyển bàn và in chứng từ chỉ xem trên màn hình.

**Không cắt:** QR, âm thanh, luồng gọi món → bếp → giao → thu tiền → đóng order, chốt ngày, phân quyền, audit thao tác nhạy cảm, sao lưu/khôi phục.

**Phân tầng:** Tier 1 (xong tuần 5) = đăng nhập/refresh/quyền, bàn, thực đơn, gọi món, bếp/bar, giao, thu tiền, miễn thu, chứng từ, QR, âm thanh, kho cơ bản, chốt ngày, audit, hạ tầng. Tier 2 (tuần 6) = sự cố/làm lại, hoàn tiền, điều chỉnh tồn, báo cáo, quản trị tài khoản (giao diện).

## 10. Kiểm tra công suất (ước lượng của người soạn, chưa được nhóm xác nhận)

Backend, tuần-người, ước lượng bảo thủ (migration + API + test SQL Server):

| Module | Tier 1 | Tier 2 | Chủ |
|---|---|---|---|
| Identity + nền tảng chung | 2.0 | | Hiếu |
| Seating | 1.0 | | Hiếu |
| Sales (kèm QR) | 3.0 | | Hiếu |
| Fulfillment + SignalR | 2.0 | 1.0 | Hiếu |
| **Tổng Hiếu** | **8.0** | **1.0** | |
| SalesDay (kèm chốt ngày) | 2.0 | | Phương |
| Catalog | 1.5 | | Phương |
| Billing | 2.0 | 1.0 | Phương |
| Inventory | 2.5 | 0.5 | Phương |
| Reporting | | 1.0 | Phương |
| **Tổng Phương** | **8.0** | **2.5** | |

Tuần 1–5 mỗi người có 5 tuần. **Ước lượng này vượt công suất khoảng 50% nếu làm đúng mọi hạng mục ở mức bảo thủ.** Lịch ở mục 6 đã nén bằng cách chuyển 3 hạng mục sang Hải (Audit, dữ liệu mẫu, R2) và đẩy Tier 2 sang tuần 6, nhưng vẫn **sát công suất**. Hệ quả:
- Không có chỗ cho trượt trong tuần 2–5; vì vậy mốc #1–#4 và bậc cắt giảm phải được dùng nghiêm túc.
- Nếu ở mốc #0 nhóm ước lượng thực tế thấp hơn (ví dụ do dùng công cụ sinh code hiệu quả), kế hoạch an toàn hơn. Nếu cao hơn, **cần quyết định sớm** giữa: cắt phạm vi, kéo dài thời gian, hoặc bổ sung người cho backend.
- Frontend và DevOps/QA được phân đều hơn, nhưng cả hai phụ thuộc API backend; mock và contract-first giảm thời gian chờ chứ không loại bỏ.

## 11. Việc cần người duyệt xác nhận

| # | Nội dung | Hạn |
|---|---|---|
| 1 | Chấp nhận thay đổi sở hữu: Catalog → Phương, Audit → Hải | Thứ Hai tuần 1 |
| 2 | Ngày bắt đầu thực tế (giả định 09/11) và mức độ dành thời gian của từng người | Thứ Hai tuần 1 |
| 3 | **Đã chọn (2026-11-07): nhóm thuê VPS ngay tuần 1; tên miền `quanlynh.id.vn` đã đăng ký; web và API chạy trực tiếp trên VPS từ thứ Tư–Năm tuần 1 (không còn máy tạm/tunnel).** Cần: (a) nhóm chốt nhà cung cấp, cấu hình (khuyến nghị Ubuntu LTS, 4 vCPU/8 GB RAM; có thể 4 GB nếu dùng SQL Server Express, chưa ai đo), IP tĩnh, có snapshot, và người thanh toán; (b) người đăng ký tên miền cấp cho Hải quyền sửa DNS (và đổi nameserver sang Cloudflare nếu nhà đăng ký `.id.vn` cho phép); (c) production dùng VPS riêng (khuyến nghị) hay chung VPS (chỉ khi RAM đủ cho hai bộ SQL Server), chốt trước tuần 5 | (a, b) Thứ Hai tuần 1 (thuê trước 12:00); (c) Thứ Sáu tuần 4 |
| 4 | Đồng ý bậc cắt giảm ở mục 9 và quy tắc kích hoạt | Thứ Sáu tuần 1 |
| 5 | Duyệt cơ chế cấp mã truy cập QR cho khách theo phiên (Hiếu trình ở tuần 2) | Thứ Sáu tuần 2 |
| 6 | Nội dung chứng từ/phiếu thu cần in | Thứ Sáu tuần 3 |
| 7 | Danh sách kịch bản UAT mục 7 | Thứ Sáu tuần 4 |
| 8 | Có mặt ở demo thứ Sáu hằng tuần; phản hồi quyết định trong 24 giờ | Hằng tuần |

## 12. Điểm chưa xác minh

- Kỹ năng và thời gian thực tế từng người; nhóm chưa ước lượng lại.
- Spike C03 (hai DbContext dùng chung giao dịch): Phương báo 5/5 test đạt trên SQL Server 2022 Docker (`docs/spike-c03-result.md`, 10/11). **Chưa chạy trên CI và chưa được người khác review**; chưa có kiểm thử tải. Chỉ áp dụng khóa Shared/Exclusive cho module khác sau khi CI chạy lại và có review.
- Repo công khai nên có thể bật bảo vệ nhánh; hiện `main` và `develop` chưa được bảo vệ (đã kiểm tra bằng GitHub API ngày 07/11). Cần Hải có quyền quản trị repo để cấu hình.
- Đường dẫn OpenAPI `/openapi/v1.json` đã kiểm chứng ngày 10/11 (chạy host ở Development, trả 200, có 7 đường dẫn). Staging không có đường dẫn này nên file phải xuất và commit (xem `docs/api/identity-contract.md` mục 4).
- Cơ chế cấp mã QR cho khách theo phiên được suy ra từ thiết kế dữ liệu (mã định vị bàn không đủ để đặt món); chi tiết cần chốt ở tuần 2.
- Hành vi âm thanh trên Safari iOS và Android khi trình duyệt chặn phát tự động; cần kiểm thử thiết bị thật.
- Nhà cung cấp và cấu hình VPS cụ thể, ngân sách, và việc tên miền `.id.vn` cho phép đổi nameserver sang Cloudflare (nếu nhà đăng ký không cho, phương án thay thế: trỏ bản ghi A từ DNS của nhà đăng ký và dùng chứng chỉ Let's Encrypt trên Nginx, bỏ proxy Cloudflare; khi đó `img.` cần xem lại).
- Dung lượng RAM thực tế của SQL Server cùng API trên VPS đã chọn; chưa đo. Giới hạn hiện hành của SQL Server Express cũng cần được nhóm kiểm tra lại.
- Tiến độ thuê VPS: kế hoạch tuần 1 giả định VPS có trước 12:00 thứ Hai và sẵn sàng dùng ngay thứ Ba. Một số nhà cung cấp cần xác minh thanh toán/danh tính nên có thể trễ; chưa kiểm chứng với nhà cung cấp nào.
- Cấu hình Nginx + Let's Encrypt, SQL Server trong Docker và Next.js standalone chưa được dựng thử trong repo này; thời lượng "thứ Tư có API, thứ Năm có web" là ước lượng.
- Chưa đo tải realtime/WebSocket; sẽ đo ở HAI-4.1 trên chính VPS.
- Ma trận 17 test chưa chạy đầy đủ cho module nào. Identity có test SQL Server trên CI; SalesDay/Catalog có 5 test SQL của Phương nhưng CI chưa chạy chúng.
