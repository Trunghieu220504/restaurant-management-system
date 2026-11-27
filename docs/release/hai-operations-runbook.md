# Jenkins và vận hành của Hải

Nguồn công việc: PDF phân công cập nhật 17/11/2026 và `docs/phan-cong-chi-tiet-7-tuan.md` (14 bước UAT, 17 tiêu chí). Code này chuẩn bị phần tự động hóa; việc có file không chứng minh Jenkins, R2, production hoặc UAT đã chạy thành công.

## Jenkins

Job `restaurant-deployment/staging` đã triển khai thành công `8470387` qua SSH ở build #2. Build #4 kiểm thử và tải `5f63861` lên VPS. Sau khi quản trị VPS duyệt SHA, helper áp dụng gói đó và kiểm tra migration, readiness và revision của hai image. Quy trình staging này dùng gói image qua SSH. Quy trình xuất image lên GHCR thuộc job `restaurant-management`.

Khi Jenkins báo đã tải gói nhưng SHA chưa được duyệt, quản trị VPS thêm đúng SHA vào danh sách phê duyệt của helper. Gọi lại helper để áp dụng gói đã được kiểm tra. Lịch sử build giữ kết quả của lần chạy ban đầu. Chi tiết job nằm tại [hướng dẫn Jenkins staging](hai-jenkins-auto-deploy.md).

`Jenkinsfile` dùng Multibranch Pipeline và agent Linux mang label `restaurant-linux`. Agent cần Git, .NET SDK trong `global.json`, Node 22/npm, Python 3, Docker/Compose, OpenSSL, Gitleaks đã kiểm checksum. Không chạy build trên controller. Agent đang được cấp Docker socket cho job tin cậy này; không dùng agent này cho repository/PR không tin cậy. Controller hiện chạy trong container Docker `jenkins` trên máy Windows. Đã khởi động lại và tạo node `restaurant-linux` với 1 executor, container `restaurant-jenkins-agent` và volume riêng `restaurant-jenkins-agent-work`. Agent Linux thực hiện Docker và Bash; SQL test chia sẻ network namespace của agent để kết nối loopback, không mở SQL ra mạng ngoài.

Job `restaurant-management` đã được tạo từ `infra/jenkins/multibranch.xml`, Script Path `Jenkinsfile`, chỉ quét `develop` và nhánh giao việc `feature/hai-week7-delivery-20261124`. Sau merge, bỏ nhánh giao việc khỏi regex. Poll mỗi 5 phút được cấu hình trong pipeline. Giới hạn job và quyền sửa Jenkinsfile/credentials cho người duy trì; **không cấp credential folder này cho PR từ fork hoặc nhánh không tin cậy**. PR có thể chạy GitHub Actions không có secret. Điều kiện `when` là điều phối pipeline, không thay thế phân quyền Jenkins.

Pipeline chạy SQL Server dùng riêng, restore/build/test, kiểm TRX không skip, lint/71+ unit test/build web, NuGet/npm audit và Gitleaks trước khi publish. Migration được xuất từ cùng commit, theo thứ tự xác định, chuẩn hóa UTF-8 và có SHA256SUMS. Ba image API/web/migrations gắn SHA đầy đủ. Đường dẫn Docker build của image migration chỉ chứa bundle SQL, tránh `.dockerignore` loại thư mục artifacts.

Credentials cần tạo trong folder tin cậy:

| ID | Loại | Nội dung |
| --- | --- | --- |
| `restaurant-ghcr` | Username/password | Tài khoản được phép publish 3 package và token GHCR |
| `restaurant-staging-deploy` | SSH private key | Khóa **riêng cho CI**, user deploy; không tái sử dụng khóa SSH cá nhân |
| `restaurant-staging-known-hosts` | Secret file | Host key VPS đã xác minh ngoài luồng CI |

Không có secret trong repo. Không mở cổng Jenkins public khi chưa cấu hình đăng nhập/TLS. Để nghiệm thu CD: một build thật trên `develop` publish/deploy/smoke thành công; một bản thử lỗi trên môi trường thử riêng quay về digest cũ, có log và vẫn trả trạng thái build FAILED. Chưa chạy hai tình huống này thì HAI-2.5 chưa Done. Thời gian checkout đến deploy phải đo; poll 5 phút không bảo đảm tổng thời gian ≤10 phút.

## Cài helper trên VPS (operator có sudo)

Trước khi cài, review commit và chạy `ShellCheck`/test ở CI. Chép **các file đã review** vào `/usr/local/lib/restaurant`, owner root:root, thư mục 0755; script shell 0755, Python 0644. Đặt `deploy.sh` tại `/usr/local/sbin/restaurant-deploy` (root:root 0755). Các thư mục và file ở `/etc/restaurant`, bao gồm compose, phải root-owned và không cho deploy/Jenkins ghi.

Từ compose thực tế đang chạy, chuẩn bị snapshot `/etc/restaurant/staging/compose.yml`, `runtime.env` mode 0600 và `images.yml` đang pin API/web bằng digest. Giữ đúng volume SQL hiện có, subnet, proxy IP và certificate mounts; **không dùng compose mẫu để tạo database rỗng thay database đang chạy**. Thêm logging override sau khi render cấu hình và review. Không restart SQL khi chỉ cập nhật ứng dụng.

Tạo `/etc/restaurant/operations.env` mode 0600 từ `infra/operations.env.example`, bổ sung phần `infra/deploy.env.example`. Các giá trị mẫu phải được thay bằng cấu hình thật. Tài khoản `deploy` hiện chỉ có NOPASSWD cho helper cũ; cần operator cài helper mới và sudoers bằng `visudo`. Sudo rule chỉ cho `/usr/local/sbin/restaurant-deploy`; helper tự từ chối mọi argument ngoài một SHA 40 ký tự. Không cấp shell hoặc docker group cho khóa CI.

Dòng authorized_keys của khóa CI phải có:

```text
restrict,command="/usr/local/lib/restaurant/ssh-dispatch.sh" ssh-ed25519 <PUBLIC_KEY_CI>
```

Dispatcher chỉ nhận `deploy <40-hex-sha>`; không nhận shell, SCP, branch name hoặc ký tự shell. Host key checking bắt buộc ở client.

Trước mỗi SHA có migration, tạo hồ sơ `/etc/restaurant/compatibility/<SHA>.json` root-owned, không ghi được bởi CI, sau khi thật sự thử trên database khôi phục:

```json
{
  "revision": "<40-hex-sha>",
  "previousApiOnMigratedDatabase": "PASS",
  "migrationTwice": "PASS",
  "reviewer": "<người review thực tế>",
  "evidence": "<đường dẫn log thực tế>"
}
```

Đây là cổng triển khai chưa được tự động hóa: không tạo hồ sơ PASS trước khi thử. Migration phá vỡ tương thích cần kế hoạch bảo trì riêng; quay lui image không quay lui schema. Helper backup offsite trước, dừng API/web khi migration, khởi động image pin digest, reload Nginx và smoke. Nếu lỗi, helper khôi phục digest cũ, ghi kết quả rollback và trả mã lỗi. Không tự restore database ứng dụng hoặc xóa dữ liệu phát sinh.

## R2, sao lưu và khôi phục

Cần hai bucket riêng: ảnh public (CORS chỉ origin frontend được duyệt), backup private. Token mỗi chức năng chỉ cấp bucket tương ứng. Rclone dùng config root-private `/etc/restaurant/rclone.conf`, provider Cloudflare, endpoint R2 của tài khoản, `no_check_bucket=true` nếu token không có quyền tạo bucket. Không commit config thật.

`backup.sh` tạo SQL COPY_ONLY/CHECKSUM/COMPRESSION, VERIFYONLY, mã hóa age bằng **public recipient**, upload `.age` và checksum, đọc lại object để so SHA-256. Xóa bản plaintext cả khi lỗi. Chỉ giữ local ciphertext 7 ngày. Offsite đặt lifecycle bucket 30 ngày hoặc chính sách chủ repo chốt; script không xóa remote. Recovery identity phải lưu offline riêng và có người giữ được chỉ định.

Cài hai unit `restaurant-backup.*` vào `/etc/systemd/system`, `systemctl daemon-reload`, `systemctl enable --now restaurant-backup.timer`. Giờ lịch: 03:15 Asia/Ho_Chi_Minh, jitter tối đa 15 phút; log qua journal. Chạy thủ công một lần rồi kiểm object/checksum và URL public backup bị từ chối. Chưa có bucket/token nên chưa nghiệm thu phần này.

`restore-drill.sh <object.bak.age>` tải lại từ R2, kiểm digest, giải mã, VERIFYONLY, tạo **database mới** `RestaurantRestore_<timestamp>_<pid>`, khôi phục tất cả logical data/log files và CHECKDB. Không có WITH REPLACE. Giữ database để operator chạy API image đúng phiên bản với connection string tới database mới, readiness và smoke nghiệp vụ; kết quả script là `DATABASE_RESTORED`, `apiProbe=PENDING`, không phải toàn bộ drill PASS. Sau probe, operator xóa đúng tên database drill đã ghi trong log. Làm hai lần độc lập và lưu ngày/object/SHA/API revision/probe/cleanup vào báo cáo.

## Giám sát, realtime, bảo mật và hiệu năng

`monitor.py` kiểm disk >80%, RAM >90%, backup quá 26 giờ và HTTPS smoke. Cài `restaurant-monitor.*`; lỗi trả exit 1 và ghi journal. Có unit test kích hoạt từng ngưỡng, không lấp đầy đĩa thật. Cần nối trạng thái failed tới kênh cảnh báo nhóm bằng hệ thống được chủ repo cung cấp; hiện **chưa gửi thông báo ra ngoài**.

`/hubs/` trong Nginx chuyển WebSocket Upgrade, không buffer, timeout 3600 giây. Trình duyệt đã xác thực và kết nối qua WebSocket trên staging `5f63861`. Runner của repo dùng một phiên Staff và duy trì 50 kết nối. Trong thư mục `web`, đặt `HAI_STAGING_CREDENTIAL_FILE` trỏ đến file tài khoản staging riêng rồi chạy:

```bash
node scripts/hai-signalr-soak.mjs
```

Kết quả nằm ở `artifacts/release/staging-signalr-30min.json`. Runner dùng WebSocket, đăng ký nhận sự kiện, đọc snapshot và xác thực lại khi kết nối lại. Đo p95 sự kiện đến UI ≤2 giây và kiểm âm thanh trên thiết bị thật nằm trong kịch bản nghiệm thu riêng.

`hot-flows.k6.js` dùng `FLOW_FILE`, `ALLOW_TEST_WRITES=true`, `BASE_URL`, `LOAD_USERNAME` và `LOAD_PASSWORD`. Chạy trên HTTPS với dữ liệu thử riêng hoặc trên demo `http://localhost:3187` bằng `demo.staff`. Fixture mô tả các bước đăng nhập, tạo đơn, xác nhận, thu tiền và dọn dữ liệu thử. Mỗi bước có `operation`, `path`, `method`, `body`, `statuses` và `extract`. Giá trị body `$name` lấy từ biến. Giá trị `{name}` trong path lấy từ response trước. Runner mặc định chạy 2 người dùng ảo, mỗi người 4 lượt, tối đa 1 phút. Ngưỡng cho từng thao tác là p95 dưới 500 ms và mọi phản hồi khớp trạng thái mong đợi.

Lighthouse: cài LHCI đã khóa phiên bản trên agent, đặt `LIGHTHOUSE_QR_URL` là trang QR hoạt động rồi `lhci autorun --config=scripts/release/lighthouserc.cjs`. Ba lần chạy mobile 360px; performance ≥80, accessibility ≥90. Token trong URL QR và báo cáo phải giữ riêng, không upload public.

ZAP: cung cấp `ZAP_IMAGE` dạng `ghcr.io/zaproxy/zaproxy@sha256:...` đã kiểm tra rồi chạy `bash scripts/release/zap.sh`. Báo cáo JSON/HTML ở artifacts; lỗi engine, report rỗng hoặc alert High/Critical đều fail. Kiểm thêm CORS, rate limit hai IP và refresh thật; không suy ra từ readiness.

## Production, phát hành và bàn giao

`infra/compose.production.yml` là mẫu tách project/SQL volume/network/secret, mặc định bind loopback 8443 để chưa mở cho người dùng. Domain/certificate là `quanlynh.id.vn`. Chưa chọn chung hay riêng VPS, chưa có secret/license SQL/cert production. Operator render config, kiểm không dùng chung secret/volume với staging, migrate đúng bundle, bootstrap admin riêng rồi smoke. Chưa deploy production.

Sau khi 17 tiêu chí, 14 UAT vòng 2, hai restore/API probe, security/performance/device checks đạt và không còn S1/S2: Hiếu review/merge PR develop → main bằng merge commit, gắn v1.0.0, Hải deploy cùng image đã nghiệm thu, smoke và diễn tập rollback. Không tự ghi UAT/tập huấn Done hay gắn tag khi điều kiện chưa đạt. Gói bàn giao gồm runbook này, UAT 1/2, matrix, hướng dẫn từng vai trò của chủ module và backlog có chủ/mức độ.
