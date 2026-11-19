# Review ADR-01 — Hải: CI và vận hành

Ngày 18/11/2026. Đối chiếu ADR [#29](https://github.com/Trunghieu220504/restaurant-management-system/pull/29), implementation [#36](https://github.com/Trunghieu220504/restaurant-management-system/pull/36) đã merge (`c3b4e0d`) và mã tại `develop` `68cd462`. Đây là báo cáo review, không phải xác nhận staging hay approval thay người khác.

## Nhận xét

| Nội dung | Bằng chứng và giới hạn |
|---|---|
| CI SQL | [Workflow](../../.github/workflows/backend-ci.yml) dùng SQL Server tạm trên loopback, lọc `FullyQualifiedName~SqlServer` và tải TRX. [Run 44240180220](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44240180220) của PR #46, SHA `59e3bf8421fc669dc6a3ddb8ba4fd9eac1cbc9d0`: job SQL thành công, log tổng 50 đạt / 0 lỗi / 0 bỏ qua; build/test cũng thành công. Đã đọc [artifact TRX](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44240180220/artifacts/14399620425): cả 9 kết quả thuộc lớp `PhuongWeekOneSqlServerTests` đều Passed, 0 lỗi, 0 bỏ qua. Đây là kết quả của SHA nêu trên, không tự chứng minh mọi lần chạy sau. |
| Khóa và timeout | [BusinessDayLock](../../src/Shared/Restaurant.Shared.SqlServer/BusinessDayLock.cs) dùng transaction của caller, SQL có tham số, timeout 0–60000 ms; -1 phát `TimeoutException`, mã âm khác báo lỗi. Caller phải rollback và ánh xạ lỗi an toàn; helper chưa tự bảo đảm điều đó. [Test SQL](../../tests/Restaurant.IntegrationTests/BusinessDayLockSqlServerTests.cs) bao phủ khóa, rollback và cancellation. |
| Phạm vi Outbox | [IntegrationEvent và IOutboxWriter](../../src/Shared/Restaurant.Shared.Contracts/IntegrationEvent.cs) là hợp đồng/envelope; chưa có persistence, dispatcher/worker hay consumer runtime. Không suy ra enqueue đã publish hoặc cam kết exactly-once. |
| Cấu hình và migration | PR #36 mô tả thay đổi additive, không có migration/schema, endpoint hoặc cấu hình runtime mới; các project shared chưa được module runtime tiêu thụ. Không cần chạy lại migration vì review này. |
| Rollback | Với phạm vi additive chưa được tiêu thụ, quay về image API đã kiểm chứng trước đó; không suy ra có thể downgrade database khi release khác đã thay schema. Cần đánh giá tương thích schema và kế hoạch khôi phục riêng trước rollout. |
| Logging và secret | Helper không tự ghi log; chưa xác minh hành vi log ở consumer vì chưa có consumer runtime. Khi tích hợp cần log lỗi/timeout có correlation ID, không log payload, connection string, token hoặc dữ liệu nhạy cảm; rà soát secret và log trước phát hành. |

## Cần tiếp tục

1. Đã đọc ruleset `develop` id `26971142` ngày 18/11: active, include đúng `refs/heads/develop`, tối thiểu một approval, approval cho lần push cuối, giải quyết review thread và hai check bắt buộc `Build and test`, `Identity SQL Server tests`; cập nhật nhánh bắt buộc. Còn kiểm chứng PR CI đỏ thực tế bị chặn; không tắt ruleset.
2. Xác nhận mapping timeout/rollback và logging khi có consumer thực; thiết kế worker, retry, giám sát và tính nguyên tử Outbox thuộc task riêng.
3. Review độc lập báo cáo này. Chưa kết luận kiểm thử staging hay nghiệm thu toàn bộ ADR.
