# Tiến độ công việc của Hải — 27/11/2026

## Staging và nghiệm thu tuần 7

- `https://staging.quanlynh.id.vn` chạy frontend và backend `5f638610c0a76b78d13d41ed111deaa41926bb27`. Jenkins build #4 đã kiểm thử, đóng gói và tải đúng SHA lên VPS. Sau khi Hải duyệt SHA, helper staging triển khai từ gói đã tải.
- VPS đã sao lưu có CHECKSUM, khôi phục vào cơ sở dữ liệu riêng, áp dụng migration hai lượt và kiểm tra readiness của cả hai phiên bản API trên bản khôi phục. Sau triển khai, HTTPS, đăng nhập, refresh, `/orders` và WebSocket xác thực đều đạt.
- Bản QR đạt ma trận **17/17** với 493 integration test và 5 architecture test trên SQL Server. Browser staging đã gửi một đợt QR thật, xác nhận và theo dõi, chuyển bàn giữ quyền, giao món, đóng đơn và đóng phiên thu hồi quyền. Đơn thử `DH-0002` có giá 0 đ. Hai bàn thử đã được lưu trữ và món dùng cho lượt kiểm thử đã được ngừng bán.
- [PR #128](https://github.com/Trunghieu220504/restaurant-management-system/pull/128) cập nhật QR trên nhánh `feature/hai-week7-qr-ordering`. Ứng viên `d56783a` giảm JavaScript khởi động QR và sửa lỗi trùng mã đơn khi tạo đồng thời. Các check kiểm thử GitHub của SHA này đã đạt. Trong Jenkins #7, cùng SHA chạy đạt 494 integration test và 5 architecture test trên SQL Server cùng ma trận 17/17.
- Jenkins #7 đã tải gói `d56783a` lên VPS và kiểm checksum tại VPS đạt. Helper dừng ở cổng phê duyệt revision vì SHA này chưa được duyệt và chưa phải HEAD của `develop`. Sau khi operator duyệt, triển khai từ gói đã kiểm checksum.
- Hướng dẫn theo vai trò, tình trạng UAT và các hạng mục vận hành còn mở được ghi trong [hồ sơ bàn giao tuần 7](hai-uat-and-handoff.md).

## Nhật ký chuẩn bị staging trước ngày 25/11/2026

Phần nhật ký dưới đây ghi tình trạng trước khi helper Jenkins được cài và bản QR `5f63861` được triển khai.

## Điểm xuất phát

- Repo: https://github.com/Trunghieu220504/restaurant-management-system
- Đã fetch GitHub và tích hợp develop `f9fbeba` (QR session access, incidents/remake, sensitive-operation audit và UI của nhóm).
- Nhánh làm việc: `feature/hai-2.5-staging-release`.
- Code và gói release: `b2e15e55389f373a3c6b28c5c40d6b50de9a8663`. Source ứng dụng kế thừa UAT `cee11d6`.
- Nhánh sửa lỗi: `fix/hai-5.1-pr105-incidents-ci`, head `dfa1c3a`; đã push cùng sửa đổi lên nhánh #105.
- Commit mới đều có author/committer **Hai249** (`hoangphihai428@gmail.com`), không thêm đồng tác giả.
- Không tự merge vào develop/main, không đổi ruleset, không gắn tag production.

## Trạng thái GitHub ở mốc chuẩn bị

| PR | Nhánh | Trạng thái |
| --- | --- | --- |
| #105 | feature/hai-week7-delivery-20261124 | `dfa1c3a`: tất cả check chạy đều SUCCESS; publish GHCR SKIPPED; mergeable |
| #118 | chore/hai-6.1-uat-integration | `cee11d6`: backend/SQL/web/API sync SUCCESS; đã hết xung đột, mergeable |
| #126 | feature/hai-2.5-staging-release | `b2e15e5`: backend/SQL SUCCESS; draft xếp lớp trên #118, mergeable |

PR #116/#117 đã được merge vào nhánh delivery, #119 đã merge vào UAT trên GitHub trước lượt làm việc này. #105/#118/#126 vẫn mở. Chi tiết check theo SHA: `artifacts/hai-server-candidate/github-status.json`.

## Những việc vừa hoàn thành

1. Sửa lỗi #105 sau upstream incidents: fixture phân quyền đăng ký IFulfillmentIncidents, giữ toàn bộ 10 ca quyền. Sửa thêm fixture SQL truyền businessContext/audit sau khi merge develop; cả check push và PR với develop đã xanh.
2. Giải quyết xung đột #118, tích hợp develop mới; giữ luồng Sales/Billing/Outbox atomic và màn đơn đọc/ghi API thật. Tái sinh OpenAPI/client có incidents và order query.
3. Mọi đường vào `/orders`, kể cả view=service/orderId, chuyển về màn API thật; bỏ nhánh routing còn dùng giao diện giả lập. Nối thao tác sự cố/hoàn tiền theo đơn đã chọn.
4. Khôi phục form sự cố ở bếp, hiển thị tên món/mã đơn, sửa accessible label. Phiếu LOI được xử lý như trạng thái kết thúc để không còn trong hàng chờ khi làm lại.
5. Cài WebSocket middleware trước JWT để trình duyệt gửi token qua query được xác thực đúng; kiểm query/header trong test và xác nhận handshake WebSocket thật qua Nginx.
6. Nghiệm thu thật bếp báo sự cố → Kitchen không có quyền duyệt (403) → Manager duyệt làm lại → replay cùng request không tạo phiếu thứ ba → cùng phần món, hóa đơn giữ nguyên 20.000 VND, không lỗi JavaScript.
7. HAI-5.1 đạt **16/17** tiêu chí có bằng chứng SQL. Tiêu chí 4 vẫn BLOCKED: QR đã có cấp quyền/xem menu/expiry/revocation nhưng chưa có API khách gửi món, kiểm đơn khác phiên và race gửi món–đóng phiên. Không đánh PASS bằng mock.
8. HAI-2.5: tạo installer/helper cho đúng một SHA; build image thật, xuất 9 migration, đóng gói checksum và chuyển lên VPS. Gói không chứa secret, seed hoặc database demo.

## Kiểm chứng

- Full SQL Server 2022: **482 integration + 5 architecture = 487 PASS**, 0 fail/skip. `artifacts/hai-sql-20261125T065302Z/summary.json` và TRX. Chạy trước thay đổi middleware cuối.
- Sau thay đổi middleware: **20/20** test hub/phân quyền/incident PASS. `artifacts/hai-week7-tools/resume-targeted-final.log`. Không cộng hai lượt thành một lượt full suite.
- Web cuối: lint **0 error, 2 warning** có sẵn; format PASS; **82/82 unit PASS**, production build PASS; **41/41 production browser fixture smoke PASS**. `artifacts/hai-week7-tools/server-web-checks.log`.
- E2E thật gọi món/bếp/bar/giao/thu/đóng đơn PASS sau tích hợp. E2E incidents/remake PASS sau sửa form và WebSocket; `artifacts/hai-week7-tools/incident-verified.log`, `artifacts/hai-live-demo/incident-remake-evidence.json`.
- Helper staging: Bash/ShellCheck PASS; **6/6** kiểm thử control flow trong container riêng: plan, success, migration/start/readiness failure và manifest bị sửa. `artifacts/hai-week7-tools/staging-helper-tests-final.log`. Đây chưa phải diễn tập rollback trên VPS.
- Tool release cũ: **9/9** PASS trong Linux, Bash/ShellCheck toàn bộ scripts PASS; `artifacts/hai-week7-tools/release-tools-linux-final.log`. Lượt chạy nhầm shell tests trên Windows thất bại do môi trường, log được giữ.
- Matrix máy đọc: `artifacts/hai-live-demo/matrix-resume.json`; báo cáo repo: `docs/release/hai-matrix-results.md`.
- Soak 50 kết nối/30 phút, backup local và ZAP cũ thuộc bằng chứng lượt trước, **không chạy lại trên VPS mới** và không coi là nghiệm thu bản release hiện tại.

## Server thật — đã xác minh và đã chuẩn bị

- SSH khóa vào `deploy@103.69.97.224` thành công. Firewall 22/80/443, SQL/API không mở trực tiếp. Disk còn khoảng 28 GB khi kiểm.
- URL đích: **https://staging.quanlynh.id.vn**. API/web hiện vẫn `8af7ab35612a95cb5f2a07778fccf0b3c8f31b6e`, `/health/ready` Healthy. Chưa thay runtime hoặc dữ liệu.
- `sudo -n true` trả **sudo: a password is required**. Các NOPASSWD helper hiện có chỉ pin SHA cũ; không dùng chúng để deploy SHA mới.
- Gói đã tải lên: `/home/deploy/restaurant-release-b2e15e55389f373a3c6b28c5c40d6b50de9a8663`.
- Archive image: 213196503 bytes (~203 MiB). **Toàn bộ checksum đã OK trên chính VPS** sau upload.
- API image ID: `sha256:598cd76e073c9b4fa276b440fa5c1074d2ca191ba1a75fe1e13302e81970dc3f`.
- Web image ID: `sha256:dcb7a5badd953c4e1db4ebf519414f2ec998d9ded6eebaa6786e5b47f48d3f16`.
- SHA256 manifest: `49bd21c0d408e807fb1cf5ab9863fe03225319fcb227a226427c7274151af96d`.
- SHA256 installer: `a23cb2f7043a33b028f3c37d7d7f5b6e907221e95306a665a6b507cbc502d3ee`.
- Gói và lệnh operator tại `artifacts/hai-server-candidate/`; bản hướng dẫn `docs/release/hai-staging-candidate.md`.

## Quy trình cài thủ công ở mốc chuẩn bị

Sau SSH vào VPS bằng khóa đã dùng, chạy:

```bash
bash ~/restaurant-install-b2e15e5.sh
```

Lệnh kiểm checksum installer rồi yêu cầu sudo trong phiên SSH. **Nhập mật khẩu sudo trên VPS, không gửi mật khẩu trong chat.** Installer sao chép và kiểm gói tại root-owned path, nạp image, chạy preflight rồi cấp wrapper zero-argument chỉ cho SHA này; **không tự deploy khi cài**.

Sau khi operator báo cài thành công, tiếp tục bằng wrapper `sudo -n /usr/local/sbin/restaurant-deploy-approved-b2e15e55389f373a3c6b28c5c40d6b50de9a8663` cho bản đã được duyệt. Kế hoạch của nhóm yêu cầu staging từ develop; cần review/merge các PR phù hợp trước phát hành thường lệ, không vượt branch protection. Nếu SHA phát hành thay đổi sau merge thì build/xuất gói mới đúng SHA, không đổi nhãn gói cũ.

Khi apply: backup/VERIFYONLY → restore DB scratch → migration hai lượt → old/new API readiness → dừng writer → migration live → thay API/web/Nginx → pin proxy IP thực tế → smoke/revision; lỗi sau dừng writer sẽ khôi phục image cũ/config proxy và vẫn báo FAILED. SQL container/volume giữ nguyên; không seed/reset/restore đè dữ liệu.

## Danh sách công việc ở mốc chuẩn bị

1. Operator cài helper bằng sudo; review/merge release theo quy trình nhóm rồi triển khai staging thật đúng SHA. Sau deploy kiểm browser HTTPS bằng tài khoản staging: login/refresh, gọi món, confirm, bếp/bar, giao, thu tiền/đóng đơn, incidents/remake và WebSocket. Không dùng tài khoản/dữ liệu demo để kết luận server đã hoàn tất.
2. Phối hợp Hiếu hoàn thiện public guest ordering HIE-5.1; thêm test expiry/revocation/cross-session/race trước khi tăng matrix lên17/17.
3. Jenkins controller đã được khởi động lại sau khi thấy container Exited; giữ cấu hình/credential. Chưa công bố build Jenkins mới thành công cho SHA release này. Cần GHCR write:packages, khóa CI forced-command/known_hosts và deploy từ develop để hoàn tất CD tự động; chuyển image qua SSH lần này không thay bằng chứng CD.
4. R2 public images/private backups, tokens/CORS/lifecycle, age/rclone/timer/alert trên VPS; hai restore offsite có API probe vẫn chưa xong. Backup trong helper lần này là local.
5. UAT đủ14 kịch bản hai vòng, iOS/Android thật, SignalR qua VPS/Cloudflare, perf/Lighthouse/cảnh báo thật; chốt production/DNS/cert/secret/admin/license, tập huấn và ký nhận. Chưa main/v1.0.0/production.

**Chưa hoàn tất toàn bộ bảy tuần và chưa ghi server mới chạy thành công.** Bản local chỉ phục vụ kiểm thử kỹ thuật; mục tiêu bàn giao là hệ thống thật trên VPS.

## File thay đổi từ điểm nghỉ 31368b7 đến gói release

A = thêm, M = sửa. Danh sách gồm phần code của nhóm được tích hợp, không tuyên bố Hải viết toàn bộ.

```text
A	docs/api/fulfillment-incidents.md
M	docs/api/fulfillment-realtime.md
A	docs/api/guest-session-access.md
M	docs/api/openapi.json
A	docs/api/sensitive-operation-audit.md
M	docs/release/hai-matrix-results.md
A	docs/release/hai-staging-candidate.md
M	docs/release/hai-week7-status.md
A	docs/release/hai-zap-baseline.md
M	infra/nginx/backend.https.conf
M	infra/nginx/production.conf
M	scripts/Export-FulfillmentOpenApi.cs
A	scripts/Export-GuestMenuOpenApi.cs
A	scripts/release/install-staging-candidate.sh
M	scripts/release/matrix.json
A	scripts/release/staging-candidate.sh
A	scripts/release/staging_candidate_checks.py
M	src/Host/Restaurant.Api/Configuration/AuthenticationConfiguration.cs
A	src/Host/Restaurant.Api/Configuration/GuestMenuAccess.cs
A	src/Host/Restaurant.Api/Modules/BusinessAudit.cs
A	src/Host/Restaurant.Api/Modules/FulfillmentIncidentContext.cs
M	src/Host/Restaurant.Api/Modules/ModuleRegistration.cs
M	src/Host/Restaurant.Api/Modules/OperationsSnapshot.cs
M	src/Host/Restaurant.Api/Modules/OrderClosing.cs
M	src/Host/Restaurant.Api/Program.cs
M	src/Host/Restaurant.Api/packages.lock.json
A	src/Modules/Billing/Restaurant.Modules.Billing.Infrastructure/IncidentObligation.cs
M	src/Modules/Catalog/Restaurant.Modules.Catalog.Presentation/CatalogEndpoints.cs
A	src/Modules/Fulfillment/Restaurant.Modules.Fulfillment.Application/IncidentContracts.cs
M	src/Modules/Fulfillment/Restaurant.Modules.Fulfillment.Domain/FulfillmentEntities.cs
M	src/Modules/Fulfillment/Restaurant.Modules.Fulfillment.Domain/FulfillmentRules.cs
A	src/Modules/Fulfillment/Restaurant.Modules.Fulfillment.Infrastructure/FulfillmentIncidents.cs
M	src/Modules/Fulfillment/Restaurant.Modules.Fulfillment.Infrastructure/FulfillmentModule.cs
M	src/Modules/Fulfillment/Restaurant.Modules.Fulfillment.Infrastructure/FulfillmentService.cs
M	src/Modules/Fulfillment/Restaurant.Modules.Fulfillment.Infrastructure/Messaging/BatchConfirmedHandler.cs
M	src/Modules/Fulfillment/Restaurant.Modules.Fulfillment.Presentation/FulfillmentEndpoints.cs
A	src/Modules/Fulfillment/Restaurant.Modules.Fulfillment.Presentation/IncidentEndpoints.cs
M	src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Accounts/AccountManagement.cs
M	src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Authentication/IdentityAuthentication.cs
M	src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/Restaurant.Modules.Identity.Infrastructure.csproj
M	src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure/packages.lock.json
M	src/Modules/Sales/Restaurant.Modules.Sales.Infrastructure/EfOrderStore.cs
M	src/Modules/Sales/Restaurant.Modules.Sales.Infrastructure/StaffPortions.cs
M	src/Modules/Seating/Restaurant.Modules.Seating.Infrastructure/GuestAccess.cs
M	src/Modules/Seating/Restaurant.Modules.Seating.Infrastructure/Restaurant.Modules.Seating.Infrastructure.csproj
M	src/Modules/Seating/Restaurant.Modules.Seating.Infrastructure/SeatingCatalog.cs
M	src/Modules/Seating/Restaurant.Modules.Seating.Infrastructure/SeatingSessions.cs
M	src/Modules/Seating/Restaurant.Modules.Seating.Infrastructure/packages.lock.json
A	src/Shared/Restaurant.Shared.Contracts/BusinessAudit.cs
A	tests/Restaurant.IntegrationTests/BusinessAuditSqlServerTests.cs
A	tests/Restaurant.IntegrationTests/BusinessAuditTestSupport.cs
M	tests/Restaurant.IntegrationTests/CatalogEndpointTests.cs
M	tests/Restaurant.IntegrationTests/DayLedgerSqlFixture.cs
A	tests/Restaurant.IntegrationTests/FulfillmentIncidentSqlServerTests.cs
A	tests/Restaurant.IntegrationTests/FulfillmentIncidentTests.cs
M	tests/Restaurant.IntegrationTests/FulfillmentPermissionTests.cs
M	tests/Restaurant.IntegrationTests/FulfillmentSqlServerTests.cs
A	tests/Restaurant.IntegrationTests/GuestMenuAccessTests.cs
M	tests/Restaurant.IntegrationTests/IdentityAccountManagementSqlServerTests.cs
A	tests/Restaurant.IntegrationTests/IdentityBusinessAuditTests.cs
M	tests/Restaurant.IntegrationTests/IdentityRefreshTokenSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/IdentitySqlServerAuthenticationTests.cs
M	tests/Restaurant.IntegrationTests/IdentitySqlServerTests.cs
A	tests/Restaurant.IntegrationTests/IncidentEndpointTests.cs
M	tests/Restaurant.IntegrationTests/OperationsHubTests.cs
M	tests/Restaurant.IntegrationTests/OperationsSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/OutboxInboxSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/PhuongWeekFourSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/PhuongWeekThreeSqlServerTests.cs
A	tests/Restaurant.IntegrationTests/SeatingBusinessAuditTests.cs
M	tests/Restaurant.IntegrationTests/SeatingServiceSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/SeatingSessionLookupSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/SeatingSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/packages.lock.json
A	web/e2e-hai-live/incident-remake.spec.ts
A	web/e2e-production/guest-session-access.spec.ts
M	web/e2e-production/week-three-orders.spec.ts
M	web/src/app/(dashboard)/inventory/page.tsx
M	web/src/app/(dashboard)/kitchen/page.tsx
M	web/src/app/(dashboard)/orders/page.tsx
M	web/src/app/globals.css
A	web/src/features/billing/bill-documents.tsx
A	web/src/features/billing/refund-actions.test.ts
A	web/src/features/billing/refund-actions.ts
A	web/src/features/billing/refund-panel.tsx
M	web/src/features/guest/guest-access.tsx
M	web/src/features/guest/guest-menu.tsx
A	web/src/features/inventory/api.test.ts
A	web/src/features/inventory/api.ts
A	web/src/features/inventory/stock-adjustment.tsx
M	web/src/features/kitchen/api.ts
A	web/src/features/kitchen/incident-panel.tsx
A	web/src/features/kitchen/kitchen-incidents.tsx
M	web/src/features/kitchen/kitchen-live.tsx
A	web/src/features/operations/operation-request.ts
M	web/src/features/operations/week-one-demo.tsx
M	web/src/features/orders/live-orders.tsx
A	web/src/features/orders/service-live.tsx
M	web/src/features/orders/staff-orders.tsx
M	web/src/features/sales-day/api.test.ts
M	web/src/features/sales-day/api.ts
M	web/src/features/sales-day/demo-api.ts
M	web/src/features/sales-day/sales-day-screen.tsx
M	web/src/features/sales-day/sales-day.module.css
M	web/src/lib/api/generated/schema.d.ts
```

Giữ stash cũ `Hai249: pending isolated demo seed and staging hardening`, không pop mù. Checkout riêng sửa #105 nằm trong `artifacts/hai-pr105-review`; branch fix đã push. Bản tiến độ trước lượt này được giữ tại `artifacts/hai-server-candidate/progress-before-resume.md`. Hai file tiến độ được đồng bộ local; tài liệu checkpoint này chưa commit/push, source release vẫn pin `b2e15e5`.

## Mốc Jenkins trước khi cài helper CI — 25/11/2026

- Jenkins hiện có đã khởi động tại http://localhost:8080, restart policy unless-stopped; agent restaurant-linux online. Không cài bản Jenkins trùng hoặc xóa volume cấu hình.
- Tạo job riêng restaurant-deployment/staging; Jenkins validate pipeline thành công. Credential SSH riêng nằm trong folder đó, known_hosts lấy từ host key đã tin cậy. Không lưu mật khẩu sudo vào Jenkins.
- Pipeline chuẩn bị checkout develop hoặc SHA duyệt → SQL/backend → web lint/format/unit/build → audit/secret gate → migration/image bất biến → SSH upload/deploy → HTTPS smoke. Chưa có lượt CD thành công; chưa bật được quyền remote cho khóa CI.
- Fetch mới: develop e71521d, #105 đã merge. Đã tích hợp vào nhánh fix/hai-2.5-jenkins-auto-deploy; commit 8470387, tác giả/committer Hai249, đã push.
- Tìm được lỗi installer trước: Compose trên VPS trả networks.internal=null; code cũ kiểm !=null nên preflight dừng. Đổi sang has("internal"), bổ sung test hồi quy và thông báo lỗi không in secret.
- Linux release tests 12/12 và helper rollback checks 6/6 PASS; Bash/ShellCheck PASS. Đây là test công cụ, không thay kiểm chứng deploy thật.
- Bộ xét duyệt tự động từ chối bước cài khóa CI + helper root-owned + sudo NOPASSWD có argument wildcard trên VPS; cần Hải đồng ý rõ phạm vi quyền. Không thực hiện bootstrap, không vượt chặn bằng đường khác.
- Bản quyền cần duyệt: artifacts/hai-jenkins-cd/QUYEN-CAN-DUYET.txt. Bộ cài local: artifacts/hai-jenkins-cd/bootstrap/install-ci.sh và SHA256SUMS. Chưa upload/cài bootstrap này lên VPS.
- Quyền dự kiến chỉ cho upload/deploy SHA, không shell/SFTP/forwarding; helper xác minh SHA current develop hoặc root-approved. Helper có thể dùng Docker, backup, migration staging và đổi API/web/Nginx/rollback; lỗi có thể ảnh hưởng downtime/schema staging. Không mở quyền production.
- Chờ đồng ý cho quyền trên; sau đó cài bootstrap, chạy pipeline trên revision đã duyệt và xác minh frontend/backend thật qua HTTPS. Server mới CHƯA được tuyên bố deploy thành công.
