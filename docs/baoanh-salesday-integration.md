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

Update 2026-11-17: backend refresh was merged in PR #26. The frontend flag must
be true at build time after that API and migration are deployed. The Dockerfile,
compose build args and Web CI now explicitly set it; a runtime container env
does not change compiled browser JS. The auth client still defaults OFF when
the flag is absent. See week-one-acceptance.md for staging evidence and remaining
browser acceptance; the earlier claim that no refresh endpoint exists is obsolete.
Without refresh, reload/expiry requires login. Login accepts the actual username without phone-name rewriting
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
