# Bằng chứng kiểm tra VPS chỉ đọc — 18/11/2026

Nguồn: Hải chạy các lệnh sudo và cung cấp kết quả trong phiên làm việc; agent kiểm tra SSH, trạng thái service và kết nối từ máy bên ngoài VPS. Không lưu IP máy khách, khóa, mật khẩu hoặc token. Đây là ảnh chụp trạng thái tại thời điểm kiểm tra, không thay thế review/approval.

- Đăng nhập bằng khóa thành công với user `deploy`; hostname `restaurant-staging`.
- Hải chạy `sudo sshd -T`: `permitrootlogin no`, `pubkeyauthentication yes`, `passwordauthentication no`, `kbdinteractiveauthentication no`. Đây là cấu hình tổng quát; `Match` theo ngữ cảnh kết nối chưa được đối chiếu riêng.
- `sudo ufw status`: active; các rule hiển thị cho TCP 22, 80, 443 trên IPv4 và IPv6.
- `sudo fail2ban-client status`: một jail `sshd`; service fail2ban active. Chưa kiểm tra thử chặn thực tế.
- `unattended-upgrades` enabled và active. Đọc `apt-config dump` sau đó thấy `APT::Periodic::Update-Package-Lists "0"` và `APT::Periodic::Unattended-Upgrade "0"`: cơ chế APT periodic tiêu chuẩn đang tắt. Allowed-Origins có các nguồn security nhưng chỉ có service active không chứng minh cập nhật tự động hoạt động. Chưa có bằng chứng cơ chế thay thế hoặc lịch sử cập nhật thành công; INF03 không đạt yêu cầu auto-update theo cấu hình này.
- `certbot.timer` enabled; timer đã xuất hiện trong danh sách lịch. `certbot.service` chạy `/usr/bin/certbot -q renew --no-random-sleep-on-renew`; lần chạy gần nhất trả `Result=success`, `ExecMainStatus=0`. Lệnh renew có thể kết thúc 0 khi chứng chỉ chưa đến hạn, nên chưa chứng minh đã gia hạn hoặc dry-run thành công. Chưa đọc cấu hình/hook renewal bằng quyền root.
- `sudo docker ps`: API, web, Nginx và SQL Server đang Up; chỉ Nginx publish 80/443 ra host; các cổng API 8080, web 3000 và SQL 1433 được liệt kê nội bộ. Trạng thái này không thay thế quét cổng ngoài.
- DNS A của staging tại thời điểm kiểm tra trùng địa chỉ VPS đã đăng nhập SSH; HTTPS trang chủ trả 200.
- Từ máy của agent ngoài VPS, thử TCP năm cổng: 22/80/443 kết nối được; 1433/8080 không kết nối được trong thời gian chờ. Không chứng minh không có cổng khác mở; chưa thử từ mạng 4G hoặc kiểm tra riêng IPv6.

Chưa hoàn tất quét cổng ngoài, xử lý cấu hình auto-update đang tắt, renewal thực tế và cấu hình SSH theo từng `Match`. Không thay đổi SSH, firewall, container hoặc database trong các bước này.

Một phép quét TCP toàn dải được khởi động sau đó nhưng đã dừng trước khi hoàn tất khi SSH timeout; không có kết quả toàn dải để nghiệm thu. Sau dừng, HTTPS root 200 và SSH kết nối lại thành công. Không suy ra nguyên nhân timeout là do scanner khi chưa có log/đối chiếu mạng.

Theo dõi cấu hình auto-update tại [issue #53](https://github.com/Trunghieu220504/restaurant-management-system/issues/53).

## Audit trực tiếp sau khi cấp quyền giới hạn

Đã chạy thành công bản root-owned `/usr/local/sbin/restaurant-week1-audit` qua `sudo -n`, không dùng mật khẩu trong công cụ:

- SSH tổng quát vẫn root/password/kbd-interactive=no, pubkey=yes; UFW active, chỉ có rule allow 22/80/443 trong output.
- Jail sshd ghi 23 lần ban tổng cộng, hiện tại 0 ban; không lưu địa chỉ máy khách.
- Hai cờ APT periodic vẫn 0; [PR #54](https://github.com/Trunghieu220504/restaurant-management-system/pull/54) chuẩn bị script sửa có backup/rollback. Preview APT ngoài `/etc` đã trả 1/1/false, nhưng chưa áp dụng thật hoặc đóng issue #53.
- API và web có OCI revision đúng `7de77d4cbd15deb474bab146ae6f14dd05937dbe`, không có host port bindings; SQL không có host port bindings. HTTP live/ready kiểm lại đều 200.
- API image id `sha256:06f2fc5738f57a1f49751816e21444b85ca394fd0f25cf17773120b416083318`; web `sha256:dc343676bf5d1edb45bb9692a1ea35efc5a7a63466f97c709a0c52aa36e5d60e`. Đây là image id runtime, không mặc định là registry manifest digest. Không thấy Docker health object trong output inspect, không suy ra healthcheck container đã được cấu hình.
- Không có biến `ReverseProxy__KnownProxies__*` trong environment API; Nginx runtime có địa chỉ nội bộ `172.18.0.3` và đã đặt X-Forwarded-For thành remote_addr, X-Forwarded-Proto thành scheme. Đối chiếu thêm nhãn Compose/mount trước khi thay runtime; không tin toàn bộ proxy hoặc mở API ra host.
- Timer certbot enabled nhưng audit không tìm thấy renewal `.conf` trong `/etc/letsencrypt/renewal`. Cần đối chiếu mount của Nginx và config-dir/cli.ini thực tế trước khi kết luận cơ chế renewal đang quản lý đúng chứng chỉ hoặc chạy dry-run.

Quyền được cấp chỉ chạy audit cố định; không cho phép sửa APT/proxy, deploy hoặc ghi SQL. Các thay đổi hệ thống vẫn cần quản trị viên thực hiện đúng script/lệnh đã chuẩn bị.

## Sau khi quản trị viên áp dụng cấu hình APT

Quản trị viên chạy thành công script PR #54 lúc 13:04 UTC, backup root-only tại `/var/backups/restaurant-week1-security-updates/20261118T130404Z-193006`. Agent kiểm tra độc lập qua SSH và audit, gần nhất 13:36 UTC:

- `APT::Periodic::Update-Package-Lists "1"`, `APT::Periodic::Unattended-Upgrade "1"`.
- `Unattended-Upgrade::Automatic-Reboot "false"`; apt-daily.timer và apt-daily-upgrade.timer enabled/active.
- Không ép upgrade hoặc reboot; không kết luận bản vá đã được cài. Kết quả này thay thế trạng thái APT 0/0 ở các lần kiểm tra trước, không xóa lịch sử lỗi.
- INF03 đạt trong phạm vi cấu hình vận hành. [Issue #53](https://github.com/Trunghieu220504/restaurant-management-system/issues/53) vẫn theo dõi approval/merge [PR #54](https://github.com/Trunghieu220504/restaurant-management-system/pull/54).

## Đối chiếu Compose và chứng chỉ đang phục vụ

Quản trị viên cung cấp Docker labels/mounts; agent đọc file không bí mật để kiểm tra bổ sung:

- Working directory thực tế là `/home/deploy/restaurant-management-system/infra`. API dùng backend, backend.https, frontend của working directory và hai GHCR overrides của release `hai-r8-release-7de77d4cbd15`; API không có mounts. Không dùng nhầm topology mới trong repo để đổi mạng đang chạy.
- Nginx mount read-only `infra/certbot/conf` vào `/etc/letsencrypt`, `infra/certbot/www` vào `/var/www/certbot`, và `infra/nginx/backend.https.conf` vào config Nginx.
- Certbot 2.9.0; native unit không có drop-in và không chỉ rõ config-dir. Thư mục `/etc/letsencrypt/renewal` có 0 file `.conf`; thư mục project `infra/certbot/conf/renewal` có đúng 1 file cho staging. Không có override config-dir trong CLI ini được kiểm tra.
- Renewal config của project dùng webroot, map staging đến đúng `infra/certbot/www` trên host; không thấy saved hook hoặc directory hook. Vì vậy timer hiện tại không quản lý lineage Nginx đang phục vụ. Exit 0 của timer không phải bằng chứng renewal đạt; INF06 FAIL phần này, theo [issue #56](https://github.com/Trunghieu220504/restaurant-management-system/issues/56).
- [PR #55](https://github.com/Trunghieu220504/restaurant-management-system/pull/55) sửa đúng trusted proxy; preview Compose chỉ thêm một biến môi trường, cú pháp đạt, CI đạt. Script đã tải nhưng chưa root apply/retest.
- [PR #57](https://github.com/Trunghieu220504/restaurant-management-system/pull/57) chuẩn bị sửa lịch Certbot, có ACME staging dry-run trước khi đổi scheduler và hook Nginx test/reload. Bash syntax đạt cục bộ/VPS, SHA256 bản tải trùng nguồn. Quyền deploy không đọc được file certificate trong lineage; kiểm tra certificate và dry-run bằng root còn chờ, không cố đọc private key.

Chưa thay runtime proxy hoặc scheduler Certbot trong các bước kiểm tra này; không ghi PASS trước apply và retest.

## Sau áp dụng proxy và lịch gia hạn — kiểm tra 13:49–13:52 UTC

Các kết quả sau thay thế trạng thái FAIL/chờ áp dụng của proxy và Certbot phía trên; giữ nguyên lịch sử điều tra:

- Quản trị viên chạy plan/apply Certbot thành công, backup root-only tại `/var/backups/restaurant-week1-certbot/20261118T134845Z-195360`. Output script xác nhận ACME staging dry-run đạt, fingerprint chứng chỉ thật không đổi, hook Nginx test/reload đạt và ready=200. Đây là kết quả quản trị viên cung cấp, không đọc log riêng hoặc private key qua tài khoản deploy.
- Agent kiểm tra độc lập `systemctl show certbot.service -p ExecStart`: lệnh đã nạp dùng config/work/log dirs dưới thư mục dự án, đúng cert-name staging và deploy hook `/usr/local/sbin/restaurant-staging-reload-certificate`; không còn chạy vào default renewal directory rỗng.
- Hook là root:root mode 700, drop-in là root:root mode 644. Timer enabled/active; lịch kế tiếp hiển thị 19/11/2026 08:42:12 +07 tại thời điểm kiểm tra. Không ép renew thật ngay và không suy ra lần chạy production trong tương lai đã thành công.
- HTTP root trả 301 đến đúng HTTPS. HTTPS root/live/ready đều 200, TLS verify=0. Từ workstation ngoài VPS, HTTPS root 200 và `Strict-Transport-Security: max-age=86400`. INF06 PASS theo phạm vi thử gia hạn/configuration và kết nối hiện tại.
- Audit thấy API có đúng `ReverseProxy__KnownProxies__0=172.18.0.3`, trùng Nginx; override trên VPS chỉ thêm biến này. Nginx vẫn overwrite XFF/Proto. [Retest hai nguồn và spoofed XFF](rate-limit-concurrent-2026-11-18.md#kiểm-lại-sau-sửa--pass) đạt.
- API image ID `sha256:06f2fc5738f57a1f49751816e21444b85ca394fd0f25cf17773120b416083318`, web `sha256:dc343676bf5d1edb45bb9692a1ea35efc5a7a63466f97c709a0c52aa36e5d60e` giữ nguyên; revision vẫn `7de77d4cbd15deb474bab146ae6f14dd05937dbe`. SQL image ID vẫn `sha256:cf8f0ab2ebe61feec2eed2629061a07d247f791c2a8ddda74a4ac9f06a291718`; cả ba đều running, host port bindings rỗng. Không có bằng chứng độc lập so sánh container ID/StartedAt trước/sau ngoài các guard của script sửa proxy; không khẳng định quá mức rằng đã tự kiểm toàn bộ các giá trị đó.

Các issue #51/#56 đã có runtime đạt; vẫn cần review/merge PR #55/#57. Quét toàn dải/4G, SSH Match theo ngữ cảnh và E2E có dữ liệu/tài khoản test còn chưa đủ bằng chứng.
