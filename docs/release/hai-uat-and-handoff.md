# UAT, thiết bị và bàn giao — Hải

Chủ điều phối Hai249. Danh sách 14 kịch bản nằm ở mục 7 trong `docs/phan-cong-chi-tiet-7-tuan.md`. Staging đã triển khai `5f63861` ngày 25/11/2026. Lịch nghiệm thu theo kế hoạch là 16–17/12/2026 và 21–22/12/2026. Hai vòng đang chờ người duyệt chạy và ký nhận.

## Hai vòng UAT

Mỗi lần chạy phải lưu commit/digest API & web, môi trường, người thực hiện, giờ, dữ liệu test dùng riêng, kết quả mong đợi/thực tế, bằng chứng và cleanup. Không dùng test mock hoặc test SQL thay kết quả UI được chủ repo nghiệm thu.

| # | Kịch bản | Kiểm thử kỹ thuật | Vòng 1 | Vòng 2 |
| --- | --- | --- | --- | --- |
| 1 | Mở ngày và chặn lần mở thứ hai | Chờ ngày thử riêng | Chờ | Chờ |
| 2 | Mở bàn, 3 món có size/topping, xác nhận, bếp/bar đúng trạm và âm thanh | Đã kiểm một món QR tại bar trên VPS | Chờ | Chờ |
| 3 | QR gửi đợt, nhân viên xác nhận, khách theo dõi, đóng phiên thu hồi quyền | **Đạt trên staging**, trình duyệt Chromium 360 px và API thật | Chờ | Chờ |
| 4 | Hoàn thành và giao từng phần, nối lại và kiểm âm thanh | Đã giao một phần QR trên VPS | Chờ | Chờ |
| 5 | Thu nhiều lần, hai phương thức, tiền thừa và chặn thu dư | Ma trận SQL đạt phần thu đồng thời | Chờ | Chờ |
| 6 | Gọi thêm sau thu đủ phát sinh số còn phải thu | Chờ chạy nghiệp vụ | Chờ | Chờ |
| 7 | Miễn thu, đóng đơn, in chứng từ | Đã đóng đơn QR giá 0 đ trên VPS | Chờ | Chờ |
| 8 | Báo sự cố, duyệt làm lại, doanh thu không tăng | Có ca trình duyệt dùng API thật trên demo độc lập | Chờ | Chờ |
| 9 | Hoàn một phần có duyệt và chặn vượt hạn mức | Ma trận SQL đạt phần đồng thời và hạn mức | Chờ | Chờ |
| 10 | Chuyển bàn giữ quyền QR theo phiên | **Đạt trên staging**, giữ quyền xem đợt và hiển thị tên bàn đích | Chờ | Chờ |
| 11 | Nhập kho, đếm bất thường, điều chỉnh có lý do | Chờ bộ dữ liệu kho thử | Chờ | Chờ |
| 12 | Chốt bị chặn, chốt đồng thời, hủy chốt và tồn đầu ngày sau | Ma trận SQL đạt các ca đồng thời và rollback | Chờ | Chờ |
| 13 | Phân quyền báo cáo, bếp/bar và Admin | Staff xem báo cáo và Kitchen bắt đầu phiếu bar đều nhận 403 trên VPS. Còn ca Admin | Chờ | Chờ |
| 14 | Đổi mật khẩu thu hồi phiên và giới hạn đăng nhập | Đã kiểm refresh qua HTTPS. Còn phần đổi mật khẩu và hai IP | Chờ | Chờ |

Lượt QR ngày 25/11 tạo `DH-0002` với món 0 đ và hai bàn tách khỏi bàn người dùng đang thử. Sau khi giao món, đơn và phiên đã đóng. Hai bàn được lưu trữ và món thử ngừng bán. Bằng chứng kỹ thuật được lưu trong `artifacts/release/staging-qr-uat.json`.

## Ma trận thiết bị và realtime

| Thiết bị/trình duyệt | Viewport | Cần thử | Trạng thái |
| --- | --- | --- | --- |
| Chrome desktop | 768px, 1024px | Nhân viên/bếp/bar, bật âm thanh, reconnect, dedup | HTTPS login, refresh, màn đơn và WebSocket đã đạt trên VPS |
| Safari iOS thiết bị thật | 360px trở lên | QR, chặn autoplay, resume, dấu hiệu thị giác | CẦN THIẾT BỊ |
| Chrome Android thiết bị thật | 360px trở lên | QR, autoplay, nền/foreground, mất mạng | CẦN THIẾT BỊ |
| Hai trình duyệt | Theo màn | p95 sự kiện đến UI ≤2 giây, kiểm không phát lại âm thanh | Chờ đo đồng thời hai vai trò |
| Lighthouse QR mobile | 360 × 800 | Hiệu năng ≥80, truy cập ≥90 | Hai lượt đo trên `5f63861` có trung vị hiệu năng 76 và 75, truy cập 100. Trung vị thời gian chặn luồng chính ở lượt đo chi tiết là 1.396 ms |

Ứng viên frontend sau đó tải bộ xác thực và thông báo của nhân viên theo nhu cầu, đồng thời dừng tải trước trang đăng nhập ở luồng QR. So sánh ba lần cho mỗi image trên cùng demo Docker giảm dung lượng JavaScript trung vị từ 195.476 byte xuống 173.453 byte, khoảng 11%. Điểm hiệu năng demo từ 67 lên 68 và điểm truy cập vẫn 100. Cần đo lại trên staging trước khi nghiệm thu ngưỡng 80.

Đo API đọc trên staging với hai luồng, 40 mẫu mỗi endpoint, không có lỗi HTTP. p95 là 93 ms cho thực đơn công khai, 54 ms cho sơ đồ bàn, 70 ms cho đơn đang mở và 62 ms cho snapshot phục vụ. Bằng chứng nằm tại `artifacts/release/staging-read-performance.json`. Đo tải luồng ghi gọi món, xác nhận và thu tiền còn mở.

## Thao tác nghiệm thu theo vai trò

- **Quản lý** mở ngày bán và chuẩn bị thực đơn trên môi trường thử. Duyệt sự cố, miễn thu và hoàn tiền trong các ca được phân công. Rà soát điều kiện chốt và đối chiếu báo cáo sau khi đóng ngày thử.
- **Nhân viên** mở phiên trong **Bàn**, bấm **Mời khách qua QR**, rồi dùng **Đơn hàng** để xác nhận đợt, giao phần đã sẵn sàng và ghi nhận thanh toán.
- **Khách** quét mã vừa cấp, chọn món, bấm **Gửi đợt gọi món**, theo dõi xác nhận và thử mở lại phiên sau khi nhân viên đóng bàn.
- **Bếp và bar** vào **Bếp**, bật âm thanh và xử lý các phiếu của trạm mình. Khi nghiệm thu nối lại, kiểm danh sách phiếu và âm báo sau khi phục hồi mạng.
- **Admin** kiểm tài khoản và vai trò dùng cho buổi thử. Dùng tài khoản vận hành được cấp quyền tương ứng để chạy nghiệp vụ.

Không coi Chromium mobile emulation là Safari iOS/Android thật.

## Lỗi và việc sau phát hành

S1: mất dữ liệu/an ninh/ngừng toàn bộ. S2: chặn luồng chính hoặc sai tiền/kho. S3: còn cách xử lý khác. S4: trình bày/cải tiến. Phụ thuộc chưa triển khai được ghi riêng, không giả thành lỗi UAT đã tái hiện.

| ID | Loại | Mức/ưu tiên | Việc | Chủ | Điều kiện đóng |
| --- | --- | --- | --- | --- | --- |
| OPS-01 | Hạ tầng | P0 | Diễn tập rollback trong môi trường riêng | Hải/operator VPS | Helper triển khai staging đã chạy thành công. Cần log từ một lượt quay lui thật |
| OPS-02 | Hạ tầng | P0 | R2 public images/private backup, token, lifecycle, hai restore/API probe | Hải/chủ tài khoản R2 | Upload/read/deny + hai drill thật |
| OPS-03 | Hạ tầng | P0 | Chọn VPS production, DNS/cert/secret riêng và license SQL | Chủ repo/Hải | Production readiness + smoke |
| QA-01 | Nghiệm thu | P0 | 14 UAT cả hai vòng, thiết bị thật, ký nhận | Hải/chủ repo/nhóm | Bằng chứng và không còn S1/S2 |
| QA-02 | Kiểm thử | P1 | Tối ưu hiệu năng QR trên mobile và đo lại trên VPS | Hải | Lighthouse đạt ngưỡng 80/90 trên SHA nghiệm thu |
| OPS-04 | Giám sát | P1 | Nối lỗi monitor/backup/deploy đến kênh nhóm được cấp | Hải | Alert thực nhận khi lỗi giả lập |
| DOC-01 | Bàn giao | P1 | Hướng dẫn từng vai trò, tập huấn, phản hồi có người nhận | Hải điều phối; Duy/Bảo Anh/Phương | Biên bản tham dự và backlog |

## Biểu mẫu restore và release

Mỗi restore: ngày; người chạy; R2 object; checksum; DB drill mới; CHECKDB; API digest; readiness; smoke nghiệp vụ; cleanup đúng DB. Chỉ sau API probe mới ghi PASS.

Release: SHA develop đã review; PR vào main; người merge Hiếu; tag; digest; migration compatibility; backup object; smoke; rollback rehearsal; lỗi S1/S2 còn mở; người nhận bàn giao. Không điền tên người duyệt hoặc PASS thay họ.
