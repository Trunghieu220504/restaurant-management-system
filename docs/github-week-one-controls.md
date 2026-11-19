# Đối chiếu bảo vệ nhánh tuần 1 — 19/11/2026

Hai ruleset đang active và áp dụng đúng `refs/heads/develop` / `refs/heads/main`. API effective rules xác nhận cả hai có pull request, một approval, yêu cầu approval sau push cuối, giải quyết review thread và hai required checks (`Build and test`, `Identity SQL Server tests`) với strict up-to-date. Ruleset main có chặn non-fast-forward; develop chưa có rule này.

Merge methods theo ruleset hiện tại đều cho phép merge/squash/rebase, trong khi [quy trình repository](../CONTRIBUTING.md) yêu cầu develop squash và main merge commit. Không coi đây là bằng chứng mọi force-push chắc chắn được phép: rule PR cũng hạn chế cập nhật; chưa thử force-push vào nhánh thật.

## Đề xuất đã chuẩn bị, chưa áp dụng

| Nhánh | Thay đổi | JSON cho chủ repo review |
|---|---|---|
| develop | Thêm `non_fast_forward`; chỉ cho squash merge | [develop ruleset](ops/develop-week1-ruleset.json) |
| main | Giữ chặn non-fast-forward; chỉ cho merge commit | [main ruleset](ops/main-week1-ruleset.json) |

Các JSON giữ nguyên conditions, required checks và approval đã đọc từ API. Phản hồi cho Hai249 không có trường `bypass_actors`; không thể kết luận danh sách bypass rỗng. Vì vậy đề xuất **bỏ hẳn trường này**, không tạo danh sách rỗng thay cấu hình ẩn. Đây là tài liệu đối chiếu, không phải payload PUT hoàn chỉnh để áp dụng trực tiếp. Chủ repo cần lấy cấu hình mới nhất bằng tài khoản admin, giữ nguyên bypass actors và các cài đặt khác, rồi chỉ sửa những mục trong bảng; có thể thực hiện ngay trong giao diện Rulesets.

Tài khoản kết nối Hai249 có push nhưng không có admin. Agent chưa PUT cấu hình và không thể tự cấp quyền quản trị. Không cần cấp quyền rộng chỉ để làm bước này: chủ repo có thể vào Settings → Rules → Rulesets, chọn đúng hai ruleset và sửa các mục trong bảng, giữ nguyên phần còn lại. Sau đó agent đọc API để kiểm chứng.

PR thử CI đỏ #50 đã đóng, không merge; bằng chứng lịch sử ở [GitHub controls](evidence/week-one/github-controls-2026-11-18.md). PR #52 có review APPROVED lúc 14:37:44 UTC ngày 18/11 và đã merge. #54/#55 có approval độc lập của Trung Hiếu, đã squash-merge và đóng issue #53/#51. Thành viên đã merge #49 vào `a33acbf`. Kiểm tra ngày 19/11 lúc 03:35 UTC: #57 và #69 có validation checks xanh, không xung đột, nhưng GitHub trả `REVIEW_REQUIRED`; #57 cần approval sau push cuối, #69 chưa có approval. #68 chưa có approval và còn behind tại thời điểm kiểm tra; nhánh được cập nhật từ develop trong bản sửa này, cần CI trên head mới rồi review. Cả ba không có review thread chưa giải quyết. Người bấm merge phải có quyền write; approval sau push cuối phải đến từ người khác người thực hiện push đó. Không tự approve PR của mình, không tự merge hoặc bật auto-merge. [Báo cáo hiện tại](evidence/week-one/automation-2026-11-19.md) gắn từng kết quả với đúng môi trường/commit; không dùng tài khoản người khác hoặc bypass ruleset để hoàn tất.
