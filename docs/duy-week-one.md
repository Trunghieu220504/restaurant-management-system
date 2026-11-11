# Duy - Week one implementation and handoff

Branch: feature/duy-week-one, based on develop 3c26e5e.
Scope: DUY-1.1, DUY-1.2, DUY-1.3. No backend or shared auth implementation.

## 1. Run the frontend

From the repository root in PowerShell:

```powershell
cd web
npm.cmd ci
npm.cmd run dev
```

Open http://localhost:3000/dev/components in Development. If port 3000 is busy,
use `npm.cmd run dev -- --port 3001` and open the same path on port 3001.
The demo returns not-found in production. The production route is /sales-day.

## 2. Demonstrate DUY-1.1

1. Click the speaker control to activate browser audio.
2. Click Mon moi: one new row, one sound, and a visible notification appear.
3. Click Gui lai su kien: no second row or sound.
4. Disconnect, then reconnect: the full snapshot reloads without a sound.
5. Repeat the old event after reconnect: still deduplicated.
6. Mute, reload the page, and verify the preference persists.
7. Test on real iOS Safari/Android. Browser audio blocking has a visible fallback.

The mock event envelope is provisional: eventId, occurredAt (UTC), payload.
Hieu must approve the hub URL, event names, server clock assumptions and snapshot
semantics before week-four integration. Recent event IDs are bounded to 500.
Events are buffered while a snapshot loads. Data snapshots never trigger sound.
SignalR automatically retries established connections; an initial start failure
is reported to the caller (not an endless hidden retry).

## 3. Demonstrate DUY-1.2

The demo includes status labels, timestamp-based waiting time, integer VND input,
a bounded quantity stepper, and an 80mm receipt. Print to PDF and inspect it.
The receipt is a template, not an approved business receipt. Final content needs
the review planned for week three. Shared components/ui has not been modified.

## 4. Demonstrate DUY-1.3 with mock

1. Choose Chua mo ngay, set the date and opening cash, then open the day.
2. Verify DANG BAN and opening cash. A second open action is unavailable.
3. Use the scenario menu for CHO CHOT, DA CHOT, permission denial and network error.
4. API-level tests verify a second request with a new key is rejected with 409.

GET /current returns only the active day or null. Null is NOT evidence that the
last day was closed. The screen does not invent a closed-day history.

## 5. Connect to the real backend

Phuong/Hai must apply the reviewed Identity and SalesDay migrations and provide
a test account with salesday.manage. Bootstrap Admin alone is not sufficient.
Run the API on port 5080 using the existing backend instructions. The frontend
rewrite proxies /api to API_URL (default http://localhost:5080).

For local integration only, expand Ket noi API that on the Development demo and
enter a valid short-lived access token. The token stays in React memory; it is
not saved to storage, logs, URLs or source. Disconnect clears it. This is NOT a
replacement login screen. Never use a production token in the demo.

For the actual /sales-day route, Bao Anh wraps the authenticated subtree with:

```tsx
<SalesDaySessionProvider session={{ getAccessToken, permissions }}>
  {children}
</SalesDaySessionProvider>
```

Import the provider from @/features/sales-day/session. getAccessToken reads the
auth owner's in-memory token. For automatic refresh/retry, inject the shared
authenticated fetch into createSalesDayApi. No refresh endpoint is assumed here.
The provider is intentionally not wired to a fake identity in the root layout.
Without the provider, /sales-day asks the user to sign in and makes no API calls.

POST /open includes a UUID Idempotency-Key. Network/5xx retries with unchanged
input reuse it. Editing the payload creates a new key. Double clicks are guarded.
401, 403 and documented business error codes have distinct messages.
Dates use Asia/Ho_Chi_Minh; timestamps returned by the API are UTC.
Frontend money is restricted to safe JS integers, narrower than backend Int64.

## 6. Verification

```powershell
cd web
npm.cmd test
npm.cmd run lint
npm.cmd run build
npx.cmd playwright install chromium
npx.cmd playwright test e2e/duy-week-one.spec.ts
```

Playwright uses port 3000 from the existing shared config. Stop any unrelated
server on that port or ask Bao Anh to coordinate a shared config change.

Shared config changes needing Bao Anh review: SignalR dependency and npm lockfile;
native Next 16 flat ESLint config (the old FlatCompat failed with a circular JSON
error); Next-generated JSX setting in tsconfig. No direct components/ui edits.

Npm audit initially reports 10 dev-tool vulnerabilities (8 moderate, 2 critical),
including the existing Vitest/tinypool chain. Do not expose Vitest dev servers.
Coordinate a separate tested dependency upgrade with Bao Anh before release;
do not use npm audit fix --force blindly.

Not claimed complete: real staging acceptance, auth-provider handoff, physical
receipt printing, device audio tests, teammate review, CI/PR merge.
Do not mark the tasks Done until those required acceptance steps are satisfied.
