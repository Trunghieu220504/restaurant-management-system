# UAT, thiết bị và bàn giao — Hải

Ngày chuẩn bị: 24/11/2026. Chủ điều phối: Hai249. Nguồn 14 bước: mục 7 trong `docs/phan-cong-chi-tiet-7-tuan.md`; lịch cập nhật từ PDF 17/11/2026. Vòng 1 dự kiến 16–17/12/2026, vòng 2 dự kiến 21–22/12/2026. Đây là các ngày tương lai; chưa có buổi UAT hay tập huấn thực tế được xác nhận.

## Hai vòng UAT

Mỗi lần chạy phải lưu commit/digest API & web, môi trường, người thực hiện, giờ, dữ liệu test dùng riêng, kết quả mong đợi/thực tế, bằng chứng và cleanup. Không dùng test mock hoặc test SQL thay kết quả UI được chủ repo nghiệm thu.

| # | Kịch bản | Vòng 1 | Vòng 2 | Phụ thuộc chính |
| --- | --- | --- | --- | --- |
| 1 | Mở ngày; lần mở thứ hai bị chặn | CHƯA CHẠY | CHƯA CHẠY | Deploy bản mới + dữ liệu ngày thử |
| 2 | Mở bàn, 3 món có size/topping, xác nhận, bếp/bar đúng trạm và âm thanh | BLOCKED | BLOCKED | Consumer/state API đã merge; hub và UI thật còn thiếu |
| 3 | QR gửi gọi thêm, nhân viên xác nhận, theo dõi, thu hồi sau đóng phiên | BLOCKED | BLOCKED | Public ordering và realtime hoàn chỉnh |
| 4 | Hoàn thành/giao từng phần, nối lại không phát âm thanh cũ | BLOCKED | BLOCKED | SignalR hub và tích hợp UI thật |
| 5 | Thu nhiều lần/2 phương thức/tiền thừa; hai thu ngân không thu dư | CHƯA CHẠY | CHƯA CHẠY | Bản mới trên staging + tài khoản/dữ liệu riêng |
| 6 | Gọi thêm sau thu đủ phát sinh số còn phải thu | CHƯA CHẠY | CHƯA CHẠY | Sales/Billing + UI thật |
| 7 | Miễn thu, đóng đơn, in chứng từ | BLOCKED | BLOCKED | Trạng thái phục vụ thật trước đóng đơn |
| 8 | Báo sự cố, duyệt làm lại, doanh thu không tăng | BLOCKED | BLOCKED | Fulfillment incidents/remake API |
| 9 | Hoàn một phần có duyệt/hạn mức, vượt hạn mức bị chặn | CHƯA CHẠY | CHƯA CHẠY | Deploy + dữ liệu tiền thử |
| 10 | Chuyển bàn giữ quyền QR theo phiên | CHƯA CHẠY | CHƯA CHẠY | QR + hai bàn dùng riêng |
| 11 | Nhập kho, đếm bất thường, điều chỉnh có lý do | CHƯA CHẠY | CHƯA CHẠY | Deploy + kho thử |
| 12 | Chốt bị chặn, chốt đồng thời/hủy/chốt, tồn đầu hôm sau và snapshot | CHƯA CHẠY | CHƯA CHẠY | Các đơn/phần món xử lý xong |
| 13 | Nhân viên không xem báo cáo, bếp không sửa bar, Admin không có quyền vận hành | BLOCKED | BLOCKED | Cần deploy/test phân quyền API mới |
| 14 | Đổi mật khẩu thu hồi phiên; rate limit đăng nhập | CHƯA CHẠY | CHƯA CHẠY | Tài khoản test được phép đổi mật khẩu, hai IP thật |

## Ma trận thiết bị và realtime

| Thiết bị/trình duyệt | Viewport | Cần thử | Trạng thái |
| --- | --- | --- | --- |
| Chrome desktop | 768px, 1024px | Nhân viên/bếp/bar, bật âm thanh, reconnect, dedup | CHƯA CHẠY trên API thật |
| Safari iOS thiết bị thật | 360px trở lên | QR, chặn autoplay, resume, dấu hiệu thị giác | CẦN THIẾT BỊ |
| Chrome Android thiết bị thật | 360px trở lên | QR, autoplay, nền/foreground, mất mạng | CẦN THIẾT BỊ |
| Hai trình duyệt | Theo màn | p95 sự kiện đến UI ≤2 giây, không âm thanh lịch sử | BLOCKED bởi hub |
| k6 SignalR | 50 kết nối / 30 phút | Negotiate, handshake, ping, không rớt | BLOCKED bởi hub |

Không coi Chromium mobile emulation là Safari iOS/Android thật.

## Lỗi và việc sau phát hành

S1: mất dữ liệu/an ninh/ngừng toàn bộ. S2: chặn luồng chính hoặc sai tiền/kho. S3: còn cách xử lý khác. S4: trình bày/cải tiến. Phụ thuộc chưa triển khai được ghi riêng, không giả thành lỗi UAT đã tái hiện.

| ID | Loại | Mức/ưu tiên | Việc | Chủ | Điều kiện đóng |
| --- | --- | --- | --- | --- | --- |
| DEP-01 | Phụ thuộc | P0 | SignalR hub, public ordering và incidents/remake | Hiếu | Merge + test SQL + OpenAPI + deploy |
| OPS-01 | Hạ tầng | P0 | Cài helper CD mới bằng sudo; khóa CI hạn chế; diễn tập rollback thật | Hải/operator VPS | Log success và failure/rollback đúng digest |
| OPS-02 | Hạ tầng | P0 | R2 public images/private backup, token, lifecycle, hai restore/API probe | Hải/chủ tài khoản R2 | Upload/read/deny + hai drill thật |
| OPS-03 | Hạ tầng | P0 | Chọn VPS production, DNS/cert/secret riêng và license SQL | Chủ repo/Hải | Production readiness + smoke |
| QA-01 | Nghiệm thu | P0 | 14 UAT cả hai vòng, thiết bị thật, ký nhận | Hải/chủ repo/nhóm | Bằng chứng và không còn S1/S2 |
| QA-02 | Kiểm thử | P1 | Bổ sung các dòng matrix còn thiếu assertion SQL | Chủ module ghi trong matrix.json | Mỗi dòng đủ test và bằng chứng |
| OPS-04 | Giám sát | P1 | Nối lỗi monitor/backup/deploy đến kênh nhóm được cấp | Hải | Alert thực nhận khi lỗi giả lập |
| DOC-01 | Bàn giao | P1 | Hướng dẫn từng vai trò, tập huấn, phản hồi có người nhận | Hải điều phối; Duy/Bảo Anh/Phương | Biên bản tham dự và backlog |

## Biểu mẫu restore và release

Mỗi restore: ngày; người chạy; R2 object; checksum; DB drill mới; CHECKDB; API digest; readiness; smoke nghiệp vụ; cleanup đúng DB. Chỉ sau API probe mới ghi PASS.

Release: SHA develop đã review; PR vào main; người merge Hiếu; tag; digest; migration compatibility; backup object; smoke; rollback rehearsal; lỗi S1/S2 còn mở; người nhận bàn giao. Không điền tên người duyệt hoặc PASS thay họ.
