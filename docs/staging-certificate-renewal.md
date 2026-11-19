# Gia hạn chứng chỉ staging đang được Nginx sử dụng

## Kết quả sau áp dụng ngày 18/11/2026

Quản trị viên đã chạy plan/apply thành công lúc 13:48 UTC. Output script xác nhận ACME staging dry-run PASS, chứng chỉ thật không đổi, hook Nginx test/reload đạt và ready=200. Agent kiểm tra độc lập lúc 13:49–13:52 UTC: loaded ExecStart dùng đúng config/work/log dirs của dự án, cert-name và hook; hook root:root mode 700, drop-in root:root mode 644; timer enabled/active. HTTP 301 đến HTTPS, root/live/ready đều 200 với TLS verify=0; workstation ngoài VPS thấy HSTS max-age=86400. Bằng chứng bàn giao tại [PR #52](https://github.com/Trunghieu220504/restaurant-management-system/pull/52).

INF06 PASS trong phạm vi thử gia hạn và cấu hình hiện tại. Chưa có kết luận lần renew production trong tương lai đã xảy ra. Backup/log riêng nằm trên VPS; không công khai key/log. [Issue #56](https://github.com/Trunghieu220504/restaurant-management-system/issues/56) còn theo dõi review/merge PR này.

**Không chạy lại plan/apply trên staging hiện đã sửa**: guard sẽ dừng khi có drop-in. Các bước cài bên dưới ghi lại quy trình một lần; để kiểm tra hiện trạng dùng các lệnh chỉ đọc:

```bash
systemctl show certbot.service -p ExecStart --value
systemctl is-enabled certbot.timer
systemctl is-active certbot.timer
systemctl list-timers certbot.timer --no-pager
curl --fail --silent --show-error https://staging.quanlynh.id.vn/health/ready
```

## Lỗi đã xác nhận ngày 18/11/2026

Nginx mount `infra/certbot/conf` của bản triển khai tại `/home/deploy/restaurant-management-system`, gồm một lineage `staging.quanlynh.id.vn`. Cấu hình renewal dùng webroot host `infra/certbot/www`, đúng với mount của Nginx. Tuy nhiên, unit Certbot được đóng gói chạy `certbot -q renew --no-random-sleep-on-renew` với config-dir mặc định `/etc/letsencrypt`; thư mục renewal mặc định không có lineage nào. Không có drop-in hoặc cấu hình CLI chuyển sang thư mục dự án. Vì vậy timer enabled và lần chạy exit 0 chưa chứng minh chứng chỉ đang phục vụ sẽ được gia hạn.

## Bản sửa có giới hạn

[Script](../scripts/restaurant-week1-fix-certbot.sh) chỉ dùng cho hostname `restaurant-staging`, root, với `--plan` hoặc `--apply`. Script dừng nếu phát hiện lineage khác, hook đã lưu, directory hooks, drop-in hoặc file đích có sẵn. Không mở rộng quyền sudo của `deploy`.

`--apply` lưu bản cấu hình renewal và unit vào backup root-only, chạy **ACME staging dry-run** cho đúng chứng chỉ, xác nhận chứng chỉ thật và cấu hình renewal không đổi. Lệnh dùng `--config /dev/null`, server staging chỉ cho dry-run, `--no-directory-hooks`; các saved hook đã bị kiểm tra và từ chối trước đó. Không dùng `--force-renewal`, không xin chứng chỉ thật ngay.

Chỉ sau dry-run đạt, script cài hook root-owned kiểm tra/reload Nginx trong cùng container và thử chính hook đó; health ready phải trả 200, ID/thời điểm bắt đầu container phải giữ nguyên. Sau đó cài drop-in cho `certbot.service` chỉ rõ config/work/log dirs của dự án, tên chứng chỉ và deploy hook. Timer hiện có vẫn enabled/active; không chạy unit renew thật ngay, không restart container hoặc VPS, không thao tác SQL. Dry-run ghi log riêng trên VPS; không đưa log/account key/private key vào Git hoặc chat.

Nếu lỗi, script gỡ các file mới, khôi phục cấu hình renewal đã lưu và daemon-reload; kết quả dry-run lỗi phải được xem tại VPS, không bỏ guard để áp dụng tiếp. Script không hỗ trợ chạy lại khi drop-in đã tồn tại: quản trị viên phải đối chiếu cấu hình hiện tại trước. Lần gia hạn thật trong tương lai vẫn cần theo dõi service/log và thời hạn chứng chỉ.

## Thực hiện trong SSH Linux

Sau khi agent tải script lên VPS, chỉ chạy tại prompt `deploy@restaurant-staging:~$`, **không chạy trong PowerShell Windows**:

```bash
sudo install -o root -g root -m 0700 /home/deploy/restaurant-week1-fix-certbot.sh /usr/local/sbin/restaurant-week1-fix-certbot
sudo /usr/local/sbin/restaurant-week1-fix-certbot --plan
```

Nếu plan thành công, thực hiện:

```bash
sudo /usr/local/sbin/restaurant-week1-fix-certbot --apply
```

Gửi phần tóm tắt thành công hoặc thông báo lỗi đã lọc; không gửi nội dung backup, cấu hình đầy đủ hoặc log có dữ liệu nhạy cảm. Sau apply cần kiểm tra lại HTTPS, health, timer và ExecStart. Không ghi INF06 PASS trước khi có dry-run, reload hook và lịch đúng được kiểm chứng.

Tham khảo [hướng dẫn chính thức Certbot](https://eff-certbot.readthedocs.io/en/stable/using.html#renewing-certificates): `renew --dry-run`, thư mục cấu hình và deploy hook.
