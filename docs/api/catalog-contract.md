# Catalog — BAO-2.2

Frontend `/menu` gọi API thật, dùng `authFetch` với access token trong bộ nhớ. `GET /api/catalog/menu` cần đăng nhập; mọi thao tác ghi cần quyền `catalog.menu.manage` (Manager được cấp mặc định). Quyền báo hết món không cấp quyền sửa thực đơn.

| Method | Đường dẫn dưới `/api/catalog` | Kết quả |
|---|---|---|
| GET | `/menu` | 200 `{ groups, items, optionGroups }` |
| POST / PUT | `/groups`, `/groups/{id}` | Tạo/sửa nhóm món |
| POST / PUT | `/items`, `/items/{id}` | Tạo/sửa món |
| POST / PUT | `/items/{itemId}/variants`, `/items/{itemId}/variants/{id}` | Tạo/sửa biến thể và giá |
| POST / PUT | `/option-groups`, `/option-groups/{id}` | Tạo/sửa nhóm TOPPING hoặc TUY_CHON |
| POST / PUT | `/option-groups/{groupId}/options`, `/option-groups/{groupId}/options/{id}` | Tạo/sửa lựa chọn và giá cộng |
| PUT / DELETE | `/items/{itemId}/option-groups/{groupId}` | Gắn/cập nhật giới hạn hoặc gỡ nhóm |

POST trả 201, PUT trả 200, DELETE trả 204. Kiểu dữ liệu trong [openapi.json](openapi.json). OpenAPI Catalog được đối chiếu thủ công với DTO/endpoint do Windows Application Control chặn chạy host để xuất lại; cần xuất lại từ host khi môi trường cho phép. Client TypeScript đã sinh lại từ hợp đồng đã cập nhật.

Khi tạo món, gửi thêm `initialVariant` gồm `code`, `name`, `price`. Ví dụ `{"code":"STD","name":"Tiêu chuẩn","price":55000}`. Server lưu món và biến thể trong cùng transaction và trả biến thể trong `variants` của phản hồi 201. Thiếu biến thể hợp lệ trả 400. Khi cập nhật, việc tắt biến thể hoạt động cuối cùng của món đang bán hoặc bật món chưa có biến thể hoạt động trả 409 `catalog.variant.required`.

Triển khai API và web cùng phiên bản. Migration `DeactivateItemsWithoutActiveVariant` tạm ngưng các món cũ thiếu biến thể hoạt động. Quản lý thêm hoặc bật biến thể, kiểm tra giá rồi bật lại món.

Giá là số nguyên VND từ 0 đến `Number.MAX_SAFE_INTEGER`. Mã món/biến thể chỉ gồm chữ ASCII, số, `_`, `-`; tối đa 50 ký tự. Tên có 1–200 ký tự sau trim. Trạm `BEP` hoặc `BAR`.

PUT thực thể gửi nguyên `version` Base64 do server trả về; phiên bản cũ trả 409 `catalog.concurrency.conflict`. Gắn nhóm gửi `{ minSelections, maxSelections }`, yêu cầu `0 <= min <= max`, `1 <= max <= số lựa chọn đang sử dụng`. Dữ liệu ghi được kiểm tra trong transaction Serializable. Không xóa cứng thực thể; PUT `active: false` để ngừng sử dụng. Không thể ngừng lựa chọn nếu làm sai giới hạn của nhóm đã gắn.

Lỗi nghiệp vụ trả ProblemDetails có `code`: 400 `catalog.input.invalid`, `catalog.selection.invalid`; 404 các mã kết thúc `.not_found`; 409 mã trùng, thực thể ngừng sử dụng, lựa chọn còn cần cho giới hạn hoặc xung đột phiên bản. Frontend giữ bản nháp khi ghi bị từ chối và tải lại GET sau khi ghi thành công. Nếu ghi thành công nhưng GET lỗi, không gửi lại POST tự động.

Ảnh món hiện là chỗ trống. Cờ hết món không thuộc BAO-2.2; không có nút giả cập nhật trạng thái bếp/QR.

## Đối chiếu cột 4

1. Mở `/menu` bằng Manager; trong **Nhóm món**, tạo nhóm.
2. Trong **Món ăn**, chọn nhóm, nhập mã, trạm chế biến và giá biến thể tiêu chuẩn.
3. Trong chi tiết món, kiểm tra giá biến thể tiêu chuẩn rồi thêm biến thể thứ hai với giá riêng.
4. Trong **Tùy chọn & topping**, tạo nhóm TOPPING, thêm lựa chọn và giá cộng.
5. Quay lại chi tiết món, gắn nhóm với min/max hợp lệ.
6. Kiểm tra GET `/api/catalog/menu` có đúng món, biến thể, nhóm topping, lựa chọn và min/max; tải lại trang và kiểm tra dữ liệu còn tồn tại.

Kiểm thử trình duyệt có fixture nằm tại [catalog.spec.ts](../../web/e2e/catalog.spec.ts). Kiểm thử API thật tại [catalog-real.spec.ts](../../web/e2e-catalog-real/catalog-real.spec.ts) không intercept request, chỉ chạy khi `CATALOG_REAL_API=1`, `CATALOG_TEST_USER`, `CATALOG_TEST_PASSWORD` được cung cấp. Chỉ dùng tài khoản và database kiểm thử.

```powershell
# Sau khi đã chạy API, áp dụng migration Identity/Catalog/SalesDay và tạo Manager kiểm thử:
$env:API_URL = 'http://127.0.0.1:5080'
$env:CATALOG_REAL_API = '1'
# Cấp CATALOG_TEST_USER/PASSWORD qua môi trường; không commit secret.
cd web
npx playwright test --config e2e-catalog-real/playwright.config.ts
```

**Kiểm chứng ngày 20/11/2026 tại máy phát triển.** Chromium đăng nhập bằng Manager test, tạo món cùng biến thể tiêu chuẩn, thêm biến thể và topping, gắn min/max rồi tải lại. Ca `e2e-catalog-real/catalog-real.spec.ts` đọc được dữ liệu qua API thật với SQL Server 2022 trong Docker. Năm ca trình duyệt dùng fixture cũng đạt. Kết quả ngày 19/11 được lưu trong [biên bản ban đầu](../baoanh-catalog-verification.md).
