# Tiến độ Hải đến tuần 3 — 19/11/2026

Phân biệt code/test đã có, code đã merge và nghiệm thu thật trên staging. Không dùng tỷ lệ phần trăm khi đầu ra còn phụ thuộc thành viên khác. Agent không merge/auto-merge/bypass PR.

| Công việc | Đã kiểm chứng | Còn lại |
| --- | --- | --- |
| Tuần 1: sửa PR, SSH/UFW/fail2ban/HTTPS | Hiếu đã merge #57/#68/#69; kiểm tra khóa SSH, firewall, timer APT/Certbot và renew dry-run đạt | C03 của Phương, một số nghiệm thu ghi có ngày/dữ liệu/cleanup được nhóm chốt; không coi local test thay live acceptance |
| Tài khoản staging | Manager/Staff đã tạo, login/quyền/cookie/refresh/logout/revocation đạt sau deploy; mật khẩu chỉ ở file riêng của Hải | Không reset lại tài khoản để tạo bằng chứng |
| HAI-2.0/2.5: deploy | 8af7ab3 đã chạy API/Web, gồm responsive #81, UI #76 và Identity API #77; CI/runtime image pin đạt; backup CHECKSUM/VERIFYONLY, isolated restore, migration hai lượt, API probe và public readiness đạt | CD tự động ≤10 phút sau merge, forced-command SSH key/environment secrets và diễn tập rollback có kiểm soát chưa xong. #78 đang review tooling |
| HAI-2.3: smoke/uptime/log | Sau deploy Manager/Staff: 4/4 browser smoke, 0 skipped; desktop/mobile đã xem; public health Healthy | Smoke tự gọi từ CD, cảnh báo uptime và chính sách log/retention còn cần cấu hình/nghiệm thu |
| HAI-2.2: R2 | Chưa có bucket/token scoped hoặc cấu hình riêng do chủ repo cung cấp | Bucket ảnh public/CORS và backup private, kiểm tra upload/read/deny theo đúng quyền |
| HAI-2.6: backup | Backup và restore vào DB tách biệt đã chạy thật trong deploy | Lịch daily + đẩy R2/retention + diễn tập restore từ bản offsite còn thiếu |
| HAI-2.1 (dời tuần 3): seed-demo | Có 2 tài khoản thật; không coi là bộ dữ liệu demo đầy đủ | Chủ module chốt bộ khu vực/bàn/menu/nguyên liệu, seed lặp không trùng; reset chỉ ở môi trường dùng riêng đã được duyệt |
| HAI-2.4 (dời tuần 3): builders/test infra | Có scripts local SQL/guard DB test, một lệnh test; Audit fixture mới dùng DB generated + guarded cleanup | Thư viện builders dùng chung và Hiếu/Phương thực sự dùng trong test của họ |
| HAI-3.1: Audit | Contract #72 đã merge. Nhánh runtime có InitialAudit, SQL append-only, IAuditLog idempotent, GET Manager-only, filter/paging, OpenAPI/client. Local 278 integration + 5 architecture đạt, 0 failed/skipped | CI/review/merge/deploy; adapter dùng hạ tầng #79 sau merge; producer Sales và Billing do chủ module nối, test atomicity và ít nhất một thao tác thật mỗi module |
| HAI-3.2: E2E nghiệp vụ | Smoke read-only/login đạt; chưa ghi tiền/ngày bán cho nghiệm thu | Phụ thuộc API gọi món/xác nhận/thu tiền thật và UI của nhóm. Không dùng mock để báo PASS |
| HAI-3.3: bảo mật | #71 đã được thành viên merge; CI dependency/secret gates hoạt động, chặn High/Critical; trước đó npm còn 7 Moderate | Kiểm tra header/CORS/rate-limit sau các API mới; không báo zero findings hoặc toàn bộ tuần 3 đã xong |

## Bằng chứng staging hiện tại

Source `8af7ab35612a95cb5f2a07778fccf0b3c8f31b6e`, deploy 19/11 lúc 08:33 UTC. API/Web revisions đã được helper kiểm tra; SQL/Nginx container IDs giữ nguyên. Root-private evidence `/var/backups/restaurant-staging-release/20261119T083323Z-227199`. [Báo kết quả cho nhóm](https://github.com/Trunghieu220504/restaurant-management-system/pull/78#issuecomment-7004022608).

Develop đã tiến lên `a06708e` (Outbox #79). Không gọi 8af7ab3 là head develop hiện tại; thay đổi chưa được deploy cần đi qua release mới có đủ migration và quyền root phù hợp.

## Phối hợp tiếp theo

Hải đã hoàn thiện sink/query Audit trên nhánh và gửi Hiếu/Phương review; Bảo Anh/Duy review OpenAPI/client. [Trao đổi trên #79](https://github.com/Trunghieu220504/restaurant-management-system/pull/79#issuecomment-7002892569) yêu cầu event/action codes, Inbox + NhatKy cùng transaction, Outbox ở producer. Không xây dispatcher độc lập hoặc sửa Billing thay chủ module.

Release có Audit phải áp dụng SQL Audit từ đúng CI source, ngoài Seating/Sales. Helpers 95ab29b/f4c7c4a/8af7ab3 chỉ pin các migration của runtime cũ và không được dùng để deploy code Audit. CI của nhánh mới xuất artifact audit-sql-idempotent + SHA256SUMS để chuẩn bị release được review; artifact không tự apply hay cấp quyền.

[Contract/handoff Audit](api/audit-contract.md) · [Bằng chứng test](evidence/week-three/audit-runtime-2026-11-19.md) · [Issue phối hợp #70](https://github.com/Trunghieu220504/restaurant-management-system/issues/70).
