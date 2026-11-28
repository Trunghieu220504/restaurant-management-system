# Guest session access and ordering

Guests exchange a table invitation for a grant that allows menu access, ordering and tracking within their table session.

- Staff with seating.sessions.manage issues an invitation at POST /api/seating/sessions/{id}/guest-access.
- The invitation is random 256-bit material, stored as a SHA-256 hash, valid for ten minutes, consumed once.
- POST /api/public/guest-access/exchange exchanges tableCode + joinCode for a random, hashed, four-hour session grant.
- GET /api/public/guest-access/session validates Authorization: Guest <token>. Closed sessions revoke access, including after the same table is reopened for another session. Transfers keep the grant bound to its original session.
- GET /api/public/menu preserves the existing anonymous read-only menu. A supplied Guest token must be valid, unexpired and unrevoked. No menu read authorizes session entry or ordering.
- This preserves the merged guest UI acceptance tests that keep menu browsing available after a session closes. The planning document's menu-via-token requirement is implemented as a validated credential path alongside that existing public browsing path.
- Menu and session reads share a 240/minute/IP limit, after trusted proxy resolution. Token rotation cannot allocate unlimited limiter partitions or bypass the limit. Exchange retains its existing IP/table limits.
- Invalid token responses return 401 with qr.token.invalid/expired/revoked; rate rejection returns 429 with qr.rate.limited and Retry-After. Responses use no-store.

The guest UI sends its validated grant to menu requests. A valid session response exposes `canSubmitOrders`.

- `POST /api/public/guest-access/batches` accepts an `Authorization: Guest <token>` header, a UUID `Idempotency-Key`, and `lines` containing one entry per portion. Sales prices the selections for the session's business day and creates a batch in `CHO_XAC_NHAN`. It creates the table's order in the same transaction when needed.
- `GET /api/public/guest-access/batches/{id}` returns `id` and `status` for the issuing guest grant. Other guests, including guests at the same table, receive 404. Invalid, expired and revoked grants receive 401.
- Retries with the same key and grant return the existing receipt. The submission revalidates the grant while holding the session lock, which also protects session close. Staff can confirm or reject through the existing Sales endpoints.
- `POST /api/public/guest-access/batches/{id}/cancel` lets the issuing guest grant withdraw a batch that is still `CHO_XAC_NHAN` (ADR-02 section 5). It returns `{ id, status: "DA_HUY" }`; repeating it returns the same receipt. Another guest, another session or an unknown id receives 404; an invalid, expired or revoked grant receives 401. A batch staff already confirmed or rejected, an order that is no longer open, or a business day that is not open returns 409 (`sales.batch.not_pending`, `sales.order.not_open`, `salesday.day.not_open`). Cancelling takes the same locks as submission (day, guest session, order), so it cannot race past a staff decision or a session close.
- Submission and cancellation share one limit of 30 requests per minute per IP; submission accepts at most 50 portions per request. Tracking shares the existing QR read limit.

No migration or new secret is needed. Existing QR codes and session grants remain compatible.
