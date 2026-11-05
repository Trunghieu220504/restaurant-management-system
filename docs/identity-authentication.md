# Identity authentication APIs

Four routes under /api/identity:

| Method | Route | Input | Result |
|---|---|---|---|
| POST | /login | userName, password | accessToken, expiresAt, account |
| GET | /me | Bearer token | current id, userName, email, roles, permissions |
| POST | /logout | Bearer token, no body | 204; current session revoked |
| POST | /change-password | currentPassword, newPassword | 204; all sessions revoked |

Send Authorization: Bearer <accessToken> on protected requests. JWT is valid for
15 minutes only. There is NO refresh endpoint in this increment: sign in again
after expiry or password change. No public registration or cookie authentication.
Credentials/token DTOs redact ToString; never enable request-body logging for
these routes. All Identity responses set Cache-Control: no-store.

Bad input is 400, oversized body (>16 KiB) is 413, invalid/expired/revoked session
is 401, missing operational role is 403, rate limit is 429. Credential failures,
locked/disabled/unknown users and MFA-enabled accounts get the same login error.
This increment does not implement MFA; MFA-enabled accounts fail closed.
Database failures return a generic 500 and never authenticate a user.

Password policy remains at least 12 characters with upper/lowercase, digit and
nonalphanumeric. Five wrong passwords trigger Identity lockout (default five
minutes). Login rate limit is 10 attempts per minute per direct connection IP;
password change is five per minute per authenticated user. In-memory rate limits
are per process. No untrusted X-Forwarded-For header is accepted. Behind Nginx or
Cloudflare, configure trusted forwarded headers/firewall and edge limits before
deployment; otherwise proxy users share the process-level IP budget.

LoginSession stores IDs, stamp, times, revocation and rowversion, not raw JWT or
refresh token. AspNetUsers.IsActive defaults true; management APIs are future
work. Every protected request checks session ownership, expiry/revocation,
SecurityStamp, activity and lockout in SQL, then rebuilds roles from current
database assignments. Old scaffold test tokens without sid are rejected.
Permissions are returned for client display; this does not implement future
business authorization handlers or the subordinate account management APIs.

Logout revokes only current session. Password change requires current password,
rejects identical/weak replacement and atomically changes password/stamp and
revokes every session. SQL application locks serialize authentication mutations
per user. An already-running request is not cancelled by a later logout; the
next authentication check rejects the revoked session. Failed password changes
do not invalidate otherwise valid sessions.

Apply InitialIdentity first, then IdentityLoginSessions through reviewed migration
SQL. No production database is automatically migrated. The second migration
adds IsActive/LoginSession and does not rewrite the first migration/role seeds.
See identity-initialization.md for secret configuration and admin creation.

Tests include HTTP flow, role refresh, expiry/stamp checks, disabled users,
lockout, rate limiting, simultaneous password changes and transaction rollback.
Real SQL tests use only randomly named disposable test databases, not restaurant
data. Before push, all 64 tests passed locally with SQL Server (zero skipped).
The temporary container was removed. PR #7 is now merged into develop; this
feature fast-forwarded to that base and targets develop directly. No PR merge,
force push or production migration is performed by this change.
