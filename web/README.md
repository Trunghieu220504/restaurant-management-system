# Web application

The Next.js app uses same-origin requests under `/api/*`; Next.js rewrites
forward them to `API_URL`. For local development, create `.env.local` from
`.env.example` and keep the backend running at `http://localhost:5080`.
Set `API_URL` in the build environment when producing the standalone output so
the rewrite destination is included in the deployed Next.js configuration.

Run `npm install`, then use `npm run dev`, `npm run build`, `npm run lint`,
`npm test`, or `npm run test:e2e`.

## API Client Generation

Client types are generated with `openapi-typescript` into the dedicated directory
`src/lib/api/generated/` and must never be edited manually.

Run `npm run gen:api` to regenerate types from the committed specification
at `docs/api/openapi.json` (or pass a spec file or live URL via `OPENAPI_SPEC`).
CI enforces that `src/lib/api/generated/` remains strictly in sync with
`docs/api/openapi.json`—any commit with an updated OpenAPI spec that omits regenerated
client types will fail CI (exit code 1). Use `npm run gen:api:check` locally to verify.

## Application UI starter

The dashboard shell includes permission-filtered navigation, loading skeletons,
route and global error boundaries, 403/404 pages, a searchable TanStack table,
an RHF/Zod form example, Sonner toasts, and an accessible confirmation dialog.
Dashboard metrics and rows are sample data; replace them with API-backed data.
The permissions each route needs live in `src/lib/auth/route-permissions.ts`, using the
codes seeded in `IdentityCatalog.cs`. The same table hides menu entries (sidebar, mobile
tabs, the "Thêm" page) and renders the 403 view when a route is opened directly.

The root layout connects `AuthProvider` and `PermissionProvider` to the authenticated
user's permissions before enabling protected navigation. Client-side menu filtering is only
a usability feature: the backend must authorize every protected API request independently.

## Seating preview (BAO-2.1)

Run `npm run dev` and open `/dev/seating`. The preview lets you try area and
table creation, editing and archiving, plus opening, transferring and closing a
table session. The manager and staff options preview the catalog and session
controls. Sample changes persist across reloads within that tab.

Production `/tables` uses the HIE-2.1 endpoints through `features/seating/api.ts`.
The local preview is available only in development. Its browser tests live in
`e2e/baoanh-seating.spec.ts`.

## Cách thay mock (BAO-2.3)

1. Đối chiếu endpoint, quyền, DTO, mã lỗi và quy tắc ghi trong `docs/api/openapi.json` và hợp đồng của module trên `develop`. Kiểu TypeScript lấy từ `@/lib/api/generated`. Khi hợp đồng thay đổi, chạy `npm run gen:api`.
2. Tạo `src/features/<module>/api.ts` theo mẫu Seating. Gửi đường dẫn cùng origin qua `authFetch`, chuyển `AbortSignal` vào GET, ánh xạ DTO và mã lỗi sang dữ liệu hiển thị. Với Seating, quyền quản lý phiên đọc `GET /api/seating/board`, quyền quản lý danh mục đọc `areas` và `tables` để lấy cả phiên bản bản ghi.
3. Đặt hook trong `features/<module>/queries.ts`. Dùng `useQuery` cho GET và `useMutation` cho lệnh ghi. Khóa truy vấn chứa module, tài nguyên và các bộ lọc gửi lên server. Sau khi ghi thành công, gọi `invalidateQueries` để đọc lại dữ liệu. Xem `useSeatingBoard` và `useSeatingChange`.
4. Nối trang vào hook. Trang `/tables` tạo adapter theo quyền và giới hạn bộ nhớ đệm trong phiên hiển thị của tài khoản hiện tại. Khi đổi tài khoản hoặc quyền, trang tạo lại QueryClient. Hiển thị trạng thái tải, rỗng, lỗi và nút thử lại. Giữ nguyên `version` khi sửa hoặc lưu trữ. Với lệnh phiên bàn, dùng cùng `Idempotency-Key` khi thử lại cùng thao tác.
5. Bỏ `useRestaurant()` khỏi trang sau khi đã chuyển cả đọc và ghi. Chạy `npm run lint`, `npm test`, `npm run build`. Trên môi trường tích hợp, đăng nhập bằng tài khoản có quyền, thử một lệnh ghi và tải lại trang để đối chiếu với GET. Để mở phiên bàn, cần có ngày bán đang mở.

### Lộ trình gỡ restaurant-context

Thời hạn đề xuất cho từng trang tính theo ngày làm việc kể từ khi toàn bộ API phụ thuộc của dòng đó có trên môi trường tích hợp.

| Trang                             | Dữ liệu cần chuyển                                                                | Hạn đề xuất |
| --------------------------------- | --------------------------------------------------------------------------------- | ----------- |
| `/orders`                         | Phiên bàn, tạo đơn, phục vụ, thu tiền và đóng đơn từ Seating, Sales và thanh toán | 3 ngày      |
| `/kitchen`                        | Hàng đợi bếp và chuyển trạng thái món từ Fulfillment                              | 2 ngày      |
| `/`                               | Số liệu bàn và đơn hàng từ Seating và Sales hoặc endpoint tổng hợp theo quyền     | 2 ngày      |
| `/reports`                        | Doanh thu và ngày kinh doanh từ Reporting và SalesDay                             | 2 ngày      |
| Khung dashboard (`app-shell.tsx`) | Nhãn ngày bán từ SalesDay                                                         | 1 ngày      |

Sau khi chuyển dòng cuối cùng, tìm `useRestaurant` và `RestaurantProvider` trong `src`, bỏ provider ở layout gốc rồi xóa store trong cùng PR. Các trang ngoài MVP đã gỡ gồm `/customers`, `/promotions`, `/stores` và `/settings`. Trang `/more` giữ các lối vào MVP trên điện thoại theo quyền tài khoản.

## Nghiệm thu giao diện tuần 3

Trang `/orders` đọc phiên đang mở từ Seating và giá từ Catalog, kiểm tra ngày bán và đọc lại giá khi gửi đợt. Trên bản develop dùng để tích hợp (a06708e), Sales chưa công bố endpoint tạo và duyệt đợt HIE-3.1. Adapter thử nghiệm trong `features/orders/batches.ts` lưu đợt theo tài khoản trong tab hiện tại. Lệnh xác nhận thử chưa tạo phiếu bếp hay nghĩa vụ thanh toán. Các đơn của giao diện phục vụ cũ vẫn truy cập được qua liên kết trên trang.

Khách mở `/guest/<tableCode>` để xem thực đơn công khai mẫu và chọn món vào giỏ tạm. Liên kết cũ dạng `/guest?table=T04` dẫn đến cùng trang. Mã bàn chỉ định vị bàn. Trang bỏ fragment chứa mã tham gia và chưa kích hoạt đổi token, đọc đơn hay gửi đơn.

**Lighthouse lần đầu cho BAO-3.3.** Ngày 19/11/2026 lúc 09:33 UTC, Lighthouse 12.8.2 chạy trên Edge headless, Next.js production tại localhost, đường dẫn `/guest/T04`, mobile 360 × 800 px, DPR 1.75. Performance 95, Accessibility 95, Best Practices 96, SEO 100. LCP 2.50 giây, CLS 0. Tuần 5 có thể chạy lại cùng đường dẫn, cấu hình mobile và kích thước để so sánh.

## Identity Authentication & Client-side Route Guard

Follows the specification in `docs/api/identity-contract.md`:

- **In-Memory Access Token**: Token is held strictly in RAM (`token-store.ts`) to prevent XSS.
- **Refresh Cookie**: Scoped to `Path=/api/identity; HttpOnly; SameSite=Strict`.
- **Client-side Route Guard**: Because the refresh cookie path is limited to `/api/identity`,
  Next.js middleware cannot inspect it. Session restoration runs on client mount via
  `refreshAccessTokenSingleFlight()`. Unauthenticated users are redirected to `/login`.
- **Concurrent 401 Single-Flight Refresh**: If multiple requests hit 401 simultaneously,
  only one `/api/identity/refresh` call is made; concurrent requests wait on that single promise
  and automatically retry once refreshed. A 401 that arrives after another request already
  refreshed retries with the new token instead of failing.
- **Cross-tab refresh**: The backend revokes the whole session when a rotated refresh secret is
  reused, so refreshes are serialized across tabs with the Web Locks API (`navigator.locks`).
- **Refresh flag**: `NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true` turns refresh on. It is a
  build-time value, so it must be present when `npm run build` runs (Docker build arg on staging).
  Without it, reloading the page or waiting 15 minutes sends the user back to `/login`.
- **Change password**: `/profile/password` (RHF + Zod, same policy as the backend). The backend
  revokes every session afterwards, so the user is sent back to `/login`.
- **Testing**: Run `npm test` to execute Vitest suites, including the concurrency test in
  `src/lib/auth/auth-client.test.ts`.

## Operations Previews and Staging Smoke

Development preview: `/dev/week-three`. Kitchen, closing and billing fixtures
are isolated from production financial operations. The kitchen route uses the
new board only in development; production does not display fixture tickets.
See `../docs/duy-week-two-three.md` for status, API dependencies, receipt checks
and the guarded staging smoke command (`npm run test:smoke:staging`).
