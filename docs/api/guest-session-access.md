# HIE-4.4: Guest session access

The existing invitation/exchange implementation is reused. This change completes the read path and its verification.

- Staff with seating.sessions.manage issues an invitation at POST /api/seating/sessions/{id}/guest-access.
- The invitation is random 256-bit material, stored as a SHA-256 hash, valid for ten minutes, consumed once.
- POST /api/public/guest-access/exchange exchanges tableCode + joinCode for a random, hashed, four-hour session grant.
- GET /api/public/guest-access/session validates Authorization: Guest <token>. Closed sessions revoke access, including after the same table is reopened for another session. Transfers keep the grant bound to its original session.
- GET /api/public/menu preserves the existing anonymous read-only menu. A supplied Guest token must be valid, unexpired and unrevoked. No menu read authorizes session entry or ordering.
- This preserves the merged guest UI acceptance tests that keep menu browsing available after a session closes. The planning document's menu-via-token requirement is implemented as a validated credential path alongside that existing public browsing path.
- Menu and session reads share a 240/minute/IP limit, after trusted proxy resolution. Token rotation cannot allocate unlimited limiter partitions or bypass the limit. Exchange retains its existing IP/table limits.
- Invalid token responses return 401 with qr.token.invalid/expired/revoked; rate rejection returns 429 with qr.rate.limited and Retry-After. Responses use no-store.

The guest UI sends its validated read token to menu requests. It separately keeps the canSubmitOrders check;
this change does not enable the HIE-5.1 ordering flow before that backend capability exists.

No migration or new secret is needed. Existing QR codes and session grants remain compatible.
