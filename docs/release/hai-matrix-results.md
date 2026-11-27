# Ma trận SQL của Hải — 25/11/2026

Lượt kiểm thử sau khi hoàn thiện gửi món qua QR chạy trên SQL Server 2022. **493 integration + 5 architecture = 498 PASS**, 0 lỗi, 0 bỏ qua. Tổng hợp và TRX nằm trong `artifacts/hai-sql-20261125T132550Z/`.

**17/17 tiêu chí** có bằng chứng SQL. Hai vòng UAT ghi kết quả trong hồ sơ nghiệm thu staging.

| # | Tiêu chí | Trạng thái |
| --- | --- | --- |
| 1 | Unique identity, invoice and charge | PASS |
| 2 | Variant and option ownership | PASS |
| 3 | Concurrent seating open and transfer | PASS |
| 4 | QR expiry, revocation and session isolation | PASS |
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

Tiêu chí 4 kiểm tra API gửi món và theo dõi đợt của đúng khách, gửi lại cùng khóa, mã bàn dùng một mình, token hết hạn/thu hồi và khách gửi món đồng thời với đóng phiên. Bộ Seating kiểm thêm invitation một lần và token cũ sau khi bàn mở phiên mới.

Tiêu chí 6 kiểm remake đồng thời/idempotency, không thêm doanh thu, chặn giao khi pending, rollback Billing/Outbox và kiểm quyền. E2E thật bổ sung: `web/e2e-hai-live/incident-remake.spec.ts`; bằng chứng `artifacts/hai-live-demo/incident-remake-evidence.json`.

Danh sách test theo tiêu chí nằm trong `scripts/release/matrix.json`. Bằng chứng máy đọc gồm `summary.json` và hai file TRX của lượt kiểm thử trên.
