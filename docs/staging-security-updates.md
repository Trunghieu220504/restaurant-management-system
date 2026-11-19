# Bật lại tự cập nhật bảo mật trên staging

Theo dõi [issue #53](https://github.com/Trunghieu220504/restaurant-management-system/issues/53). Audit 18/11/2026 xác nhận service unattended-upgrades enabled/active nhưng hai giá trị APT periodic bằng 0; service active không đủ chứng minh cập nhật tự động đang chạy.

[Script sửa có phạm vi cố định](../scripts/restaurant-week1-enable-security-updates.sh) chỉ chạy trên hostname `restaurant-staging`, bằng root và không đối số. Thao tác:

1. Khóa chống chạy trùng, sao lưu override hiện tại (nếu có) và trạng thái hai timer vào thư mục root-only dưới `/var/backups/restaurant-week1-security-updates/`.
2. Kiểm tra cú pháp cấu hình preview và bảo đảm Allowed-Origins có nguồn security hiện hữu.
3. Cài `/etc/apt/apt.conf.d/99zz-restaurant-security-updates` với `Update-Package-Lists=1`, `Unattended-Upgrade=1`, `Automatic-Reboot=false`; không thay danh sách nguồn cho phép.
4. Bật và khởi động hai timer APT. Việc cập nhật diễn ra theo lịch; nếu timer đã đến hạn, hệ thống có thể kích hoạt công việc theo lịch ngay. Script không gọi apt upgrade hoặc reboot trực tiếp.
5. Xác minh cấu hình hiệu lực, timer enabled/active và in đường dẫn backup. Nếu lỗi sau khi bắt đầu áp dụng, tự phục hồi override và hoàn tác enable/active của timer khi đã thay đổi.

Script không đổi Docker, SQL, SSH, firewall hoặc chứng chỉ; không cấp quyền sudo mới.

## Thực hiện

Quản trị viên kiểm tra nội dung script, tải đúng bản lên `/home/deploy/restaurant-week1-enable-security-updates.sh`, rồi chạy từng lệnh **trong phiên SSH Linux**, khi dấu nhắc là `deploy@restaurant-staging:~$`:

```bash
sudo install -o root -g root -m 0700 /home/deploy/restaurant-week1-enable-security-updates.sh /usr/local/sbin/restaurant-week1-enable-security-updates
sudo /usr/local/sbin/restaurant-week1-enable-security-updates
```

Không dán lệnh Linux vào PowerShell Windows. Không chia sẻ mật khẩu sudo. Sau chạy, agent dùng script audit chỉ đọc để xác minh 1/1/false và đối chiếu timer. Lưu kết quả thực tế trước khi đổi INF03 sang PASS; không suy ra các bản vá đã được cài chỉ từ cấu hình đúng.

Nếu cần quay lại sau khi đã áp dụng thành công, quản trị viên dùng đúng backup được in: phục hồi `previous.conf` nếu tồn tại, hoặc chỉ bỏ override do script này tạo nếu trước đó chưa có; đối chiếu các file trạng thái timer để hoàn tác enable/active. Không khôi phục toàn bộ thư mục APT từ backup của thao tác này.

## Kiểm chứng trước áp dụng

- `bash -n` đạt.
- Trên VPS, `apt-config -c` đọc file preview ngoài `/etc` trả đúng `1`, `1`, `false`; bước này không áp dụng cấu hình thật.
- Chưa khẳng định thao tác root thực tế đã chạy cho đến khi quản trị viên thực hiện và có kết quả audit sau đó.
