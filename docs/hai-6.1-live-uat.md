# HAI-6.1 — bản UAT chạy thật trên máy

Nhánh `chore/hai-6.1-uat-integration` ghép phần seed HAI-2.1/2.4, kiểm chứng HAI-5.1, UI bếp/thu tiền của Duy và phần nối API còn thiếu. Không merge tự động vào develop/main; không thay dữ liệu VPS.

## Mở thử

- URL: `http://localhost:3187` (chỉ trên máy chạy Docker).
- Tài khoản: `demo.manager`, `demo.staff`, `demo.kitchen`, `demo.bar`, `demo.admin`.
- Mật khẩu ở `artifacts/hai-live-demo/credentials.json`; file này bị gitignore và không được commit.
- Khởi động từ repo: `python scripts/release/start_demo.py`. Có `--skip-build` khi image đã được build đúng source; `--seed` khi muốn chạy lại seed/migration idempotent trên đúng database demo. Lần khởi động thường không reset dữ liệu đang thử.
- Chỉ Nginx mở `127.0.0.1:3187`; SQL, API và web không mở cổng trực tiếp. Project/volume/network tách biệt với các stack khác.

## Các phần đã nối và kiểm trên trình duyệt thật

1. Staff tạo đơn, gọi món/size/topping và gửi đợt lên SQL thật; danh sách đơn đọc từ API có phân trang, không còn giả lập bằng sessionStorage ở màn `/orders`.
2. Staff xác nhận đợt. Sales/Billing/Outbox commit cùng transaction, consumer tạo phiếu đúng trạm.
3. Kitchen/Bar nhận snapshot và sự kiện SignalR qua Nginx, thấy tên món/size/topping/ghi chú/mã đơn, bắt đầu và hoàn thành đúng quyền.
4. Staff nhận món sẵn sàng, giao món; Kitchen/Bar không có quyền giao thay Staff.
5. Hóa đơn tự lấy theo đơn và ngày hiện tại; người dùng không nhập ID kỹ thuật. Khoản thu/miễn thu giữ idempotency key khi kết quả chưa rõ; role không có quyền không thấy hành động đó.
6. Thu tiền và đóng đơn được thực hiện bằng API. Đóng đơn vẫn bị server chặn nếu chưa đủ điều kiện.
7. Realtime join trước snapshot; giữ rowversion/tombstones, bỏ sự kiện cũ/trùng; reconnect làm mới token khi cần. Âm thanh cần người dùng bấm bật, có thông báo thị giác khi trình duyệt chặn.

Bằng chứng browser: `artifacts/hai-live-demo/core-flow-evidence.json`, `paid-order.png`, `orders-mobile.png`. Kịch bản thật đã tạo 2 phần món, 1 khoản thu 45.000 VND, còn phải thu 0 và đóng đơn; không dùng mock, không lỗi JavaScript. Kiểm thêm viewport 390px; không coi đây là thử trên Safari iOS/Android vật lý.

Chạy lại trên đúng demo riêng:

```powershell
$env:HAI_DEMO_CREDENTIAL_FILE = (Resolve-Path artifacts/hai-live-demo/credentials.json).Path
$env:HAI_DEMO_ALLOW_WRITES = 'true'
Push-Location web
npx playwright test --config playwright.hai-live.config.ts
Pop-Location
```

SQL đầy đủ: 441 integration + 5 architecture PASS, 0 skipped, ở `artifacts/hai-sql-20261124T192756Z`. Web: 79 unit test PASS và build thành công. Test browser là một luồng lõi, không thay báo cáo 14 kịch bản UAT của cả hai vòng.

## Phần chưa được nghiệm thu

Staging vẫn chạy revision cũ vì user deploy chỉ được gọi helper cũ; chưa cài helper CD mới, chưa có GHCR token `write:packages`, chưa có R2/age/rclone trên VPS, chưa có hai restore từ R2 kèm API probe, production/DNS/secret riêng và tập huấn. Token GitHub hiện có chỉ `gist, repo, workflow` nên không tự dùng để publish GHCR bằng Jenkins.

Các nhánh QR/session-access, incidents/remake và UI service/realtime mới của nhóm đã được phát hiện trên GitHub nhưng chưa mặc nhiên coi là merged/đã nghiệm thu. Bản demo này chỉ chứng nhận các luồng nêu trên; không ghi toàn bộ bảy tuần Done.

## Kiểm chứng bổ sung ngày 25/11/2026

- Production UI smoke: 40/40 PASS với NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true; đây là suite fixture giao diện, tách biệt với E2E thật đã nêu.
- SignalR qua Nginx demo: 50 kết nối trong 1.801.014 ms (~30 phút), cuối kỳ 50/50 active, 0 failure; 2 lần refresh, 100 lượt reconnect theo hết hạn JWT. Kết quả ở artifacts/hai-live-demo/signalr-30min.json. Không coi là nghiệm thu đường mạng Cloudflare/VPS.
- PR #105 đã sửa và xanh ở e69f75f; PR #116 seed và #117 atomic Billing cũng có Build/SQL xanh. PR #118 là nhánh tích hợp UAT, theo dõi CI riêng.
