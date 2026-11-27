# ZAP baseline trên staging — 25/11/2026

Chạy passive baseline (không đăng nhập, không active scan), spider 1 phút, 24 URL trên bản staging đang chạy `8af7ab3`. Image chính thức được pin: `ghcr.io/zaproxy/zaproxy@sha256:9624669db72f54d86c4b46a7064518bd9bf79d8c61a4c31befa9923489f328af`.

Không có alert High/Critical trong báo cáo. Console có 9 nhóm cảnh báo; JSON có 12 loại alert (2 Medium, 6 Low, 4 Informational),, gồm CSP, chống clickjacking, nosniff, X-Powered-By, Permissions-Policy, cache directives, non-storable content, modern web app và cross-origin isolation. Không gọi kết quả này là “không còn cảnh báo”. Report raw: `artifacts/hai-week7-zap/report.json` và `report.html`.

Nhánh Hải bổ sung `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` và ẩn `X-Powered-By` trong Nginx staging/production. Chưa deploy cấu hình này lên VPS; cần quét lại sau deploy. CSP đầy đủ cần frontend nonce/hash tương thích Next.js; không thêm `unsafe-inline` chỉ để làm report xanh. Cross-origin isolation phải xét tương thích ảnh R2 và luồng đăng nhập trước khi bật.

Giới hạn: baseline công khai không kiểm các API cần đăng nhập, phân quyền, tiền/kho, hợp đồng mới hoặc CORS/rate-limit đầy đủ. Kết quả chỉ thuộc revision đang triển khai, không thay đánh giá bản release mới.
