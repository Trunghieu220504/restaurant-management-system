# Duy - Week 2 and 3 Frontend Handoff

Date: 2026-11-19. Integrated base: develop 866e533. Branch:
feature/duy-salesday-acceptance. Target: develop after team review.
This handoff records local implementation; deployment and live acceptance
must be recorded separately by the team.

## Scope and Status

| Task | Implemented locally | Remaining acceptance / dependency |
| --- | --- | --- |
| DUY-2.0 | Existing live SalesDay screen; fixed read-failure retry; retained POST idempotency retry | Phuong/Hai: approved account, real database and open-day evidence; review/merge/deploy |
| DUY-2.1 | Kanban new/working/ready, kitchen/bar filter, waiting timer, shared OperationsFeed/NotificationSound, duplicate/reconnect handling, visible audio fallback | Bao Anh review; physical-device sound test. Actual Fulfillment integration belongs to later work and is absent |
| DUY-2.2 | Mock day states, close request/cancel, open-order/count blockers and cash discrepancy reason | Bao Anh review and Phuong confirmation of provisional checklist behavior |
| DUY-2.3 | One-command Playwright staging smoke, configurable HTTPS origin, login/current-day check and guarded open/menu sequence | Credentials, explicit approved test date/write permission, Catalog API path; full sequence NOT run |
| DUY-3.1 | Development billing fixture and UI: partial cash payments, change, transfer references, pending confirmation/cancel, no overcollection, idempotency and competing-command tests | Phuong PHU-3.2: actual HTTP contract/OpenAPI, payment permissions/SQL enforcement and Sales invoice data; live adapter and staging integration not implemented |
| DUY-3.2 | Waiver dialog with reason/permission fixture, only unpaid/unreserved amount, receipt preview and PDF 80 mm | Phuong PHU-3.3 and D-6 approved document fields; real receipt API/permission enforcement and live integration not implemented |

None of these rows is a claim of business acceptance or SQL correctness.
Mock models do not define the backend contract. No financial backend, migration,
auth architecture or shared restaurant store was implemented by the previews.
The develop merge retains Bao Anh's Catalog, Seating and store updates.

## Try Locally

From web/:

```powershell
npm.cmd run dev -- --hostname 127.0.0.1 --port 3016
```

- /dev/week-three: isolated mock kitchen, closing and billing panels.
- /dev/components: existing week-one component/SalesDay regression demo.
- /sales-day: actual API route, requiring the shared login and a running API.
- /kitchen: authenticated preview of new mock board in development only.

The new kitchen does not import restaurant-context. In production it shows an
unavailable source state rather than pretending mock tickets are live. The old
prototype kitchen route was replaced; other owners' develop updates are retained.
/dev/week-three returns 404 in production. No production
payment/waiver route or navigation item was added without an API contract.

All demo data remains in memory; changing tabs retains it, reloading resets it.
Only mute preference is persisted using the existing sound library. Demo
permission checkbox is not security and never authorizes a backend operation.

The print template uses 80 x 200 mm pages and hides surrounding application
content. This is a sample document, not an approved legal/tax receipt. Chromium
PDF inspection found one page, approximately 80.1 mm wide, with the expected
150000 - 50000 = 100000 VND total. Physical printer output remains untested.

## Staging Smoke (Hai)

Run from web/; no infrastructure edits are needed:

```powershell
$env:STAGING_BASE_URL = 'https://staging.quanlynh.id.vn'
npm.cmd run test:smoke:staging
```

Without credentials this only verifies /health/ready. Other checks are SKIP,
not PASS. Supply STAGING_USERNAME and STAGING_PASSWORD through your approved
secret mechanism, never through source, committed env files or shared logs.
The configured URL must be an HTTPS origin without credentials/query/path.

To permit the write sequence, coordinate with Phuong/Hai and additionally set:

- STAGING_ALLOW_OPEN_DAY=true: explicit opt-in; omitted by default.
- STAGING_BUSINESS_DATE: a date allocated by Phuong for the test database.
- STAGING_OPENING_CASH: a nonnegative integer VND amount agreed by the group.
- STAGING_MENU_PATH: an actual same-origin /api/... JSON endpoint from the
  approved Catalog contract. None is invented as a default.

The smoke checks menu availability before any open-day write. Existing active
day causes SKIP: it never closes/deletes it. The write sequence logs in through
the real UI, opens once, checks current again and reads menu JSON. Tests log out
when possible. Traces, screenshots and videos are off to avoid recording secrets.
Do not enable verbose request logging with real credentials.

An opened test day is intentionally not auto-closed or deleted. Phuong owns the
agreed cleanup. A failed run may have created a day: inspect it before rerunning.
Even a passing smoke does not prove SQL concurrency, rollback or migration safety.

## Verification

- npm test: 71 unit tests passed, including six new ledger/close tests.
- npm run build: passed, also with NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true.
- npm run test:e2e:production: 21 passed with mocked API, including Seating
  and production boundary cases; not evidence of real staging refresh.
- Development E2E: 21 passed. Covers desktop/mobile, Catalog/Seating, dedup/reconnect, closing checklist,
  partial cash/transfer, waiver, PDF, panel persistence and keyboard tabs.
- Format and generated API synchronization checked; previews add no dependencies.
  The develop merge includes TanStack Query for Seating.
- ESLint: zero errors, 25 existing warnings outside the new feature modules.
- Staging smoke: readiness passed, authenticated read and approved write/menu
  checks skipped because no account/write authorization was supplied.
- Screenshots generated and overflow/geometry assertions run. Image viewer
  unavailable in this session; no claim of manual screenshot visual approval.
- Real SQL/physical mobile audio/printer, deployed images and new feature CI
  are not verified by these local commands.

## Team Handoff

- Bao Anh: review new UI, /kitchen replacement and shell conventions; agree
  where billing/closing pages belong after contracts exist. Follow the merged
  Seating adapter/query pattern when adding live adapters.
- Phuong: deliver Billing payment/waiver/document contracts. Confirm close
  checklist/discrepancy semantics and approve D-6.
- Catalog and live Seating are now merged into develop. The approved menu
  contract is GET /api/catalog/menu; set STAGING_MENU_PATH accordingly only
  after confirming deployment. An unauthenticated response is not proof
  that an endpoint is deployed.
- Hieu: provide Fulfillment snapshot, event envelope/version and hub contract.
- Hai: run approved smoke after deployment and record commit/image/test outcome.
- Duy: replace fixtures with generated contract types/live adapters only after
  those APIs exist, then add real staging evidence before marking Done.
