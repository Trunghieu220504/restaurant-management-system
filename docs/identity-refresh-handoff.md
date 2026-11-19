# HIE-1.3 refresh token handoff

## Contract

POST `/api/identity/login` and POST `/api/identity/refresh` return the same
`LoginResult`: accessToken, expiresAt, account. Refresh accepts no body and reads
the `rt` cookie. Raw refresh secrets are excluded from JSON and object logging.
Cookies are HttpOnly, Secure, SameSite=Strict, Path=/api/identity, host-only.
Use HTTPS for local browser integration and staging; do not disable Secure.

Access tokens last 15 minutes. Refresh secrets expire seven days after login;
rotation does not extend that deadline. The database stores SHA-256 hashes only.
Reusing a consumed secret revokes its entire session immediately, including the
latest access token and every refresh secret in that session. Concurrent refresh
calls must therefore be serialized by the frontend, including across tabs.
Other device sessions are unaffected. Password changes revoke all user sessions.

Missing, expired, revoked, disabled-account and reused secrets return 401.
Refresh failures do not overwrite cookies, avoiding stale responses deleting a
newer cookie. Login, logout and password changes retain their existing shapes.
Logout accepts either a valid access token or the refresh cookie, so the current
frontend can log out even after its access token expires. Without either proof
it returns 401; successful logout clears the cookie and revokes its session.

## Deployment and frontend

1. Review and apply the additive IdentityRefreshTokens migration before deploying
   the API. The host does not automatically migrate production databases.
2. Old sessions without a refresh cookie require one new login.
3. PR #24's auth client already matches the endpoint and LoginResult shape.
   Enable NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true when building that frontend
   after the backend migration/API are deployed; Next.js public variables are
   build-time values. Preserve same-origin credentials and in-memory access tokens.
4. Browser refresh should run once for concurrent failed requests. Cross-tab
   refresh races deliberately fail closed: coordinate tabs or require re-login.
5. Rebuild generated frontend types using the updated docs/api/openapi.json.
6. Verify login, page reload, expired access token, logout and password change
   against staging using a non-production test account. Local SQL tests do not
   replace staging acceptance.

Rollback: deploy the prior API while leaving the additive table in place. Existing
access sessions remain bounded by their expiry; the prior API cannot refresh them.
Do not drop the table while any new API instance is still using it. No automatic
database downgrade is part of this change.

## Git

Work stays on feature/identity-refresh-token. Do not push main/develop directly.
After local checks, push this branch and open a PR into develop, request Phuong's
review and wait for both required CI jobs. CI's SQL filter includes all SqlServer
test classes, including refresh and Phuong's week-one tests. No push or remote PR
is performed by the agent. A pre-sync stash is retained as a recovery copy.
