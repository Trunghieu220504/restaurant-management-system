# Sửa cấu hình trusted proxy của staging hiện tại

## Kết quả kiểm lại ngày 18/11/2026

Audit 13:49 UTC xác nhận API có đúng `ReverseProxy__KnownProxies__0=172.18.0.3`, trùng peer Nginx runtime; override đã tồn tại trên VPS và Nginx tiếp tục overwrite XFF thành remote_addr. API/web giữ nguyên image ID và revision trước sửa. Agent không nhận transcript apply proxy; kết luận dưới đây dựa vào cấu hình và kết quả kiểm thử trực tiếp, không suy ra đã tự so sánh toàn bộ container ID/StartedAt trước/sau.

Hai public outbound IPv4 được xác minh khác nhau, không lưu địa chỉ. Retest bắt đầu cùng epoch 13:50:39 UTC: **mỗi nguồn nhận riêng 10 × 400 rồi 429**, thay cho tổng hạn mức chung trước đây. Ngay sau khi hết hạn mức workstation, thêm 5 request đổi X-Forwarded-For giả sang các địa chỉ TEST-NET khác nhau: cả 5 vẫn 429. Không dùng credential, không đổi SQL/SalesDay; HTTPS và health 200.

Phần rate limit độc lập đồng thời và chống bypass header **PASS**; INF10 tổng thể vẫn PARTIAL vì khóa tài khoản chưa nghiệm thu. [Bằng chứng trong PR #52](https://github.com/Trunghieu220504/restaurant-management-system/pull/52), [issue #51](https://github.com/Trunghieu220504/restaurant-management-system/issues/51) theo dõi review/merge PR này. Giữ override khi deploy API lần sau và xác minh lại peer khi Nginx bị recreate. Không cần áp dụng lại script chỉ để lặp kết quả.

## Lỗi và quy trình sửa ban đầu

Theo dõi [issue #51](https://github.com/Trunghieu220504/restaurant-management-system/issues/51). Trước sửa, API/web dùng image SHA `7de77d4cbd15deb474bab146ae6f14dd05937dbe`, nhưng API không có biến KnownProxies; Nginx ở mạng cũ `restaurant-staging_internal`. Hai nguồn đã tái hiện chia tổng hạn mức 10 login request trước 429. Không dùng mặc định subnet mới của repo để thay mạng SQL đang chạy.

[Script sửa cố định](../scripts/restaurant-week1-fix-proxy.sh) hỗ trợ `--plan` và `--apply`, chỉ root trên hostname restaurant-staging. Script dùng đúng năm file Compose được xác nhận từ label của container API và working directory `/home/deploy/restaurant-management-system/infra`.

## Biện pháp kiểm soát

- Lấy đúng IP Nginx runtime, chỉ tin một proxy cụ thể; không tắt limiter hoặc tin toàn mạng.
- Đối chiếu source image API/Web, loaded image ID và config hash của API đang chạy với cấu hình Compose hiện tại. Nếu có drift, dừng trước khi thay đổi.
- Render preview và xác minh khác biệt duy nhất là thêm `ReverseProxy__KnownProxies__0`; API vẫn không có host ports/mounts.
- Không pull/build image, chạy migration, đổi subnet hoặc tạo lại SQL/Web/Nginx.
- `--apply` tạo backup root-only chứa config và env để khôi phục, tạo override `compose.week1-trusted-proxy.yml`, rồi chỉ `up --no-deps --no-build --pull never api`. API có gián đoạn ngắn trong lúc tạo lại.
- Kiểm tra và reload Nginx trong cùng container để cập nhật DNS upstream API; không restart/recreate container Nginx.
- Health ready phải 200, image API giữ nguyên, container ID và thời điểm bắt đầu của SQL/Web/Nginx phải giữ nguyên. Nếu lỗi, bỏ override vừa tạo và dùng baseline để phục hồi API; ghi log rollback riêng trong backup root-only. Không xuất các file backup/env ra chat hoặc Git.

Sau khi Nginx bị recreate/move sau này, phải kiểm tra lại IP và cập nhật override; không coi IP runtime này là cố định vĩnh viễn. Giữ override trong danh sách file Compose của các lần deploy API tiếp theo cho đến khi có kế hoạch chuyển sang topology cố định của repo.

## Thực hiện trên VPS

Agent tải script vào `/home/deploy/restaurant-week1-fix-proxy.sh`. Quản trị viên xem nội dung và chạy từng lệnh khi terminal là `deploy@restaurant-staging:~$`, không chạy trong PowerShell Windows:

```bash
sudo install -o root -g root -m 0700 /home/deploy/restaurant-week1-fix-proxy.sh /usr/local/sbin/restaurant-week1-fix-proxy
sudo /usr/local/sbin/restaurant-week1-fix-proxy --plan
```

Nếu plan thành công và chỉ nêu thêm đúng peer Nginx/tạo lại API, áp dụng:

```bash
sudo /usr/local/sbin/restaurant-week1-fix-proxy --apply
```

Không cấp NOPASSWD cho script sửa hoặc docker/shell. Nếu guard hoặc rollback báo lỗi, dừng và gửi thông báo lỗi đã lọc, không tự bỏ kiểm tra để chạy tiếp.

## Kiểm chứng

- `bash -n` trên máy phát triển và VPS đạt.
- Preview Compose trên VPS đã xác nhận khác biệt đúng một biến môi trường, không áp dụng container.
- Sau apply cần chạy audit, phép thử 11 request từ hai IP cùng cửa sổ 60 giây, kiểm tra giả mạo X-Forwarded-For và health endpoints. Không đóng issue #51 hoặc đánh dấu INF10 PASS trước retest.
