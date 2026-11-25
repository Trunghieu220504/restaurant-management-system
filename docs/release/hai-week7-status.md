# Tình trạng nhiệm vụ Hải đến tuần 7 — 24/11/2026

Đã đọc PDF `công việc chi tiết.pdf` trước rồi đối chiếu `tom-tat-cong-viec-hai.md`. Bản tóm tắt cũ chỉ phản ánh kiểm tra ban đầu; `develop` mới nhất khi bắt đầu là `b3acf64` (PR #101). Đã fetch lại và tích hợp `cf40c97` gồm PR #103/#104 của Hiếu (consumer và state API Fulfillment). Nhánh triển khai: `feature/hai-week7-delivery-20261124`. Tác giả/committer yêu cầu: Hai249.

**Chưa thể ghi toàn bộ tuần 7 Done.** Tuần 7 trong PDF là 21–25/12/2026. Có thể chuẩn bị công cụ ngay, nhưng UAT với chủ repo, tập huấn thiết bị thật, production, R2 và các API phụ thuộc cần bằng chứng thực tế.

## Đối chiếu nhiệm vụ

| Mã | Đầu ra hiện có hoặc thêm trong nhánh này | Trạng thái nghiệm thu / còn thiếu |
| --- | --- | --- |
| HAI-2.0 | Audit SSH/UFW/fail2ban/updates/certbot; readiness thực tế đạt | Staging vẫn chạy `8af7ab3`, chưa phải develop mới |
| HAI-2.1 | Chưa có seed/reset demo đầy đủ các module | Cần dữ liệu dùng riêng, public ordering/Fulfillment hoàn chỉnh; không reset staging chung |
| HAI-2.2 | API ảnh R2 đã có trên develop; cấu hình bucket/token và kiểm public/private nằm trong runbook | Chưa có quyền/tài khoản R2 để tạo/kiểm bucket |
| HAI-2.3 | Smoke HTTPS 3/3 PASS; monitor ngưỡng/backup age; log rotation override | Chưa cài timer/alert receiver trên VPS |
| HAI-2.4 | Helper pool test dùng chung cho 5 fixture; SQL local shared-memory; Linux SQL runner riêng, kiểm no-skips | Test thật và giới hạn tải ghi phần kiểm chứng; chưa yêu cầu thành viên xác nhận bàn giao |
| HAI-2.5 | Jenkinsfile, agent, multibranch XML; migration checksum; SSH forced-command; backup trước deploy và rollback digest | Jenkins được cấu hình cục bộ; VPS cần operator cài helper, credentials và migration compatibility trước deploy; chưa diễn tập thật |
| HAI-2.6 | Backup checksum → age → private R2, readback digest, timer daily; restore DB riêng | Chưa chạy R2/restore/API probe thật |
| HAI-3.1 | Audit Contracts/InitialAudit/API/query/tests đã có trên develop | Chưa deploy Audit mới; producer nhạy cảm phải đủ theo module |
| HAI-3.2 | Smoke/API test có sẵn; chưa có E2E toàn bộ login → mở ngày → gọi → xác nhận → thu tiền sau deploy | Bản staging cũ; chưa có nghiệm thu UI nghiệp vụ |
| HAI-3.3 | Security CI hiện có; Jenkins chạy NuGet/npm/Gitleaks trước publish | Kết quả Jenkins/GitHub CI cần xem theo SHA; không báo không có lỗ hổng khi chưa quét |
| HAI-4.1 | Nginx WebSocket và k6 50 kết nối/30 phút | Consumer/state API Fulfillment đã merge; vẫn chưa có SignalR hub trên develop |
| HAI-4.2 | Mã hóa age, local retention 7 ngày, R2 lifecycle runbook, monitor disk/RAM | Chưa cài và thử alert thật |
| HAI-4.3 | Bảng thiết bị/reconnect/dedup trong UAT | API start/ready/deliver đã có; còn hub, tích hợp UI và Safari iOS/Android vật lý |
| HAI-5.1 | Mapping đủ 17 tiêu chí, parser TRX, kiểm SHA bằng chứng, FAIL/SKIP rõ ràng | Các dòng chưa đủ test không được PASS |
| HAI-5.2 | ZAP baseline runner, report high-severity gate | Chưa chạy ZAP image được pin và nghiệm thu header/CORS/rate limit mới |
| HAI-5.3 | Restore tool, 14 bước UAT và biểu mẫu kết quả | Cần R2 và chủ repo xác nhận UAT |
| HAI-6.1 | Biểu mẫu UAT vòng 1, backlog có chủ/mức ưu tiên | Chưa thực hiện buổi UAT |
| HAI-6.2 | Compose/network/volume/secret riêng cho production, mặc định loopback | Chưa có quyết định VPS production/DNS/cert/secret/license SQL |
| HAI-6.3 | k6 hot-flow fixture runner; Lighthouse 80/90; monitor; runbook | Chưa đo tải thật, chưa restore lần 2 |
| HAI-7.1 | Biểu mẫu UAT vòng 2 và yêu cầu bằng chứng | Chưa thực hiện với chủ repo |
| HAI-7.2 | Checklist phát hành/rollback, pipeline chỉ deploy develop | Chưa đủ release gates; chưa merge main/gắn v1.0.0/deploy production |
| HAI-7.3 / ALL-7.3 | Runbook + UAT/device/backlog + biểu mẫu bàn giao | Tập huấn và ký nhận cần người dùng/nhóm tham dự |

## Phát hiện đã kiểm chứng

- VPS `103.69.97.224`: SSH chỉ khóa, root login/password login tắt; UFW chỉ 22/80/443, SQL/API không publish; security updates và certbot timer active.
- API/web staging cùng revision `8af7ab35612a95cb5f2a07778fccf0b3c8f31b6e`; `/health/live`, `/health/ready`, `/login` đều HTTP 200 và có HSTS. Đây là smoke bản cũ, không phải nghiệm thu bản mới.
- User deploy không được Docker socket; NOPASSWD chỉ có helper cũ theo SHA và helper audit. Chưa có quyền noninteractive cài helper CD mới.
- Jenkins ở Docker Desktop, container `jenkins` đã dừng; đã khởi động lại, xác thực admin bằng credential setup có sẵn, giữ nguyên security realm và authorization. Đã tạo node `restaurant-linux`, agent image .NET/Node/Docker/Gitleaks và job multibranch `restaurant-management`. Jenkinsfile đã qua linter của Jenkins thật.
- GitHub authenticated user là `Hai249`, repository permission `push=true`, `admin=false`. Không thay đổi ruleset hoặc tự duyệt/merge PR.

## Kiểm chứng

Backend: full suite 397 integration + 5 architecture PASS trên SQL Server 2022, 0 skipped; bổ sung 3 test còn thiếu cho matrix và chạy riêng 3/3 PASS. Ma trận đạt 14/17 dòng; đã bổ sung SQL test giao đồng thời cho API mới và 10 test HTTP phân quyền; chờ full Jenkins trên head cuối. Còn public ordering, liên kết confirm đến consumer và incident/remake. Frontend: lint 0 error/11 warning, 71/71 test PASS, production build PASS. Release tooling: 9 test PASS (bao gồm các tình huống rollback mô phỏng); Bash syntax và ShellCheck PASS; compose production config PASS với giá trị giả. Jenkinsfile validation PASS, node restaurant-linux online. [Ma trận SQL](hai-matrix-results.md) vẫn BLOCKED bởi các tiêu chí thiếu coverage. Các log thử thất bại vẫn giữ trong `artifacts/hai-week7-*`; không đổi FAIL thành PASS bằng cách bỏ test.

Tài liệu vận hành: [runbook](hai-operations-runbook.md). Nghiệm thu/bàn giao: [UAT và backlog](hai-uat-and-handoff.md). Mapping máy đọc: `scripts/release/matrix.json`.

## Cập nhật tích hợp Fulfillment

Sau khi merge cf40c97, phát hiện endpoint deliver/cancel dùng cùng kiểm tra role bếp/bar: Staff bị chặn giao món, còn bếp lại được gọi giao/hủy. Đã đổi sang permission claim theo từng hành động: prepare theo trạm; deliver theo fulfillment.portions.serve; cancel theo fulfillment.incidents.approve. Thêm 10 ca HTTP và SQL tranh chấp giao món; nhóm Fulfillment chạy thật 34/34 PASS, 0 skipped. Không thay hình dạng request/response.

Jenkins build #1 trên c29e2a5 đã SUCCESS: full SQL/web/security/migration; NuGet 0 finding, npm 7 finding nhưng 0 High/Critical, Gitleaks sạch; bundle có 9 module. Nhánh feature bỏ qua publish/deploy đúng thiết kế. Build mới sau tích hợp sẽ là bằng chứng cuối.

Backup/restore đã thử trong container SQL Server riêng: BACKUP/CHECKSUM/VERIFYONLY → age → rclone local alias → đọc lại checksum → giải mã → restore database mới → CHECKDB → sentinel đúng 1 dòng, PASS. Đây là kiểm thử local, không phải R2 thật hoặc API probe. Hai container test được xóa sau chạy; không tác động VPS.
