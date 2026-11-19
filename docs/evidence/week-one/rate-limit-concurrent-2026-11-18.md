# Rate limit đồng thời hai nguồn — PASS sau sửa, 18/11/2026

Trạng thái hiện tại: **PASS phần hạn mức độc lập và chống giả mạo XFF**, kiểm lại 13:50 UTC sau sửa proxy. Các lượt FAIL bên dưới được giữ làm lịch sử tái hiện. Không thay cho kiểm tra khóa tài khoản hoặc E2E đăng nhập thật.

Môi trường: `https://staging.quanlynh.id.vn`, image/SHA đang chạy cần đối chiếu lại qua audit VPS. Dùng hai nguồn: workstation ngoài VPS và curl chạy trên VPS qua SSH. Đối chiếu public outbound IPv4 qua dịch vụ kiểm tra IP trả kết quả hai nguồn khác nhau; địa chỉ cụ thể không được lưu vào repo.

Request: `POST /api/identity/login`, `Content-Type: application/json`, body `{}`; mỗi nguồn gửi 11 request tuần tự, bắt đầu cùng epoch UTC. Không dùng tên người dùng, mật khẩu hoặc cookie; không đổi dữ liệu nghiệp vụ.

## Kỳ vọng

Mỗi IP có 10 request lỗi nhận 400, request 11 nhận 429, độc lập trong cùng cửa sổ 60 giây.

## Kết quả

| Lượt | Workstation | VPS | Thời gian UTC | Nhận xét |
|---|---|---|---|---|
| 1 | 0 × 400, 11 × 429 | 10 × 400, 1 × 429 | 10:14:37–10:14:41 | Nguồn workstation đã bị giới hạn ở đầu lượt; chưa đủ điều kiện kết luận |
| 2 | 2 × 400, 9 × 429 | 8 × 400, 3 × 429 | 10:17:32–10:17:35 | Hai nguồn cộng lại chỉ có 10 request trước 429 |
| 3 | 1 × 400, 10 × 429 | 9 × 400, 2 × 429 | 10:24:52–10:24:55 | Đã dừng scanner, không gửi login từ công cụ thử trong ít nhất 75 giây trước lúc bắt đầu; ép IPv4 cả hai nguồn; vẫn tổng 10 request |

Kết luận: **FAIL đối với kỳ vọng hạn mức độc lập cho hai nguồn trong phép thử này**. Kết quả gợi ý các request đang cùng partition; chưa xác nhận root cause hoặc cấu hình forwarded headers đang chạy. Public egress khác nhau không tự chứng minh giá trị RemoteIpAddress mà API thực sự nhận; cần audit KnownProxies, địa chỉ Nginx và forwarded headers để kết luận.

Lượt xác minh xen giữa gặp SSH timeout trong khi quét cổng nên không có kết quả hợp lệ, không được tính PASS hoặc dùng để kết luận nghiệp vụ. Sau khi dừng scanner, HTTPS root trả 200 và SSH kết nối lại thành công. Quét TCP toàn dải đã dừng trước khi hoàn tất, không có kết luận toàn dải.

Mức đề xuất S2; cần Hiếu/Hải đối chiếu cấu hình proxy rồi sửa theo PR/kế hoạch triển khai phù hợp. Không thay module Identity hoặc deploy lại chỉ để che kết quả. Sau sửa phải chạy lại cùng phép thử và kiểm tra giả mạo X-Forwarded-For không vượt giới hạn.

Theo dõi sửa và kiểm lại tại [issue #51](https://github.com/Trunghieu220504/restaurant-management-system/issues/51).

## Kết quả audit runtime tiếp theo

API/web đúng revision `7de77d4cbd15deb474bab146ae6f14dd05937dbe`. Environment API không có `ReverseProxy__KnownProxies__*`; địa chỉ Nginx đang là `172.18.0.3`, forwarded headers ở Nginx đã được overwrite đúng. [ReverseProxyConfiguration](../../../src/Host/Restaurant.Api/Configuration/ReverseProxyConfiguration.cs) đặt `ForwardedHeaders.None` khi danh sách proxy tin cậy rỗng; appsettings trong source không khai báo danh sách này. Đây là thiếu sót cấu hình phù hợp trực tiếp với hành vi dùng chung partition quan sát được. Cần đối chiếu mount/config providers và nhãn Compose thực tế trước sửa, sau đó phải retest hai nguồn cùng phép giả mạo X-Forwarded-For. Chưa thay cấu hình hoặc tuyên bố đã sửa lỗi.

## Kiểm lại sau sửa — PASS

Ngày 18/11/2026, audit 13:49 UTC thấy `ReverseProxy__KnownProxies__0=172.18.0.3`, đúng một địa chỉ Nginx runtime. Override đã có trên VPS, Nginx tiếp tục overwrite X-Forwarded-For thành remote_addr. API/web vẫn đúng revision và image ID trước sửa; không publish cổng trực tiếp. Theo dõi bản sửa tại [PR #55](https://github.com/Trunghieu220504/restaurant-management-system/pull/55).

Đối chiếu lại public outbound IPv4 ngay trước phép thử: hai nguồn khác nhau; không in/lưu địa chỉ cụ thể. Cả hai bắt đầu cùng epoch 13:50:39 UTC, dùng cùng request JSON rỗng và script đã dùng để tái hiện:

| Nguồn | Khoảng thời gian UTC | Chuỗi phản hồi | Kết quả |
|---|---|---|---|
| Workstation ngoài VPS | 13:50:39.190–13:50:43.934 | Request 1–10: 400; request 11: 429 | PASS |
| VPS outbound qua SSH | 13:50:39–13:50:42 | Request 1–10: 400; request 11: 429 | PASS |

Ngay sau khi hết hạn mức của workstation, gửi thêm 5 request, mỗi request đổi X-Forwarded-For sang một địa chỉ TEST-NET khác nhau (`203.0.113.1` đến `.5`): cả 5 đều 429. Header giả không tạo hạn mức mới trong phép thử. Không dùng credential, cookie hoặc tên tài khoản; không đổi SQL/SalesDay.

Kết luận: lỗi chia chung hạn mức đã được sửa trong runtime quan sát được; kiểm tra độc lập đồng thời và chống bypass qua XFF đạt. Issue #51 còn theo dõi review/merge PR #55 theo điều kiện Done tuần 1. INF10 tổng thể là PARTIAL do khóa tài khoản chưa nghiệm thu, không đổi cả INF10 thành PASS.
