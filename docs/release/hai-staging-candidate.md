# HAI-2.5 — gói cập nhật frontend/backend thật trên staging

Đích: `https://staging.quanlynh.id.vn` trên VPS `103.69.97.224`. Đây là gói ứng dụng dùng database staging hiện có. Không chứa seed, mật khẩu, database demo hoặc lệnh reset dữ liệu.

## Trạng thái và điều kiện

Ngày 25/11/2026, SSH bằng khóa staging hoạt động. Helper audit xác nhận API/web vẫn ở `8af7ab3`, SQL không publish port, Nginx giữ cổng 80/443. User deploy chưa được sudo không mật khẩu cho release mới; không thể tự thay helper root-owned bằng các quyền hiện tại.

Gói candidate dùng image build từ cùng SHA với source, pin immutable image ID, có nhãn revision, migration của 9 module (Reporting không có migration) và SHA256SUMS. Chuyển image qua SSH để chuẩn bị bản triển khai không cần token GHCR; việc này chưa hoàn tất yêu cầu Jenkins tự publish/deploy từ develop. Luồng release của nhóm vẫn cần review/merge vào develop trước khi phát hành staging thường lệ; không tự merge PR hoặc thay branch protection.

## Cài helper (operator có sudo)

Gói nằm ở `/home/deploy/restaurant-release-<40-hex-sha>/`. Lệnh chính xác và checksum được ghi vào `OPERATOR.txt` của gói. Operator kiểm tra source/manifest trước khi chạy installer bằng sudo. Installer nhận đúng một SHA256 của manifest; sao chép vào thư mục root-private rồi kiểm checksum trước khi nạp image hoặc thực thi helper. Các file cấu hình đang chạy được sao chép từ snapshot root-owned `/opt/restaurant/releases/8af7ab3/compose`; không đưa secret vào gói hoặc Git.

Installer chỉ chạy preflight và cấp một wrapper cố định theo SHA, không restart dịch vụ hoặc tự deploy. Wrapper chỉ nhận zero argument. Không cấp Docker group, shell sudo hoặc wildcard cho deploy.

Sau cài, có thể gọi `sudo -n /usr/local/sbin/restaurant-deploy-approved-<40-hex-sha>` cho đúng bản đã duyệt.

## Khi apply

1. Kiểm checksum, image labels, cấu hình Compose, trạng thái container/proxy, nginx -t và dung lượng.
2. Backup SQL COPY_ONLY/CHECKSUM, VERIFYONLY, chép bản backup vào thư mục root-private, lưu SHA256.
3. Restore vào database scratch tên sinh riêng; áp dụng toàn bộ migration hai lượt, CHECKDB; chạy API cũ và API mới kiểm readiness trên bản copy. Outbox bị tắt trong probe để không phát sinh xử lý nền.
4. Dừng writer API/web trước khi áp dụng migration vào database staging. Không recreate SQL, không đổi volume SQL, không chạy seed/reset/admin bootstrap.
5. Thay API/web bằng image bất biến; bật Outbox; recreate Nginx với config đã kiểm, chuyển `/api`, `/hubs` và frontend tới dịch vụ thật. Đọc IP Nginx thực tế và cấu hình API chỉ tin IP đó; không nới dải trusted proxy.
6. Kiểm readiness công khai, frontend, revision, SQL container ID và proxy. Nếu lỗi sau khi dừng writer, khôi phục image cũ/config Nginx và proxy, kiểm readiness; lần deploy vẫn FAILED. Không tự downgrade schema hoặc restore đè database.

Backup local và probe readiness không thay offsite R2 hoặc UAT nghiệp vụ trên VPS. Chưa ghi deployment PASS nếu helper chưa apply thành công; cần kiểm browser đăng nhập/gọi món/bếp/thu tiền/WebSocket trên HTTPS sau deploy bằng tài khoản staging riêng.

## Kiểm chứng công cụ

- Bash syntax/ShellCheck của installer và helper.
- Sáu ca trong `scripts/release/staging_candidate_checks.py`: plan không dừng writer; thứ tự backup/migration/switch; lỗi migration, lỗi start, lỗi readiness đều rollback và FAILED; checksum sai bị chặn trước backup.
- Các ca chạy trong container Linux riêng, Docker/curl được stub, không mount Docker socket. Đây là kiểm thử control flow; không coi là đã diễn tập rollback trên VPS.
- Bộ test thông thường: `python -m unittest discover -s scripts/release -p 'test_*.py'`.

Tài liệu tiến độ/matrix ghi riêng bằng chứng SQL và E2E ứng dụng. Tiêu chí public guest ordering vẫn BLOCKED, không bật gửi món QR khi backend chưa có.
