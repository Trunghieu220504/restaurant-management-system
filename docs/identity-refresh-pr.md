# feat(identity): implement rotating refresh tokens (HIE-1.3)

## Behavior

Users can renew their 15-minute access token using a Secure, HttpOnly, SameSite
refresh cookie with a fixed seven-day lifetime. Each successful refresh rotates
the secret; reusing a consumed secret revokes the session immediately. Only token
hashes are persisted. Logout can revoke via cookie when the access token expires;
password changes revoke every session. Token issuance failure rolls back rotation.

## Integration

Based on develop 258d90a. Preserves LoginResult's JSON shape and matches PR #24's
same-origin auth client. Updated OpenAPI and API contract. The SQL CI filter now
includes every SqlServer test class, including refresh and Phuong's tests, while
keeping the required GitHub job name unchanged.

## Deployment

Apply the additive IdentityRefreshTokens migration before API rollout. Rebuild the
frontend with NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true only after the endpoint
is available. Old sessions require a new login. Coordinate refresh across tabs;
duplicate requests with a consumed cookie deliberately revoke that session.
No staging/production changes are performed in this task. See
identity-refresh-handoff.md for browser acceptance and rollback considerations.

## Review

Verified locally: 83/83 tests pass with real SQL Server (81 integration, 2
architecture; zero skipped), including 14 refresh tests. Locked restore,
whitespace verification and git diff --check pass. EF reports no pending model
changes. OpenAPI was exported from the running API, not hand-written.

Target develop; request Phuong's review and wait for both required CI jobs.
Local verification results are recorded in AGENT_PROGRESS.md. Staging/browser
acceptance remains required; HTTP/SQL tests do not substitute for that acceptance.
