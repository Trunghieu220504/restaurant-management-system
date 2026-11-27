# UAT, thiết bị và bàn giao — Hải

Chủ điều phối Hai249. Danh sách 14 kịch bản nằm ở mục 7 trong `docs/phan-cong-chi-tiet-7-tuan.md`. Staging đã triển khai `de2f449` ngày 28/11/2026. Lịch nghiệm thu theo kế hoạch là 16–17/12/2026 và 21–22/12/2026. Hai vòng đang chờ người duyệt chạy và ký nhận.

## Hai vòng UAT

Mỗi lần chạy phải lưu commit/digest API & web, môi trường, người thực hiện, giờ, dữ liệu test dùng riêng, kết quả mong đợi/thực tế, bằng chứng và cleanup. Không dùng test mock hoặc test SQL thay kết quả UI được chủ repo nghiệm thu.

| # | Kịch bản | Kiểm thử kỹ thuật | Vòng 1 | Vòng 2 |
| --- | --- | --- | --- | --- |
| 1 | Mở ngày và chặn lần mở thứ hai | Demo từ chối lần mở thứ hai với 409 và giữ nguyên ngày đang mở | Chờ | Chờ |
| 2 | Mở bàn, 3 món có size/topping, xác nhận, bếp/bar đúng trạm và âm thanh | Đã kiểm một món QR tại bar trên VPS. Luồng demo có bếp, bar và topping đã chạy lại | Chờ | Chờ |
| 3 | QR gửi đợt, nhân viên xác nhận, khách theo dõi, đóng phiên thu hồi quyền | **Đạt trên staging**, trình duyệt Chromium 360 px và API thật | Chờ | Chờ |
| 4 | Hoàn thành và giao từng phần, nối lại và kiểm âm thanh | Đã giao một phần QR trên VPS. Chromium desktop nhận một âm báo, phục hồi phiếu khi nối lại và không phát lại âm báo cũ | Chờ | Chờ |
| 5 | Thu nhiều lần, hai phương thức, tiền thừa và chặn thu dư | Demo đối soát đủ hai lần thu tiền mặt/chuyển khoản và trả lại 5.000 đ. Ma trận SQL đạt phần thu đồng thời | Chờ | Chờ |
| 6 | Gọi thêm sau thu đủ phát sinh số còn phải thu | Demo xác nhận đợt mới và số còn phải thu tăng từ 0 lên 20.000 đ | Chờ | Chờ |
| 7 | Miễn thu, đóng đơn, in chứng từ | Demo đã miễn thu, đọc chứng từ có số dư 0 và đóng đơn. Còn thao tác in trên thiết bị nghiệm thu | Chờ | Chờ |
| 8 | Báo sự cố, duyệt làm lại, doanh thu không tăng | Có ca trình duyệt dùng API thật trên demo độc lập | Chờ | Chờ |
| 9 | Hoàn một phần có duyệt và chặn vượt hạn mức | Demo chặn yêu cầu quá hạn mức bằng 409 và xác nhận khoản hoàn 5.000 đ sau khi Quản lý duyệt | Chờ | Chờ |
| 10 | Chuyển bàn giữ quyền QR theo phiên | **Đạt trên staging**, giữ quyền xem đợt và hiển thị tên bàn đích | Chờ | Chờ |
| 11 | Nhập kho, đếm bất thường, điều chỉnh có lý do | Demo ghi sổ phiếu 10.000 đ, từ chối điều chỉnh thiếu lý do và từ chối cập nhật số đếm bằng phiên bản cũ | Chờ | Chờ |
| 12 | Chốt bị chặn, chốt đồng thời, hủy chốt và tồn đầu ngày sau | Demo xác nhận số đếm chưa duyệt chặn chốt, hủy chốt mở lại ngày và chốt xong chuyển 8 đơn vị tồn sang ngày sau. Ma trận SQL đạt ca chốt đồng thời | Chờ | Chờ |
| 13 | Phân quyền báo cáo, bếp/bar và Admin | Staff xem báo cáo và Kitchen bắt đầu phiếu bar đều nhận 403 trên VPS. Admin nhận 403 khi đọc sơ đồ bàn trên demo | Chờ | Chờ |
| 14 | Đổi mật khẩu thu hồi phiên và giới hạn đăng nhập | Refresh HTTPS đạt. Đổi mật khẩu ở phiên thứ nhất làm access token và refresh token của phiên thứ hai trả 401 trên demo | Chờ | Chờ |

Lượt QR ngày 25/11 tạo `DH-0002` với món 0 đ và hai bàn tách khỏi bàn người dùng đang thử. Sau khi giao món, đơn và phiên đã đóng. Hai bàn được lưu trữ và món thử ngừng bán. Bằng chứng kỹ thuật được lưu trong `artifacts/release/staging-qr-uat.json`.

Hai luồng Playwright trên demo Docker dùng API và SQL Server thật đã chạy lại với ứng viên frontend, gồm gọi món đến đóng đơn và báo sự cố đến duyệt làm lại mà doanh thu không tăng. Ca đổi mật khẩu đã vô hiệu hóa tài khoản thử khi kết thúc. Bằng chứng gồm `artifacts/hai-live-demo/core-flow-evidence.json` và `artifacts/release/demo-identity-salesday-uat.json`.

Ca Billing trên demo dùng API và SQL Server thật bao gồm hai phương thức thanh toán, phát sinh số phải thu, miễn thu, chứng từ, đóng đơn và hoàn tiền. Bằng chứng kỹ thuật nằm tại `artifacts/release/demo-billing-uat.json`.

Ca kho và chốt ngày chạy trên một cơ sở dữ liệu demo mới. Sau khi nhập 10, điều chỉnh thêm 2 và kiểm đếm 8, bản xem trước ghi nhận sử dụng 4 và tồn cuối trị giá 8.000 đ. Ngày sau mở với đúng 8 đơn vị tồn và 8.000 đ giá trị. Bằng chứng nằm tại `artifacts/release/demo-stock-uat.json`.

## Ma trận thiết bị và realtime

| Thiết bị/trình duyệt | Viewport | Cần thử | Trạng thái |
| --- | --- | --- | --- |
| Chromium desktop | 1024 × 768 | Nhân viên/bếp/bar, bật âm thanh, nối lại, chống trùng | Đăng nhập HTTPS, làm mới phiên, màn đơn và WebSocket đạt. Màn bar phục hồi đúng một phiếu sau khi mất kết nối và không phát lại âm báo |
| Safari iOS thiết bị thật | 360px trở lên | QR, chặn autoplay, resume, dấu hiệu thị giác | CẦN THIẾT BỊ |
| Chrome Android thiết bị thật | 360px trở lên | QR, autoplay, nền/foreground, mất mạng | CẦN THIẾT BỊ |
| Hai phiên Chromium desktop | 1024 × 768, 768 × 1024 | p95 sự kiện đến UI ≤2 giây, kiểm không phát lại âm thanh | **Đạt trên `d56783a`**. Sáu đơn thử, 24 mẫu mỗi phiên, p95 từ lúc gửi API đến khi màn bar cập nhật lần lượt 1.414 và 1.415 ms |
| SignalR qua HTTPS | 50 kết nối WebSocket | Giữ 50 kết nối và phục hồi phiên khi token hết hạn trong 30 phút | **Đạt trên staging `5f63861`** ngày 27/11. Cuối lượt còn đủ 50 kết nối, 100 lần nối lại qua hai lượt làm mới token, 0 lỗi |
| Lighthouse QR mobile | 360 × 800 | Hiệu năng ≥80, truy cập ≥90 | **Đạt trên `de2f449`** ngày 28/11. Cả ba lần đo đạt 98 điểm hiệu năng và 100 điểm truy cập |

Frontend `d56783a` tải bộ xác thực và thông báo của nhân viên theo nhu cầu, đồng thời dừng tải trước trang đăng nhập ở luồng QR. So sánh ba lần cho mỗi image trên cùng demo Docker giảm dung lượng JavaScript trung vị từ 195.476 byte xuống 173.453 byte, khoảng 11%. Điểm hiệu năng demo từ 67 lên 68 và điểm truy cập vẫn 100. Sau triển khai, Lighthouse 13.6.0 đo trên staging bằng Chromium 360 × 800 theo phiên QR thực, xác thực thành công cả ba lần. Lần đo lại tắt chế độ giảm ưu tiên cửa sổ chạy nền của Chromium để công cụ hoàn tất. Hai lượt đo trước đó trên `5f63861` có trung vị hiệu năng 76 và 75. Bằng chứng mới nằm tại `artifacts/release/staging-d56783a-lighthouse-qr.json`.

Lượt đo lại sau triển khai `de2f449` xác thực thành công cả ba phiên QR. Bằng chứng nằm tại `artifacts/release/staging-de2f449-lighthouse-qr.json`.

Đo API đọc trên staging với hai luồng, 40 mẫu mỗi endpoint, không có lỗi HTTP. p95 là 93 ms cho thực đơn công khai, 54 ms cho sơ đồ bàn, 70 ms cho đơn đang mở và 62 ms cho snapshot phục vụ. Bằng chứng nằm tại `artifacts/release/staging-read-performance.json`.

Ngày 27/11, k6 trong demo ghi nhận một HTTP 409 khi tạo đơn đồng thời. Bản sửa cấp mã đơn dưới khóa cấp phát của SQL Server và tính mã kế tiếp tại cơ sở dữ liệu. Ca hồi quy tám yêu cầu đồng thời đạt cùng 33 ca Sales/QR và nghiệp vụ liên quan. Hai lượt đo sau sửa đều chạy đủ tám lần với 56/56 phản hồi đúng. Ở lượt đầu, p95 xác nhận khoảng 2,86 giây và thu tiền 743 ms, vượt ngân sách 500 ms. Ở lượt sau khi hệ thống đã khởi động ổn định, p95 đăng nhập, tạo đơn, xác nhận và thu tiền lần lượt là 146, 19, 49 và 32 ms. Bằng chứng nằm ở `artifacts/release/demo-k6-after-cold-summary.json` và `artifacts/release/demo-k6-summary.json`.

Ngày 28/11, k6 trên staging `d56783a` chạy hai luồng, tám lượt. p95 đăng nhập 314 ms, tạo đơn 102 ms và xác nhận 174 ms. Thu tiền có một HTTP 409 `billing.concurrency.conflict` và p95 5.088 ms. Đã đóng tám đơn thử, hoàn đủ 7.000 đ đã thu, đối soát tám hóa đơn đều có nghĩa vụ, số thực thu và số còn phải thu bằng 0, rồi kiểm món thử đã ngừng bán. Bằng chứng nằm tại `artifacts/release/staging-d56783a-k6-report.json` và `artifacts/release/staging-d56783a-k6-cleanup.json`.

Demo SQL Server với `READ_COMMITTED_SNAPSHOT OFF` tái hiện sáu deadlock trong 14 cặp nghiệp vụ đồng thời. Nhật ký SQL chỉ ra truy vấn chứng từ có thể quét khóa thanh toán của hóa đơn khác. Bản sửa dùng chỉ mục hiện có để đọc đúng thanh toán của từng hóa đơn. Chạy lại 14 cặp trên cơ sở dữ liệu demo mới với cùng chế độ khóa không còn lỗi. Bộ 16 ca SQL liên quan đạt, gồm ca thu tiền khi hóa đơn khác còn giữ giao dịch mở.

Sau deploy `de2f449`, chạy lại k6 trên staging với hai luồng và tám lượt. Cả 56 phản hồi đúng trạng thái mong đợi, không có 409. p95 đăng nhập 316 ms, tạo đơn 87 ms, xác nhận 149 ms và thu tiền 188 ms, đều dưới 500 ms. Đã đóng tám đơn thử, hoàn đủ 8.000 đ, đưa số dư từng hóa đơn thử về 0 và ngừng bán món thử. Bằng chứng nằm tại `artifacts/release/staging-de2f449-k6-report.json`.

Lượt thử độc lập `DH-0004` lặp lại thành công luồng QR và chuyển bàn. Màn bar 1.024 × 768 nhận đủ phiếu mới, bắt đầu, sẵn sàng và loại phiếu khỏi hàng đợi sau khi giao. Đơn 0 đ, phiên đóng và dữ liệu thử đã được dọn bằng thao tác lưu trữ hoặc ngừng bán. Bằng chứng nằm tại `artifacts/release/staging-qr-uat-realtime.json`.

Bằng chứng kiểm tra 50 kết nối WebSocket nằm tại `artifacts/release/staging-signalr-30min.json`.

Sau khi triển khai `d56783a`, HTTPS, đăng nhập, làm mới phiên, màn đơn và WebSocket đã đạt. Lượt thử `DH-0005` chạy lại luồng QR qua Chromium 360 px và chuyển bàn. Bốn cập nhật màn bar xuất hiện sau 1.686, 1.521, 918 và 937 ms. Đơn và phiên đã đóng, hai bàn được lưu trữ và món giá 0 đ được ngừng bán. Bằng chứng nằm tại `artifacts/release/staging-d56783a-smoke.json`, `artifacts/release/staging-d56783a-browser-smoke.json` và `artifacts/release/staging-d56783a-qr-uat-realtime.json`.

Lượt `DH-0014` trên `d56783a` kiểm thêm nối lại trên Chromium 1024 × 768. Sau khi bật âm thanh, phiếu mới phát một âm báo Web Audio. Trình duyệt mất kết nối khi bar bắt đầu món, sau đó khôi phục đúng một phiếu ở cột **Đang làm** và giữ nguyên số lần phát âm báo. Đơn, phiên và dữ liệu dùng riêng đã được dọn. Bằng chứng nằm tại `artifacts/release/staging-d56783a-qr-uat-reconnect.json`.

Lượt đo hai phiên Chromium trên `d56783a` theo dõi cùng sáu đơn thử. Mỗi phiên ghi 24 cập nhật từ xác nhận, bắt đầu, sẵn sàng đến giao món. p95 được tính theo thứ hạng trên từng phiên, lần lượt 1.414 và 1.415 ms. Sáu đơn giá 0 đ đã đóng và món thử đã ngừng bán. Bằng chứng nằm tại `artifacts/release/staging-d56783a-realtime-two-windows.json`.

Sau khi triển khai `de2f449`, HTTPS, đăng nhập, làm mới phiên và WebSocket đều đạt. Lượt QR `DH-0021` xác nhận gửi đợt, theo dõi, chuyển bàn, giao món và thu hồi quyền khi đóng phiên. Đơn đã đóng, hai bàn thử được lưu trữ và món thử đã ngừng bán. Bằng chứng nằm tại `artifacts/release/staging-de2f449-smoke.json`, `artifacts/release/staging-de2f449-browser-smoke.json` và `artifacts/release/staging-de2f449-qr-uat-realtime.json`.

## Thao tác nghiệm thu theo vai trò

Trên staging, mở [màn Bàn](https://staging.quanlynh.id.vn/tables) bằng tài khoản Nhân viên hoặc Quản lý. Chọn **Bàn 5**, mở phiên nếu bàn đang trống rồi bấm **Mời khách qua QR**. Món **Trà đá kiểm thử QR** trong nhóm **Kiểm thử QR** đã được chuẩn bị để kiểm tra thực đơn và nút gửi món.

- **Quản lý** mở ngày bán và chuẩn bị thực đơn trên môi trường thử. Duyệt sự cố, miễn thu và hoàn tiền trong các ca được phân công. Rà soát điều kiện chốt và đối chiếu báo cáo sau khi đóng ngày thử.
- **Nhân viên** mở phiên trong **Bàn**, bấm **Mời khách qua QR**, rồi dùng **Đơn hàng** để xác nhận đợt, giao phần đã sẵn sàng và ghi nhận thanh toán.
- **Khách** quét mã vừa cấp, chọn món, bấm **Gửi đợt gọi món**, theo dõi xác nhận và thử mở lại phiên sau khi nhân viên đóng bàn.
- **Bếp và bar** vào **Bếp**, bật âm thanh và xử lý các phiếu của trạm mình. Khi nghiệm thu nối lại, kiểm danh sách phiếu và âm báo sau khi phục hồi mạng.
- **Admin** kiểm tài khoản và vai trò dùng cho buổi thử. Dùng tài khoản vận hành được cấp quyền tương ứng để chạy nghiệp vụ.

Không coi Chromium mobile emulation là Safari iOS/Android thật.

### Buổi tập huấn trên staging

1. Quản lý chuẩn bị ngày bán, khu vực bàn thử, thực đơn và tài khoản cho từng vai trò. Người điều phối ghi SHA đang chạy và chọn thiết bị dùng trong buổi.
2. Nhân viên và khách thực hành mở bàn, cấp QR, gọi món, xác nhận, chuyển bàn và theo dõi đợt. Bếp và bar xử lý món đúng trạm rồi bàn giao cho nhân viên.
3. Nhân viên thực hành giao từng phần, xử lý thanh toán và đóng đơn. Quản lý minh họa cách duyệt làm lại, miễn thu, hoàn tiền và xem lý do ngày bán bị chặn chốt.
4. Người điều phối ghi ngày, thiết bị, người tham dự, tình huống đã thực hành, phản hồi, người phụ trách và hạn xử lý. Lưu biên bản cùng kết quả UAT nếu cùng một buổi.

## Lỗi và việc sau phát hành

S1: mất dữ liệu/an ninh/ngừng toàn bộ. S2: chặn luồng chính hoặc sai tiền/kho. S3: còn cách xử lý khác. S4: trình bày/cải tiến. Phụ thuộc chưa triển khai được ghi riêng, không giả thành lỗi UAT đã tái hiện.

| ID | Loại | Mức/ưu tiên | Việc | Chủ | Điều kiện đóng |
| --- | --- | --- | --- | --- | --- |
| OPS-01 | Hạ tầng | P0 | Diễn tập rollback khi nghiệm thu release | Hải/operator VPS | Demo tạo lỗi web 502, khôi phục image ban đầu và kiểm `/login` cùng `/health/ready` đều 200. SQL giữ đúng container. Sáu ca helper trong container cô lập đều đạt. Lượt rollback release thực tế còn chờ |
| OPS-02 | Hạ tầng | P0 | R2 public images/private backup, token, lifecycle, hai restore/API probe | Hải/chủ tài khoản R2 | Upload/read/deny + hai drill thật |
| OPS-03 | Hạ tầng | P0 | Chọn VPS production, DNS/cert/secret riêng và license SQL | Chủ repo/Hải | Production readiness + smoke |
| QA-01 | Nghiệm thu | P0 | 14 UAT cả hai vòng, thiết bị thật, ký nhận | Hải/chủ repo/nhóm | Bằng chứng và không còn S1/S2 |
| QA-02 | Kiểm thử | P1 | Tối ưu hiệu năng QR trên mobile và đo lại trên VPS | Hải | Đã đo 98/100 trên `de2f449`. Chạy lại khi SHA nghiệm thu thay đổi |
| PERF-01 | Thanh toán | S2 | Thu tiền đồng thời gặp 409 trên `d56783a` | Hải | Đã đóng trên `de2f449`. Hồi quy SQL và k6 staging đạt, p95 thu tiền 188 ms |
| OPS-04 | Giám sát | P1 | Nối lỗi monitor/backup/deploy đến kênh nhóm được cấp | Hải | Alert thực nhận khi lỗi giả lập |
| DOC-01 | Bàn giao | P1 | Hướng dẫn từng vai trò, tập huấn, phản hồi có người nhận | Hải điều phối; Duy/Bảo Anh/Phương | Biên bản tham dự và backlog |

## Biểu mẫu restore và release

Mỗi restore: ngày; người chạy; R2 object; checksum; DB drill mới; CHECKDB; API digest; readiness; smoke nghiệp vụ; cleanup đúng DB. Chỉ sau API probe mới ghi PASS.

Release: SHA develop đã review; PR vào main; người merge Hiếu; tag; digest; migration compatibility; backup object; smoke; rollback rehearsal; lỗi S1/S2 còn mở; người nhận bàn giao. Không điền tên người duyệt hoặc PASS thay họ.
