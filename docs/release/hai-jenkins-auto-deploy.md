# HAI-2.5 Jenkins tự triển khai staging qua SSH

Jenkins controller hiện có được giữ nguyên và đặt restart `unless-stopped`. Agent `restaurant-linux` dùng một executor. Docker Desktop phải chạy để Jenkins có thể hoạt động.

Job tin cậy: `restaurant-deployment/staging`, pipeline nguồn `infra/jenkins/staging-cd.Jenkinsfile`. Job poll develop mỗi 5 phút sau lần chạy đầu. Tham số RELEASE_REVISION rỗng lấy develop; SHA cụ thể chỉ được server chấp nhận nếu đang là develop hoặc có trong approved-revisions root-owned.

Thứ tự: checkout → full SQL/backend → frontend lint/format/unit/build → dependency/secret checks → migration → build image cùng SHA → archive → SSH upload → VPS preflight/backup/restore/migration/probe/apply/rollback → HTTPS smoke. Không nhận submit PR/fork làm nguồn deploy.

Credential SSH `restaurant-ci-deploy` nằm riêng trong folder này, không gắn vào job multibranch kiểm thử. Khóa mới chỉ nhận `upload <sha>` và `deploy <sha>` qua forced command, cấm shell/SFTP/forwarding. Known_hosts lấy từ host key đã xác minh của tài khoản local. Không chứa mật khẩu sudo trong Jenkins.

Receiver chạy dưới user deploy, từ chối symlink/hardlink/path traversal/entry lặp và giới hạn tổng 1,5GB; mỗi thư mục SHA immutable. Helper sudo chỉ nhận một SHA 40 hex; xác minh develop hiện tại/approved revision, copy vào root-private, kiểm checksum và image revision, chỉ chạy deployment script root-owned. Script do CI upload không được thực thi. Việc bootstrap helper/khóa chỉ cần sudo một lần.

Các script: `ci_transport.py`, `ci_deploy.py`, `package_ci.py`, `staging-candidate.sh`. Gói không chứa database hay secret. SQL volume/server giữ nguyên; chỉ API/web/Nginx được cập nhật. Migration thử hai lượt trên bản restore và probe API cũ/mới trước khi đổi live. Lỗi vẫn FAILED kể cả rollback thành công. Backup local không thay offsite R2.

Cần review lại trusted helper khi thay đổi migration layout hoặc chính sách deploy. Một SHA đã cài/đã upload không tự ghi đè khi retry; operator kiểm log trước. Thay đổi pipeline trong repo cần cập nhật cấu hình job tin cậy tương ứng. Không tự mở Jenkins ra Internet.
