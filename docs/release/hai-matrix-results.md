# Ma trận kiểm chứng SQL của Hải

Chạy SQL Server 2022 cục bộ: full suite 397 integration + 5 architecture đạt, sau đó thêm 3 test SQL và chạy riêng 3/3 đạt. Tổng bằng chứng 405 test, không skipped; chưa coi đây là một lần full suite 405 cho đến khi CI chạy commit cuối. PASS của dòng chỉ áp dụng assertions SQL, không thay UAT UI/staging. SKIP nghĩa coverage chưa đủ, không phải test runner bỏ qua.

| # | Tiêu chí | Chủ | Kết quả SQL/coverage |
| --- | --- | --- | --- |
| 1 | Unique identity, invoice and charge | Hiếu / Phương | PASS |
| 2 | Variant and option ownership | Phương / Hiếu | PASS |
| 3 | Concurrent seating open and transfer | Hiếu | PASS |
| 4 | QR expiry, revocation and session isolation | Hiếu | SKIP |
| 5 | Confirmation produces one charge and first ticket | Hiếu / Phương | SKIP |
| 6 | Approved remake without new revenue | Hiếu | SKIP |
| 7 | Cannot serve twice or after cancellation | Hiếu | SKIP |
| 8 | Concurrent collectors never overcollect | Phương | PASS |
| 9 | Refund reservation under concurrency and unknown payout | Phương | PASS |
| 10 | Prior-day refunds preserve prior snapshots | Phương | PASS |
| 11 | Purchase snapshots survive unit conversion edits | Phương | PASS |
| 12 | Archived stock remains countable and blocks structural changes | Phương | PASS |
| 13 | Residual stock value and signed adjustments | Phương | PASS |
| 14 | Atomic concurrent closing and late writers | Phương | PASS |
| 15 | Closing rollback leaves no partial state | Phương | PASS |
| 16 | Negative profit and distinct units | Phương | PASS |
| 17 | Permission revocation affects existing access token | Hiếu | PASS |

13/17 dòng đủ bằng chứng; dòng 4–7 còn thiếu public ordering/Fulfillment/incident/serve. Nguồn: artifacts/hai-week7-verified/*.trx và artifacts/hai-week7-matrix-additions/*.trx. Mapping cụ thể: scripts/release/matrix.json. Toàn bộ matrix vẫn BLOCKED.
