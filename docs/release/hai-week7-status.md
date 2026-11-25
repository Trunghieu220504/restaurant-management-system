# Tóm tắt công việc của Hải — bàn giao khi tạm nghỉ

Cập nhật ngày **25/11/2026 (giờ Việt Nam)**. **TẠM DỪNG theo yêu cầu của Hải.**

Bản này thay bản tóm tắt kiểm tra ban đầu đã lỗi thời. Nhiệm vụ từ `D:/Down/công việc chi tiết.pdf` đã được đối chiếu trong đợt triển khai trước. **Chưa hoàn thành trọn bảy tuần:** matrix 15/17; còn nghiệm thu tích hợp, VPS/CD, R2, production và bàn giao thực tế.

## Điểm tiếp tục

- Repository: https://github.com/Trunghieu220504/restaurant-management-system
- Workspace: `D:/Học/đồ án sever nâng cao/restaurant-management-system`.
- Nhánh hiện tại: `chore/hai-6.1-uat-integration`.
- HEAD: `31368b7d672410a3d57662bf6d94bc773204b1ac`.
- Working tree sạch trước lượt tạm nghỉ. Lượt này chỉ cập nhật tài liệu và log; không phát triển thêm tính năng, commit/push/deploy.
- Commit mới phải dùng author/committer `Hai249`, email `hoangphihai428@gmail.com`, không thêm tên/đồng tác giả của công cụ hỗ trợ. Giữ nguyên tác giả commit của thành viên khác khi tích hợp.
- Push URL của origin đang bị vô hiệu hóa có chủ đích; các lần push trước dùng URL HTTPS repo trực tiếp.

## Kiểm tra chạy lại ngay trước khi nghỉ

| Kiểm tra | Kết quả |
| --- | --- |
| `dotnet build RestaurantManagement.slnx -c Release --no-restore -m:2 -nodeReuse:false` | PASS, exit 0, 0 warning, 0 error, 44,87 giây |
| `npm.cmd run build` trong web, `NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true` | PASS, exit 0; TypeScript/prerender 21/21 hoàn tất |
| Docker demo SQL/API/web/Nginx | Cả 4 container đang chạy, SQL healthy |
| `http://localhost:3187/health/live` | HTTP 200 |
| `http://localhost:3187/health/ready` | HTTP 200 |
| `http://localhost:3187/login` | HTTP 200 |
| GitHub CI tại HEAD `31368b7` | Build and test + Identity SQL Server tests SUCCESS |
| GitHub CI tại `62b238e` (thay đổi web gần nhất) | Web Build and Test + Verify API Client Sync + backend/SQL SUCCESS |
| GitHub CI tại `d925a51` (sửa #105/Jenkins) và `f9ab5d7` (header riêng) | Các check trả về SUCCESS; publish GHCR SKIPPED theo nhánh |

Log: `artifacts/hai-pause-20261125/backend-build.log`, `frontend-build.log`, `github-checks.jsonl`, `demo-containers.txt`, `demo-health.json`. File tự sinh `web/next-env.d.ts` đã phục hồi nội dung trước build.

Kết quả này xác nhận code hiện tại build được và demo local đáp ứng. Không thay thế nghiệm thu toàn bộ chức năng hoặc production. Full SQL/Playwright không chạy lại trong lượt tạm nghỉ; số liệu bên dưới là bằng chứng của đợt triển khai vừa hoàn thành.

## Bản có thể mở thử

- Demo: **http://localhost:3187**, trên máy Hải khi Docker Desktop và stack demo đang chạy.
- Jenkins: **http://localhost:8080**.
- Tài khoản: `demo.staff`, `demo.kitchen`, `demo.bar`, `demo.manager`, `demo.admin`. Mật khẩu nằm trong `artifacts/hai-live-demo/credentials.json`; không đưa vào Git/tài liệu.
- Dùng profile/cửa sổ trình duyệt riêng cho staff, kitchen, bar. Luồng đã kiểm: tạo đơn → chọn món/size/topping → xác nhận → nhận đúng trạm → chuẩn bị → giao → thu tiền → đóng đơn.
- Demo có dữ liệu riêng; Nginx chỉ bind 127.0.0.1:3187, SQL/API không mở trực tiếp. Lượt tạm nghỉ không tắt/reset container.
- Khởi động lại bằng `python scripts/release/start_demo.py`; chỉ dùng `--skip-build` khi image còn đúng source. Không reset dữ liệu staging chung.

## Tiến độ theo nhiệm vụ

| Mã | Đã thực hiện | Còn thiếu/nghiệm thu |
| --- | --- | --- |
| HAI-2.0 | Audit VPS: SSH key-only, root/password login tắt, UFW 22/80/443, SQL/API không publish, updates/certbot | Staging vẫn revision cũ 8af7ab3; cần deploy bản được duyệt và kiểm lại |
| HAI-2.1 | Seed demo 5 role/bàn/món/topping/nguyên liệu/ngày; reset có guard nonproduction/DB/ownership marker | Chưa đầy đủ luồng QR/incidents toàn bộ module |
| HAI-2.2 | API ảnh R2 có trong code nền, hướng dẫn bucket/token/CORS | Chưa cấu hình/kiểm R2 thật, public images/private backup |
| HAI-2.3 | Smoke, monitor ngưỡng/backup age, log rotation/timer mẫu | Cài timer và thử cảnh báo thật trên VPS |
| HAI-2.4 | SQL runner, fixture/pool cleanup dùng chung, no-skip gate; full suite 446 PASS | Thành viên chạy lại/xác nhận bàn giao |
| HAI-2.5 | Jenkins local/agent/multibranch; SQL/web/security/migration; giới hạn worker 2/tắt MSBuild node reuse | Credential GHCR/SSH, helper VPS, deploy/rollback thật |
| HAI-2.6 | Backup CHECKSUM/VERIFYONLY → age → rclone local → readback → restore DB mới → CHECKDB/sentinel PASS | Offsite R2 và restore có API probe |
| HAI-3.1 | Audit Contracts/InitialAudit/API/query/tests trong code nền | Rà producer nhạy cảm theo module, nghiệm thu staging mới |
| HAI-3.2 | E2E UI/API/SQL/SignalR thật tạo/gọi/xác nhận/chế biến/giao/thu/đóng đơn PASS | Đủ kịch bản sau deploy, gồm đăng nhập/mở ngày và luồng còn thiếu |
| HAI-3.3 | NuGet 0 finding, npm 7 finding nhưng không High/Critical, Gitleaks sạch trong lượt kiểm trước | Kiểm tiếp theo SHA mới; không coi là không còn mọi rủi ro |
| HAI-4.1 | SignalR qua Nginx local 50 kết nối/30 phút PASS; snapshot/reconnect/refresh/dedup | Thử qua VPS/Cloudflare thật |
| HAI-4.2 | Tool backup mã hóa, retention/lifecycle runbook, monitor disk/RAM | Cài R2/lifecycle/timer và thử alert thật |
| HAI-4.3 | UI bếp/bar thật, tên món/snapshot, quyền prepare/deliver, bật âm thanh có thao tác và thông báo thị giác | iOS/Android vật lý, mạng chập chờn thực tế |
| HAI-5.1 | Matrix 15/17; atomic Sales/Billing/Outbox, rollback/concurrency/replay SQL tests | Tiêu chí 4 QR ordering và 6 incident/remake BLOCKED |
| HAI-5.2 | ZAP passive staging 24 URL: 0 High/Critical, 2 Medium/6 Low/4 Informational; header/log fix PR #119, nginx -t PASS | Review/merge/deploy/quét lại; chưa đủ nghiệm thu CORS/rate limit |
| HAI-5.3 | Tool restore, biểu mẫu 14 kịch bản UAT/backlog | R2 restore/API probe và xác nhận người nghiệm thu |
| HAI-6.1 | Demo thật, tích hợp Billing/Kitchen của Duy, orders API thật, Playwright/evidence | Chưa đủ buổi UAT vòng 1/14 kịch bản với chủ repo |
| HAI-6.2 | Compose production/network/volume/secret mẫu tách biệt | Chọn máy production, DNS/cert/secret/admin/license SQL |
| HAI-6.3 | Runner k6/Lighthouse/monitor/runbook | Đo tải/Lighthouse, alert thật, restore offsite lần 2 |
| HAI-7.1 | Biểu mẫu UAT vòng 2 | Chưa nghiệm thu vòng 2 với chủ repo |
| HAI-7.2 | Checklist release/rollback, publish/deploy có điều kiện nhánh | Chưa đủ gate; chưa main/v1.0.0/production |
| HAI-7.3 / ALL-7.3 | Runbook, tài liệu và bằng chứng bàn giao | Tập huấn, thiết bị thật, ký nhận |

## Các thay đổi chính đã hoàn thành

1. Jenkinsfile và scripts release: SQL/web/security/migration, backup/rollback, SSH dispatcher, monitor/backup/restore. Sửa MSB4166 do tiến trình dotnet tồn đọng bằng giới hạn worker/tắt persistent MSBuild nodes.
2. Seed/reset demo có guard, compose riêng và helper khởi động, không reset staging.
3. OrderService đưa Billing vào cùng SQL transaction với Sales/Outbox. Test lỗi Outbox rollback toàn bộ, confirm đồng thời, replay không nhân đôi hóa đơn/phiếu.
4. Query/list đơn phân trang từ server, kiểm quyền; `/orders` đọc/ghi API thật, giữ ảnh/tình trạng món và chỉnh phần món.
5. Snapshot/SignalR có tên món, size/topping/ghi chú/mã đơn; xử lý event cũ/trùng/tombstone/reconnect/refresh. Staff giao theo permission, kitchen/bar chuẩn bị theo trạm.
6. Billing lấy hóa đơn/ngày từ đơn, giữ idempotency khi kết quả chưa rõ, ẩn thao tác thiếu quyền; tích hợp frontend của Duy và nối API thật.
7. OpenAPI/client, fixture, SQL acceptance, Playwright và soak cập nhật. Sửa #105 bằng đăng ký IOperationsSnapshot trong fixture phân quyền.
8. Header bảo mật/bỏ query token khỏi log Nginx ở nhánh riêng HAI-5.2, chưa nằm trong HEAD UAT và chưa deploy.

## Bằng chứng từ đợt triển khai vừa hoàn thành

- Backend: **441 integration + 5 architecture = 446 PASS**, 0 fail/skip, SQL Server 2022 Docker. `artifacts/hai-sql-20261124T192756Z/summary.json` và TRX.
- Web: **79/79 unit**, lint 0 error/10 warning có sẵn; **40/40 production UI fixture smoke**, tách biệt E2E thật.
- E2E thật: 3 context Staff/Kitchen/Bar, 2 phần món, 1 khoản thu 45.000 VND, dư nợ 0, đóng đơn; 0 lỗi JavaScript; viewport 390px không tràn ngang. `artifacts/hai-live-demo/core-flow-evidence.json`, `paid-order.png`, `orders-mobile.png`.
- SignalR 50 kết nối/1.801.014 ms, cuối kỳ 50 active, 0 failure, 2 refresh/100 reconnect do JWT hết hạn. `artifacts/hai-live-demo/signalr-30min.json`. Chỉ local, không phải VPS/Cloudflare.
- Matrix **15/17, BLOCKED**: `artifacts/hai-live-demo/matrix-final.json`, `scripts/release/matrix.json`.
- Jenkins UAT **build #4 SUCCESS tại 31368b7**, 446 test PASS; matrix vẫn BLOCKED. `artifacts/hai-live-demo/jenkins-verified.json`. Publish/deploy skipped trên feature; không suy diễn trạng thái build khác từ #4.
- Backup/restore local PASS: `artifacts/hai-week7-tools/backup-restore-evidence.json`; chưa phải R2/API probe.
- ZAP staging revision cũ, image pin digest: `artifacts/hai-week7-zap/report.json`, `report.html`. Chưa quét UAT mới trên VPS.
- Release tooling 9 test PASS, gồm rollback mô phỏng; Bash/ShellCheck PASS.

## Nhánh và PR đã đẩy

| PR | Nhánh | Nội dung |
| --- | --- | --- |
| #105 | feature/hai-week7-delivery-20261124 | Hạ tầng/Jenkins, sửa CI/resource guard |
| #116 | feature/hai-2.1-2.4-demo-tests | Seed/reset demo và SQL tests |
| #117 | fix/hai-5.1-sales-billing-atomicity | Sales/Billing/Outbox atomicity |
| #118 | chore/hai-6.1-uat-integration | Demo tích hợp hiện tại |
| #119 | fix/hai-5.2-zap-headers | ZAP/header/log token |

Nhánh sửa riêng: `fix/hai-5.1-pr105-ci`, `fix/hai-2.5-jenkins-resource-guard`. #116–#119 được tạo draft xếp lớp; #105 đã sẵn sàng review theo lần kiểm trước. Chưa tự merge vào develop/main. Lượt tạm nghỉ đã đọc lại CI tại các SHA nêu trên; khi tiếp tục phải kiểm lại PR/remote mới nhất.

## Việc tiếp theo theo thứ tự

1. Kiểm git status, fetch GitHub, đọc PR #105/#116–#119 và thay đổi nhóm; tiếp tục từ nhánh/HEAD đã ghi. Giữ tên nhánh theo mã nhiệm vụ và danh tính Hai249. Không push thẳng develop/main hoặc tự duyệt PR.
2. Review/tích hợp QR/session-access, incidents/remake và UI liên quan; kiểm chứng đủ matrix 4 và 6. Chạy suite bị ảnh hưởng, full SQL/E2E trước khi công bố 17/17.
3. Operator có quyền cài helper CD root-owned/compose/sudoers mới. User deploy chỉ gọi được helper pin revision cũ và không có Docker socket. Staging lần kiểm trước vẫn `8af7ab35612a95cb5f2a07778fccf0b3c8f31b6e`; không dùng helper cũ triển khai SHA mới.
4. Cấp Jenkins GHCR credential có write:packages, khóa SSH forced-command riêng, verified known_hosts. Token GitHub đã kiểm chỉ có gist/repo/workflow.
5. Cấu hình R2 ảnh/backup, scoped token/CORS/lifecycle, age/rclone/timer/alert trên VPS. Thực hiện **hai restore R2 kèm API probe**, lưu bằng chứng.
6. Review/tích hợp header #119; deploy staging có backup, kiểm migration/smoke/rollback và quét ZAP lại đúng revision.
7. Đủ UAT hai vòng/14 kịch bản, iOS/Android thật, SignalR qua VPS/Cloudflare, perf/Lighthouse/cảnh báo thật; ghi lỗi/chủ xử lý.
8. Chốt production/DNS/cert/secret/admin/license; chỉ main/v1.0.0/production khi đủ gate và được duyệt; tập huấn/ký nhận.

Giữ stash `Hai249: pending isolated demo seed and staging hardening`. Phần cần thiết đã lên nhánh riêng; stash có thử nghiệm cũ, không pop mù hoặc xóa trước đối chiếu.

## Danh sách file thêm/sửa trong nhánh tích hợp

Lấy trực tiếp từ `git diff --name-status f7883fb...HEAD`, so với mốc develop đã tích hợp. Bao gồm cả mã của thành viên khác được tích hợp; không phải tuyên bố Hải viết toàn bộ. A = thêm, M = sửa.

```text
M	.dockerignore
A	.github/workflows/release-tooling.yml
M	.gitignore
A	Jenkinsfile
M	docs/api/openapi.json
A	docs/hai-2.1-2.4-demo-tests.md
A	docs/hai-5.1-sales-billing.md
A	docs/hai-6.1-live-uat.md
A	docs/release/hai-matrix-results.md
A	docs/release/hai-operations-runbook.md
A	docs/release/hai-uat-and-handoff.md
A	docs/release/hai-week7-status.md
A	infra/compose.demo.yml
A	infra/compose.logging.yml
A	infra/compose.production.yml
A	infra/deploy.env.example
A	infra/docker/Dockerfile.migrations
A	infra/jenkins/Dockerfile.agent
A	infra/jenkins/launch-agent.sh
A	infra/jenkins/multibranch.xml
M	infra/nginx/backend.https.conf
A	infra/nginx/demo.conf
A	infra/nginx/production.conf
A	infra/operations.env.example
A	infra/systemd/restaurant-backup.service
A	infra/systemd/restaurant-backup.timer
A	infra/systemd/restaurant-monitor.service
A	infra/systemd/restaurant-monitor.timer
M	scripts/Assert-WeekOneLocalSql.ps1
A	scripts/Export-SalesReadOpenApi.cs
M	scripts/Get-WeekOneLocalSql.ps1
M	scripts/Test-WeekOneSqlGuard.ps1
A	scripts/release/backup.sh
A	scripts/release/common.sh
A	scripts/release/deploy.sh
A	scripts/release/evidence.py
A	scripts/release/export-migrations.sh
A	scripts/release/hot-flows.k6.js
A	scripts/release/lighthouserc.cjs
A	scripts/release/matrix.json
A	scripts/release/monitor.py
A	scripts/release/publish.sh
A	scripts/release/restore-drill.sh
A	scripts/release/signalr.k6.js
A	scripts/release/smoke.py
A	scripts/release/sql.runsettings
A	scripts/release/ssh-dispatch.sh
A	scripts/release/start_demo.py
A	scripts/release/test-sql.sh
A	scripts/release/test_deploy.py
A	scripts/release/test_dispatch.py
A	scripts/release/test_evidence.py
A	scripts/release/test_monitor.py
A	scripts/release/test_sql_local.py
A	scripts/release/zap.sh
A	src/Host/Restaurant.Api/Configuration/DemoSeedCommand.cs
M	src/Host/Restaurant.Api/Modules/OperationsSnapshot.cs
M	src/Host/Restaurant.Api/Modules/TicketChangedHandler.cs
A	src/Host/Restaurant.Api/Modules/TicketDescriptions.cs
M	src/Host/Restaurant.Api/Program.cs
M	src/Modules/Fulfillment/Restaurant.Modules.Fulfillment.Application/FulfillmentModels.cs
M	src/Modules/Fulfillment/Restaurant.Modules.Fulfillment.Presentation/FulfillmentEndpoints.cs
A	src/Modules/Sales/Restaurant.Modules.Sales.Application/OrderQueries.cs
M	src/Modules/Sales/Restaurant.Modules.Sales.Application/OrderService.cs
A	src/Modules/Sales/Restaurant.Modules.Sales.Infrastructure/OrderQueries.cs
M	src/Modules/Sales/Restaurant.Modules.Sales.Infrastructure/SalesModule.cs
M	src/Modules/Sales/Restaurant.Modules.Sales.Presentation/SalesEndpoints.cs
M	tests/Restaurant.IntegrationTests/CatalogServiceSqlServerTests.cs
A	tests/Restaurant.IntegrationTests/DemoSeedSqlServerTests.cs
A	tests/Restaurant.IntegrationTests/FulfillmentPermissionTests.cs
M	tests/Restaurant.IntegrationTests/FulfillmentSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/IdentitySqlServerTests.cs
M	tests/Restaurant.IntegrationTests/OperationsSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/PhuongWeekFiveSqlServerTests.cs
A	tests/Restaurant.IntegrationTests/SalesBillingAcceptanceSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/SalesOrderServiceTests.cs
M	tests/Restaurant.IntegrationTests/SalesOrderSettlementSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/SeatingServiceSqlServerTests.cs
M	tests/Restaurant.IntegrationTests/SeatingSessionLookupSqlServerTests.cs
A	tests/Restaurant.IntegrationTests/SqlTestDatabase.cs
A	tests/Restaurant.IntegrationTests/SqlTestDatabaseTests.cs
A	web/e2e-hai-live/core-flow.spec.ts
M	web/e2e-production/duy-boundaries.spec.ts
M	web/e2e-production/week-three-orders.spec.ts
A	web/playwright.hai-live.config.ts
A	web/scripts/hai-signalr-soak.mjs
M	web/src/app/(dashboard)/kitchen/page.tsx
M	web/src/app/(dashboard)/orders/page.tsx
A	web/src/features/billing/api.test.ts
A	web/src/features/billing/api.ts
A	web/src/features/billing/billing-live.tsx
A	web/src/features/billing/request-attempts.test.ts
A	web/src/features/billing/request-attempts.ts
A	web/src/features/kitchen/api.test.ts
A	web/src/features/kitchen/api.ts
A	web/src/features/kitchen/kitchen-live.tsx
M	web/src/features/operations/week-three-demo.tsx
A	web/src/features/orders/live-api.ts
A	web/src/features/orders/live-orders.tsx
M	web/src/lib/api/generated/schema.d.ts
M	web/src/lib/realtime/signalr.ts
```

Riêng nhánh HAI-5.2 (chưa có trong UAT), diff từ merge-base với nhánh UAT:

```text
A	docs/release/hai-zap-baseline.md
M	infra/nginx/backend.https.conf
M	infra/nginx/production.conf
```

Lượt tạm nghỉ cập nhật D:/Down/tom-tat-cong-viec-hai.md và đồng bộ docs/release/hai-week7-status.md; tài liệu repo chưa commit/push. Bản tóm tắt cũ: artifacts/hai-pause-20261125/tom-tat-cong-viec-hai.before.md. Log, mật khẩu và artifact local không đưa vào Git.
