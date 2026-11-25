# Ma trận SQL của Hải — 25/11/2026

Full SQL Server 2022: **482 integration + 5 architecture = 487 PASS**, 0 failed/skipped. Nguồn: `artifacts/hai-sql-20261125T065302Z/summary.json` và TRX. Sau sửa middleware, thêm kiểm chứng tập trung 20/20 phân quyền/SignalR; không cộng thành một lượt full suite mới.

**16/17 tiêu chí**, toàn bộ matrix vẫn **BLOCKED** do public guest ordering chưa được triển khai. PASS dưới đây là SQL coverage; không thay UAT trên VPS.

| # | Tiêu chí | Trạng thái |
| --- | --- | --- |
| 1 | Unique identity, invoice and charge | PASS |
| 2 | Variant and option ownership | PASS |
| 3 | Concurrent seating open and transfer | PASS |
| 4 | QR expiry, revocation and session isolation | SKIP |
| 5 | Confirmation produces one charge and first ticket | PASS |
| 6 | Approved remake without new revenue | PASS |
| 7 | Cannot serve twice or after cancellation | PASS |
| 8 | Concurrent collectors never overcollect | PASS |
| 9 | Refund reservation under concurrency and unknown payout | PASS |
| 10 | Prior-day refunds preserve prior snapshots | PASS |
| 11 | Purchase snapshots survive unit conversion edits | PASS |
| 12 | Archived stock remains countable and blocks structural changes | PASS |
| 13 | Residual stock value and signed adjustments | PASS |
| 14 | Atomic concurrent closing and late writers | PASS |
| 15 | Closing rollback leaves no partial state | PASS |
| 16 | Negative profit and distinct units | PASS |
| 17 | Permission revocation affects existing access token | PASS |

Tiêu chí 4 đã có test invitation một lần, token hết hạn/thu hồi và không tái sử dụng sau mở lại bàn. Còn thiếu guest gửi món, kiểm đơn khác phiên và cạnh tranh với đóng phiên; không giả lập các phần này để đánh PASS.

Tiêu chí 6 kiểm remake đồng thời/idempotency, không thêm doanh thu, chặn giao khi pending, rollback Billing/Outbox và kiểm quyền. E2E thật bổ sung: `web/e2e-hai-live/incident-remake.spec.ts`; bằng chứng `artifacts/hai-live-demo/incident-remake-evidence.json`.

Mapping tên test: `scripts/release/matrix.json`. Báo cáo máy đọc: `artifacts/hai-live-demo/matrix-resume.json`.
