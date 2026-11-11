# Bao Anh / Phuong / Duy - integration

Branch: feature/duy-baoanh-integration, based on inspect_web_branch 0b1c99b.
Separate worktree: .codex-tmp/baoanh-integration. Earlier local integration is
preserved unchanged in the main working directory; do not merge its second auth.

Use Bao Anh's components/auth provider, lib/auth client, token store, login and
app shell. Duy's SalesDay provider injects this authFetch and live permissions.
There is no second authentication system. No credentials or token storage added.

The /sales-day route uses Phuong's GET current and POST open. Fake localStorage
opening/closing and invented revenue are removed from this live route. Other
restaurant-context prototype pages remain mock and must not be treated as live
financial data. The old prototype closing flow is not a backend implementation.

Canonical generation input: docs/api/openapi.json from develop. Bao Anh's
src/lib/api/generated path and generation script are retained. Run npm run gen:api.
Safe int64 strings normalize to numbers; unsafe amounts/unknown states fail.

Refresh defaults OFF because the backend has no refresh endpoint. The existing
single-flight implementation is retained and tests explicitly enable it. Only
enable NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true after backend refresh is shipped
and cookie/revocation contracts are verified. Without refresh, reload/expiry
requires login. Login accepts the actual username without phone-name rewriting
or prefilled demo credentials. Successful login routes to /sales-day.

Run from this worktree's web directory:

```powershell
npm.cmd ci
npm.cmd run gen:api
npm.cmd test
npm.cmd run build
$env:PLAYWRIGHT_BASE_URL = 'http://127.0.0.1:3002'
npx.cmd playwright test e2e/baoanh-salesday.spec.ts e2e/duy-week-one.spec.ts
npm.cmd run dev -- --hostname 127.0.0.1 --port 3002
```

Use a migrated local API on port 5080 and a real account with salesday.manage.
Browser tests intercept HTTP and are not SQL/staging acceptance. No migrations,
production accounts, secrets, commits, pushes or deployment are performed here.
Shared auth/login/route changes need Bao Anh review; API acceptance needs Phuong.

## Verification

- 25 unit tests and 6 browser tests passed (including desktop/mobile and auth).
- Production build passed after adding the missing toast import in tables/page.
- Full lint remains failing: 6 errors and 55 warnings in prototype menu/inventory
  and mock Identity files. These must be resolved before merging with required CI.
- npm audit reports 10 tooling vulnerabilities; no forced upgrades performed.
- No real SQL/staging acceptance or physical-device audio test has been run.
