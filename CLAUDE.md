# Hướng dẫn cho Claude Code

**Bắt buộc:** trước khi làm bất cứ việc gì trong repo này, đọc
[docs/AGENT_PROGRESS.md](docs/AGENT_PROGRESS.md) và làm đúng giao thức ở mục 1 của file đó.

Tóm tắt:
1. Đọc sổ tiến độ (trạng thái, việc tiếp theo, câu hỏi mở), chạy `git status`.
2. Nhận việc: ghi `In Progress` kèm tên agent và nhánh vào bảng trạng thái (mục 5).
3. Làm trong phạm vi task và đúng module của người giao việc; tuân thủ quy tắc mục 8 (kiến trúc, git, bảo mật, phạm vi MVP).
4. Chạy kiểm tra (mục 9), ghi kết quả thật.
5. Trước khi kết thúc: cập nhật bảng trạng thái, thêm một mục nhật ký (mục 10), cập nhật việc tiếp theo và câu hỏi mở.

Không commit, push hay tạo PR nếu người giao việc chưa yêu cầu. Không đẩy thẳng vào `main` hoặc `develop`.
Không đánh dấu `Done` khi chưa kiểm chứng bằng lệnh hoặc test.

**Quy ước commit:** không thêm `Co-Authored-By` của Claude/AI và không thêm dòng "Generated with Claude Code" vào commit hay mô tả PR; tác giả chỉ là người giao việc.
