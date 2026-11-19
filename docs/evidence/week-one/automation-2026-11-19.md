# Thống kê kiểm chứng tuần 1 — Hải, 19/11/2026

Phạm vi bàn giao tuần 1, kèm kiểm tra tương thích với develop mới trong lúc làm. Các số dưới đây là số lần chạy theo từng nhóm, không cộng thành số test duy nhất. Đợt browser cục bộ đầu dùng `df6160c7376297a411b3f9697e490472f0a83a3c`; đợt SQL CI sau sửa lỗi dùng develop `3f70da2` + commit `233ee23` của PR #69. Không deploy các code mới này lên staging R8.

## Kết quả đã đạt

| Nhóm | Kết quả | Bằng chứng và giới hạn |
|---|---|---|
| Backend cục bộ, SQL 2022 Developer 16.0.1000.6 | **227/227 PASS, 0 skip** | 5 architecture + 222 integration; TRX của đợt `automation-20261118T185221Z`. Dùng test DB tự sinh, không trỏ staging |
| Backend SQL 2022 Docker sau sửa lỗi Catalog | **243/243 PASS, 0 skip** | [PR #69](https://github.com/Trunghieu220504/restaurant-management-system/pull/69), [run 44390272988](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44390272988), artifact `14467986199`: 5 architecture + 238 integration. Đã tải và đọc cả hai TRX, không chỉ dựa checks xanh |
| Hồi quy Catalog sau sửa lỗi | **20 × 2 = 40/40 PASS, 0 skip** | Cùng artifact trên có đúng 20 TRX lặp, mỗi file đúng hai ca: concurrent same-code và deadlock thật. Kiểm tra conflict, tracker trống và lần ghi tiếp theo không phát lại bản ghi thất bại |
| Frontend unit mới nhất | **71/71 PASS** | Vitest, 12 file trên develop `3f70da2`; đợt `03:24:42` giờ Việt Nam ngày 19/11. Đợt trước trên `df6160c` đạt 48/48; không gộp hai lượt hoặc thay thế staging |
| Frontend build / client sync | **PASS** | Next production build và gen:api:check; không sửa logic frontend hoặc phát hành image |
| Browser/API cục bộ thật | **7/7 PASS** | W1-E01…E06 + lockout; API loopback/Kestrel, UserManager, SQL thật; không mock route hoặc đổi clock. Đợt chạy 18:52:21–19:10:42 UTC ngày 18/11 |
| Browser staging không đăng nhập | **6/6 PASS** | 14:34:06–14:34:14 UTC ngày 18/11: refresh thật không cookie=401 (cờ build hoạt động), redirect hai route bảo vệ, hai API=401, HTTPS/HSTS. Không dùng credential hoặc ghi SalesDay |
| TCP IPv4 từ ngoài VPS | **65535/65535 probe hoàn tất, PASS** | [Run 44327552913](https://github.com/Hai249/restaurant-management-system/actions/runs/44327552913), artifact `14438623911`; đã tải/đọc JSON: chỉ 22/80/443 mở, health trước/sau đạt, 14:44:39–15:10:28 UTC ngày 18/11. Không phải UDP/IPv6 hoặc nguồn 4G |
| Spike SQL 2022 Docker cô lập | **20/20 lượt, mỗi lượt 9/9 PASS, 0 skip; tổng 180** | [Run 44378527302](https://github.com/Hai249/restaurant-management-system/actions/runs/44378527302), artifact `14461883728`. Agent tải zip và độc lập đọc đúng 20 TRX, kiểm tra cả tên lớp và counters. SQL 16.0.4325.4; không chỉ dựa dấu xanh job |
| Chính script spike được bàn giao | **20 × 9 = 180/180 PASS, 0 skip** | [CI PR #68, run 44386503780](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44386503780), artifact `14465463039`. Đã tải/đọc 20 TRX, kiểm tra tên lớp và đối chiếu summary; commit `965600d` |
| Guard kết nối của script | **8/8 PASS** | Cho loopback, chặn remote/app database/alias xung đột/attach file/failover/user instance; không mở kết nối trong test guard |
| Source checks | **PASS** | Bash/PowerShell/Node parse, diagnostics file mới và Prettier toàn frontend. Lint mới nhất trên `3f70da2`: 0 error, 25 warning; hai script browser mới lint 0 warning. Đợt trên `df6160c` có 41 warning; không ghi “0 warning” cho toàn frontend |

Đợt 14:36–14:53 UTC trước đó cũng đạt 121 backend + 48 frontend + 7 browser, nhưng SQL mặc định khi ấy là 11.0.6020.0. Đợt này được giữ làm lịch sử, **không** dùng làm bằng chứng SQL 2022. Sau phát hiện, script chọn instance SQL 2022 đang chạy mà không đổi cấu hình dịch vụ. Các lượt lặp trên instance Windows RTM từng timeout ở CREATE DATABASE trong fixture; không bỏ assertion hoặc gọi đây là lỗi nghiệp vụ đã tái hiện. Kết luận lặp 20 lần ở bảng lấy từ Docker SQL 2022 cô lập trên CI.

Các lỗi harness đã xử lý: content root khi chạy C# file-based, Chromium thiếu, phản hồi 200 rỗng hợp lệ của current day, exit code tiến trình PowerShell, stderr native command, chọn đúng SQL 2022, parser connection-string không nhầm property với dictionary key và định dạng Prettier hai script mới. Hạn chế tải fixture/command timeout chỉ dùng cho test cục bộ; thời gian khóa và các Task.WhenAll bên trong test được giữ nguyên.

Lần kiểm tra mở rộng `automation-20261118T200340Z` trên develop `866e533` **FAIL**: 234/237 integration đạt, một lỗi Catalog deadlock và hai timeout CREATE DATABASE trong fixture trên SQL Windows RTM; architecture 5/5 đạt. Không đổi nhãn đợt này thành PASS. Lỗi Catalog có bản sửa riêng [PR #69](https://github.com/Trunghieu220504/restaurant-management-system/pull/69): EF bọc SQL 1205 trong InvalidOperationException nên catch cũ bỏ sót. Test mới tạo deadlock thật trong database tự sinh; trước sửa, [CI 44389437617](https://github.com/Hai249/restaurant-management-system/actions/runs/44389437617) có đúng ca đó FAIL với wrapped deadlock, không timeout (đã đọc TRX artifact `14467540398`). Sau sửa, suite đầy đủ và 20 lượt hồi quy đạt như bảng. Không tắt test hoặc thêm retry để che lỗi. Timeout provisioning Windows vẫn là giới hạn môi trường cục bộ; bằng chứng đầy đủ sau sửa lấy từ SQL 2022 Docker cô lập.

## Việc còn cần hoàn tất

| Mục | Trạng thái thực tế | Bước tiếp theo |
|---|---|---|
| E2E staging có Manager + khóa tài khoản | **PARTIAL** — hai tài khoản riêng Manager/Staff đã tạo và xác minh API; browser chỉ đọc 4/4 PASS trên R8. Chưa chạy lockout/đổi mật khẩu bằng tài khoản dùng một lần hoặc ghi ngày bán | Giữ ba tài khoản E2E riêng và chính sách dữ liệu/ngày thử trong [runbook tự động hóa](../../week-one-automation.md); không khóa hoặc reset hai tài khoản bàn giao cho người dùng |
| SSH Match theo kết nối thật | **PARTIAL** — cấu hình tổng quát đạt | Plan script chuẩn bị E2E kiểm tra deploy/root với đúng SSH_CONNECTION; cần output thực tế trước khi đổi PASS |
| Bảo vệ develop / merge methods | **Đề xuất sẵn, chưa áp dụng** | Chủ repo áp dụng [hai điều chỉnh cụ thể](../../github-week-one-controls.md). Tài khoản Hai249 không có admin |
| Review/merge | **#52, #54 và #55 đã APPROVED và merged**. #54 squash thành `2c4b412`, #55 thành `149717a`; issue #53/#51 đã đóng bằng bằng chứng. Thành viên merge #49; Trung Hiếu merge #57, #68 và #69 ngày 19/11, develop hiện `95ab29b` | #71/#72/#73 được cập nhật develop mới; cập nhật bằng chứng sau merge #68 bàn giao trong #73. Agent không merge, auto-merge, đóng PR hoặc bypass ruleset |
| Sửa lỗi Catalog phát hiện khi kiểm tra tương thích | **Đã vào develop qua #57/#69**, build/test/publish `95ab29b` PASS; chưa deploy staging | [CI 44462379960](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44462379960). #73 ghim release mới này; không triển khai bản `a33acbf` thiếu sửa lỗi. PR #68 cung cấp harness, không thêm thay đổi nghiệp vụ |
| Kết luận spike và chiến lược nhóm | **Đã có bằng chứng kỹ thuật và review/merge tài liệu #52** | Không tự ký thay Phương hoặc tạo approval thay người khác; xem [review C03 hỗ trợ Hải](../../reviews/c03-hai-2026-11-19.md) |
| Nguồn 4G / IPv6 nếu áp dụng tiêu chí INF02 đầy đủ | **Chưa có bằng chứng** | Kết quả hiện tại là external TCP IPv4, không sửa nhãn nguồn thành 4G |

INF04/INF09 và các dòng PARTIAL khác trong [ma trận](../../test-strategy.md) vẫn giữ giới hạn theo bằng chứng. Không recreate SQL, bootstrap Admin hoặc khôi phục đè staging chỉ để đổi trạng thái. Chiến lược test/runbook đã được review và merge trong #52; không suy ra mọi ca tuần 1 đều PASS.

CI #57 sau cập nhật develop: [run 44394568830](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44394568830), TRX artifact `14468759389`, 86/87 PASS, đúng ca concurrent same-code của Catalog FAIL với wrapped deadlock, không timeout (đã tải và đọc TRX). Đây là cùng nguyên nhân đã được PR #69 sửa; không phải kết luận script certbot hoặc dry-run runtime thất bại. #57 đã được cập nhật cùng bản sửa và Trung Hiếu merge lúc 04:52:54 UTC ngày 19/11. Mọi kết quả PASS trong bảng vẫn gắn với commit/môi trường đã ghi; không thay nhãn lần CI đỏ này bằng kết quả lần trước.

Lúc 04:56 UTC ngày 19/11, helper được cấp sudo đã tạo đúng `staging.manager`/Manager và `staging.staff`/Staff trong một transaction, sau backup COPY_ONLY/CHECKSUM và VERIFYONLY. Bằng chứng riêng trên VPS: `/var/backups/restaurant-staging-accounts/20261119T045630Z-219825`. Cả hai pass đăng nhập, đúng quyền/vai trò, refresh cookie bảo mật, refresh, logout và token đã thu hồi trả 401. Chạy đúng hai ca chỉ đọc trong runner staging của Duy với từng tài khoản đạt 2/2 mỗi lần; tổng 4/4, không skip trong các ca đã chọn. Không chọn ca ghi ngày bán, không coi đó là PASS. Những kiểm tra này trên ứng dụng R8 đang chạy; cần chạy lại sau deploy mới. Không lưu mật khẩu/hash/token/cookie hoặc connection string trong báo cáo này.

## Script và thực hiện

[Hướng dẫn chạy và quyền tối thiểu](../../week-one-automation.md) mô tả hai mode Local/Staging, cách tạo tài khoản có backup/cleanup, scanner giới hạn mục tiêu và báo cáo JSON đã lọc. File session/password/hash không thuộc deliverable hoặc Git. Các bản chụp runbook đính kèm của người dùng không bị chỉnh sửa.
