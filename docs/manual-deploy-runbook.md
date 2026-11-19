# Runbook triển khai staging thủ công — bản nháp chờ review

Tài liệu hướng dẫn cho lần triển khai được phê duyệt trong tương lai; không phải lệnh triển khai lại bản đang chạy. Người vận hành xác minh đường dẫn, compose, quyền và tên dịch vụ thực tế trên VPS trước khi thực hiện. Không đưa secret vào terminal log, Git, PR hoặc ảnh chụp.

## Trước triển khai

1. Ghi commit đã merge, kết quả CI và tag image API/Web theo SHA đầy đủ. Đối chiếu OCI revision và digest GHCR với artifact đã duyệt; không dùng tag `develop` để rollback. Ghi lại digest/image đang chạy và cấu hình compose hiện tại để quay lui.
2. Xác nhận cửa sổ bảo trì, người duyệt, môi trường và database đích. Đọc migration và tính tương thích giữa image cũ/mới và schema trước khi thay đổi. Chuẩn bị người xử lý và tiêu chí dừng.
3. Trước mọi migration, tạo backup SQL theo quy trình được duyệt, xác nhận hoàn tất, vị trí an toàn và checksum; kiểm tra khả năng khôi phục trên database thử nghiệm riêng. Nếu backup hoặc kế hoạch khôi phục không đạt, dừng. Không tự khôi phục đè staging để kiểm thử.
4. Tạo và review script EF idempotent cho đúng DbContext, đúng phiên bản và đúng database; kiểm tra lịch sử migration. Chạy trên bản sao thử nghiệm trước, chỉ áp dụng sau khi được phép. Idempotent không đồng nghĩa không mất dữ liệu hay có thể rollback schema. Ứng dụng không tự migrate lúc khởi động.

## Triển khai và kiểm tra

1. Tải image theo digest đã xác minh; cập nhật `GHCR_API_IMAGE` và `GHCR_WEB_IMAGE` theo cấu hình compose thực tế. Dùng các override [API](../infra/compose.backend.ghcr.yml) và [Web](../infra/compose.frontend.ghcr.yml) cùng các file nền đúng thứ tự đã kiểm tra. Không chạy `down -v`, xóa volume hoặc recreate SQL.
2. Nếu release có migration, thực hiện theo script đã duyệt sau backup; ghi kết quả và lịch sử migration, không ghi thông tin kết nối. Thay đổi chỉ API/Web theo kế hoạch được duyệt.
3. Kiểm tra container, image digest/revision; HTTPS `/`, `/health/live`, `/health/ready` và log đã lọc. `ready` chứng minh kết nối healthcheck, không tự chứng minh mọi migration. Kiểm tra nghiệp vụ với dữ liệu và tài khoản được nhóm cho phép.
4. Ghi SHA, digest, thời gian, kết quả và liên kết bằng chứng đã che dữ liệu nhạy cảm. Nếu smoke test thất bại, dừng và thực hiện kế hoạch rollback đã duyệt.

### Lệnh mẫu cho lần deploy đã được duyệt

Chạy trên VPS sau khi đối chiếu lại labels/mounts. Danh sách dưới đây khớp nguồn Compose đã xác nhận cho staging ngày 18/11 và thêm override proxy đang dùng; nếu đường dẫn/topology thay đổi, phải review lại danh sách trước khi chạy. Người vận hành điền digest mới/cũ đã xác minh. Không chạy khối này chỉ để lấy bằng chứng tuần 1. Không in `docker compose config` đầy đủ vì cấu hình có secret đã nội suy. `sudo` chỉ giữ hai biến image không bí mật; không dựa vào việc sudo tự giữ các biến đã export.

```bash
APP_DIR=/home/deploy/restaurant-management-system
RELEASE_INFRA=/home/deploy/hai-r8-release-7de77d4cbd15/infra
cd "$APP_DIR"
# Nếu file override không tồn tại, dừng và đối chiếu cấu hình, không bỏ qua.
test -f "$APP_DIR/infra/compose.week1-trusted-proxy.yml"
COMPOSE=(sudo --preserve-env=GHCR_API_IMAGE,GHCR_WEB_IMAGE docker compose
  --project-name restaurant-staging --project-directory "$APP_DIR/infra" --env-file "$APP_DIR/infra/.env"
  -f "$APP_DIR/infra/compose.backend.yml" -f "$APP_DIR/infra/compose.backend.https.yml"
  -f "$APP_DIR/infra/compose.frontend.yml" -f "$RELEASE_INFRA/compose.backend.ghcr.yml"
  -f "$RELEASE_INFRA/compose.frontend.ghcr.yml" -f "$APP_DIR/infra/compose.week1-trusted-proxy.yml")
# Ghi OLD_API_IMAGE/OLD_WEB_IMAGE từ image + digest đã xác minh trước đó.
# Đặt NEW_API_IMAGE/NEW_WEB_IMAGE bằng ghcr.io/...@sha256:<digest đã duyệt>.
export GHCR_API_IMAGE="$NEW_API_IMAGE" GHCR_WEB_IMAGE="$NEW_WEB_IMAGE"
"${COMPOSE[@]}" config --quiet
"${COMPOSE[@]}" pull api web
# Chỉ tiếp tục sau khi backup/migration ở mục trước hoàn tất.
"${COMPOSE[@]}" up -d --no-deps --no-build api web
"${COMPOSE[@]}" ps
curl --fail --silent --show-error https://staging.quanlynh.id.vn/health/live
curl --fail --silent --show-error https://staging.quanlynh.id.vn/health/ready
```

Mỗi lệnh phải thành công trước lệnh tiếp theo. `--no-deps` giữ nguyên SQL/Nginx; nếu release cần thay Nginx/network thì phải có kế hoạch riêng, không dùng khối này. Kết quả health phải là HTTP 200 và không thay thế smoke test nghiệp vụ. Giá trị export chỉ tác động phiên shell; sau nghiệm thu cập nhật nguồn cấu hình image của VPS theo quy trình được duyệt, giữ quyền truy cập hạn chế và không đưa secret vào lịch sử lệnh.

## Rollback và bảo vệ dữ liệu

- Nếu schema tương thích ngược, thay image API/Web bằng digest đã ghi trước triển khai, khởi động lại chỉ các dịch vụ đó và chạy lại health/smoke test. Không tự động hạ migration.
- Nếu schema không tương thích hoặc nghi mất dữ liệu, dừng ghi, báo người chịu trách nhiệm và dùng kế hoạch khôi phục được duyệt. Khôi phục backup có thể làm mất các thay đổi sau thời điểm backup; xác định điểm khôi phục và thử trên môi trường riêng trước. Không xóa database/volume hoặc recreate SQL để ép rollback.
- Báo kết quả, hạn chế và thời điểm khôi phục dịch vụ; chỉ đóng nghiệm thu sau review và bằng chứng tương ứng.

Nếu đã xác nhận schema tương thích ngược và các biến digest cũ ở trên chính xác:

```bash
export GHCR_API_IMAGE="$OLD_API_IMAGE" GHCR_WEB_IMAGE="$OLD_WEB_IMAGE"
"${COMPOSE[@]}" pull api web
"${COMPOSE[@]}" up -d --no-deps --no-build api web
curl --fail --silent --show-error https://staging.quanlynh.id.vn/health/ready
```

## Kiểm tra quyền tối thiểu

[Script audit cố định](../scripts/restaurant-week1-audit.sh) chỉ đọc trạng thái SSH/UFW/fail2ban, cấu hình cập nhật, cơ chế renewal và thông tin Docker đã chọn; không in secret environment. Nếu cần cho agent tự thu bằng chứng, người quản trị phải kiểm tra script rồi cài bản sao do root sở hữu, chỉ cho sudo đúng đường dẫn này với không đối số. Không cấp NOPASSWD cho shell hoặc docker và không thêm deploy vào nhóm docker chỉ để đọc bằng chứng. Script này không cấp quyền deploy, SQL write hoặc gia hạn chứng chỉ.

## Lưu ý cấu hình staging đã đối chiếu ngày 18/11/2026

- APT periodic đã được bật lại và kiểm chứng 1/1, reboot=false theo [PR #54](https://github.com/Trunghieu220504/restaurant-management-system/pull/54). Việc này không thay cho kiểm tra log cập nhật hoặc xác nhận bản vá đã cài.
- Trusted proxy đã được thêm bằng override của [PR #55](https://github.com/Trunghieu220504/restaurant-management-system/pull/55), retest hai nguồn đồng thời và spoofed XFF đạt lúc 13:50 UTC. Giữ override này trong danh sách Compose khi deploy API lần sau; xác minh lại peer IP khi Nginx bị recreate. Không dùng danh sách năm file gốc riêng lẻ để vô tình bỏ override.
- [PR #57](https://github.com/Trunghieu220504/restaurant-management-system/pull/57) đã sửa lịch Certbot trỏ đến chứng chỉ Nginx mount trong thư mục dự án và thêm deploy hook root-owned để test/reload Nginx. ACME staging dry-run/hook đạt, chứng chỉ thật không đổi; loaded ExecStart và timer đã được kiểm chứng. Theo dõi log/lần renew thật kế tiếp trên VPS, không force-renew chỉ để tạo bằng chứng và không công khai log/key.
