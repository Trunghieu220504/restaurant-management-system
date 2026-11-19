# Kiểm tra GitHub tuần 1 — Hải, 18/11/2026

Nguồn: GitHub REST API qua tài khoản Hai249 đã xác minh; không lưu credential hoặc header xác thực. Repo: `Trunghieu220504/restaurant-management-system`, develop tại `68cd4625d8475be9ebba0d590b4056903e411330` khi bắt đầu đối chiếu.

## Ruleset hiện tại

[Ruleset develop 26971142](https://github.com/Trunghieu220504/restaurant-management-system/rules/26971142):

- Enforcement `active`, include `refs/heads/develop`; tình trạng include rỗng đã được sửa.
- Pull request bắt buộc, ít nhất 1 approval, yêu cầu approval cho lần push cuối và giải quyết review threads.
- Required checks: `Build and test` và `Identity SQL Server tests`, cùng integration id `15368` (GitHub Actions).
- `strict_required_status_checks_policy=true`: nhánh phải cập nhật trước merge.
- Có rule chặn xóa nhánh. Không thấy rule `non_fast_forward` riêng trong response; chủ repo đối chiếu yêu cầu chặn force push trong CONTRIBUTING.
- `current_user_can_bypass=never` cho Hai249.
- Tài khoản Hai249 có quyền push/pull/triage, không có admin/maintain. Không thể sửa ruleset thay chủ repo bằng quyền hiện tại.

## SQL CI và C03

[Run 44240180220](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44240180220), PR #46, source SHA `59e3bf8421fc669dc6a3ddb8ba4fd9eac1cbc9d0`: hai job bắt buộc thành công. Log SQL: 50 passed, 0 failed, 0 skipped. Đã tải và đọc [TRX artifact 14399620425](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44240180220/artifacts/14399620425), đối chiếu TestDefinitions với UnitTestResult: 9 kết quả lớp `PhuongWeekOneSqlServerTests` đều Passed, không skip.

PR #47 đã bổ sung bằng chứng 20 lượt lặp cục bộ, mỗi lượt 9/9, không skip. Kết quả CI nêu trên và kết quả lặp có phạm vi riêng; không suy ra luồng chốt ngày nghiệp vụ chưa triển khai đã đạt.

## PR required check đỏ bị chặn

[PR thử #50](https://github.com/Trunghieu220504/restaurant-management-system/pull/50), source SHA `114414d5f2454fd4ca7ae857a384abe741e3e13c`, chỉ thêm `#error` trong architecture test project; không thay module runtime. [Build and test](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44283921393/job/134731527446) kết thúc `failure`, log có CS1029 đúng directive probe và exit 1. API PR trong khi đang mở trả `mergeable=true`, `mergeable_state=blocked`, `draft=false`; ruleset hiện hành yêu cầu check này. Không gửi yêu cầu merge hoặc bypass. Missing review cũng là gate độc lập, nên không tuyên bố đã cô lập nguyên nhân CI duy nhất.

Đã đóng PR thử lúc `2026-11-18T10:20:38Z`, `merged=false`. File lỗi chỉ nằm trên nhánh thử, không có trên develop hoặc nhánh bàn giao.

## CI frontend và review ADR

[PR #41](https://github.com/Trunghieu220504/restaurant-management-system/pull/41), source SHA `a69bdf5f4b208bf72e8410959e0e1e4ccf775995`, đã merge; `Web Build and Test` và `Verify API Client Sync` đều success tại [Web CI run 44037652407](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44037652407). Đây là CI, không thay browser E2E staging.

[PR #49](https://github.com/Trunghieu220504/restaurant-management-system/pull/49) review ADR của Hải đã mở, source SHA `050426aaebc5184f08c391d08e3997b431be381f`; cả hai required backend checks success ở [run 44283313758](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44283313758). Đã yêu cầu Trung Hiếu review; chưa có approval/merge lúc ghi nhận.

## Còn phối hợp

- Approval/review độc lập các PR tài liệu và xác nhận chiến lược test của nhóm.
