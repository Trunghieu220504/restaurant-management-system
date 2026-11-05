# Identity authentication approved design

Four APIs: login, me, logout current session, change password (revokes all).
JWT expires after 15 minutes; a database LoginSession is required on every
authenticated request. Session expiry/revocation, user IsActive, lockout and
SecurityStamp are checked; role claims are replaced with current database roles.
No refresh token, public registration, cookies or automatic migration in scope.

Login is username-only; bounded input, generic credential failures, Identity
five-failure lockout and process-local IP fixed-window rate limiting. No trusted
forwarded-header configuration is invented. Unknown usernames use a dummy hash
verification to reduce obvious timing differences. MFA-enabled users fail closed.

Application defines plain contracts; Infrastructure handles SQL/UserManager;
Presentation maps endpoints; Host issues/validates JWT through Application ports.
All credential/session mutations serialize on a transaction-owned per-user SQL
application lock. Token issuance is inside the transaction before commit; changes
to password update the stamp and revoke all sessions atomically. Already-running
requests are not cancelled by logout; subsequent authentication fails.

Database errors fail closed and are not interpreted as invalid credentials.
No password/token logging, response caching or secrets in tracked files.
Permission data is returned but does not imply operational APIs are implemented.

Ruling: add IsActive as a mapped shadow property on current IdentityUser<Guid>,
default true, instead of changing all existing stores/bootstrap generic types.
SQL tests exposed EF's bool default sentinel inserting false for a new shadow
property. Explicit HasSentinel(false) now lets SQL supply true on creation;
updating an existing account to false remains supported and tested. Creating a
disabled account in one INSERT is not an API supported in this increment.
Ruling: a new Session migration is separate; initial Identity migration immutable.
