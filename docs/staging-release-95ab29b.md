# Approved develop release 95ab29b — HAI-2.5 preparation

This is one fixed release, **not completed automatic CD or week-three acceptance**.
Source: merged develop `95ab29baa09949f88dd09d106de0834108e97670`,
[successful build/publish](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44462379960).
Only a member merges PRs; the agent must never merge or enable auto-merge.
Coordination and remaining dependencies are tracked in [#70](https://github.com/Trunghieu220504/restaurant-management-system/issues/70).

This supersedes the prepared `a33acbf` release: Trung Hieu merged #57, #68 and #69 into develop before
deployment, including the Catalog wrapped-deadlock fix/regression and week-one QA tooling. This
release is frozen at the successful merged build above. The old fixed helper cannot select another
revision. An operator must install the replacement below; do not deploy the superseded helper.
Seating and Sales Infrastructure are byte-for-byte unchanged from `a33acbf`, so the
canonical SQL checksums remain the same. CI regenerates them from the new exact revision.

## Immutable inputs

| Input | SHA256 |
| --- | --- |
| API image index, ghcr.io/trunghieu220504/restaurant-api | `eb136dfcb81fbbaa23bafbc524e0bb7d776bed3eef260e6b64fb8f80915376b5` |
| Web image index, ghcr.io/trunghieu220504/restaurant-web | `1f61ddcf33491a616c288eff1fabfaf15102e89701cc4271977def3eada3dd44` |
| seating.sql | `574e35a3dbbad08afff2e76d0ac9d8ee85a9ec2b8400df06f3350b27817c79a2` |
| sales.sql | `458b581c151c6eafb96c92513b5b29b9d6b37f80281906b8ff1bfef2585068af` |

Migrations are additive (new Seating idempotency table and initial Sales schema). Regenerate SQL
from that exact commit, using the Infrastructure project as both project and startup project:
`dotnet ef migrations script --idempotent --context SeatingDbContext --project src/Modules/Seating/Restaurant.Modules.Seating.Infrastructure --startup-project src/Modules/Seating/Restaurant.Modules.Seating.Infrastructure --configuration Release --output artifacts/release-95ab29b/seating.sql`.
For Sales substitute `Sales`, `SalesDbContext` and `sales.sql`. The design-time factory requires
`ConnectionStrings__Restaurant`; script generation can use an offline dummy local connection.
Do not put a staging connection secret into command history.
These hashes use UTF-8 BOM and CRLF as generated on Windows. The tooling workflow regenerates
from the exact merged revision on Linux and canonicalizes those bytes before comparing checksums.
Its syntax/guard/ShellCheck/credential checks do not exercise a live deploy or prove rollback success.

## Operator bootstrap and bounded operation

The dirty operator checkout is not reset or pulled. Existing live Compose files/environment are
snapshotted into root-owned mode-0700 directories; neither a mutable deploy-owned shell nor mutable
Compose input is used by the sudo helpers. SQL files must match the fixed checksums above.
The runtime script only accepts this exact release and fixed service names.

An operator reviews [deploy](../scripts/restaurant-deploy-95ab29b.sh),
[account provisioning](../scripts/restaurant-staging-create-accounts.sh) and
[installer](../scripts/install-approved-staging-release.sh), then runs on the VPS:

```bash
sudo bash /home/deploy/restaurant-release-95ab29b/install-approved-staging-release.sh
```

This installs and validates the **plan only** and grants exactly two root-owned, no-argument helpers.
It does not grant a shell, arbitrary Docker access, repository write access or a configurable release.
After bootstrap the authorized agent can run:

```bash
sudo -n /usr/local/sbin/restaurant-deploy-approved-95ab29b
```

The account helper was already run successfully on 19 November 2026; do not run it again.
It deliberately rejects existing accounts instead of overwriting passwords. After deployment,
repeat only the private login verification and the authenticated read-only browser smoke.

Deployment locks against concurrent runs, creates COPY_ONLY/CHECKSUM backup and VERIFYONLY,
restores into a unique `RestaurantReleaseVerify_...` DB without REPLACE, applies both SQL scripts
twice on that copy, CHECKDBs it and tests the new API's readiness against it using an unpublished
probe container. Scratch DB/container cleanup is bounded to those exact generated identifiers.
Then it applies additive migrations to staging and recreates only API/Web, tests/reloads Nginx and
verifies HTTPS/readiness, image revisions, and unchanged SQL/Nginx container IDs. Errors after an
image switch trigger restoration of the recorded previous local image IDs and readiness checks.
Schema is retained; there is no automatic migration downgrade or staging database restore.
Private backup/logs and the current pinned image state remain root-only on the VPS.

Two new accounts are fixed to `staging.manager` / Manager and `staging.staff` / Staff.
The generator uses Identity's actual password hasher and self-verifies both hashes. Only hashes/IDs
go to the provisioning payload. Any existing ID, username or email collision stops without changing
an existing account. Both insertions share a SERIALIZABLE transaction after backup verification.
There is no Admin bootstrap, role-permission expansion or SalesDay mutation.
Passwords stay in an ignored private Windows folder and are delivered only to Hải after login/role
checks. Generated credentials are NOT usable until provisioning and actual login have passed.
After successful provisioning, run [account verification](../scripts/Test-StagingAccounts.ps1) with
`-CredentialFile` pointing to that private file. It sends credentials only to the fixed HTTPS staging
origin, checks each exact role/least-privilege permission, secure refresh cookie, refresh, logout and
revocation, and prints no secret. Only after both checks pass does it mark the file LOGIN_VERIFIED.

## Evidence and remaining work

Prepared locally: locked restore/build, 5 architecture tests, 150 non-SQL tests (outside the Windows
EventLog sandbox restriction), regenerated/checksummed SQL, Bash syntax checks and self-verified
password hashes. These do not substitute actual VPS plan/restore/migration/rollback/deploy evidence.

Verified on the live staging API at 04:56 UTC on 19 November: both dedicated accounts were inserted
atomically after COPY_ONLY/CHECKSUM backup and VERIFYONLY. Private evidence remains at
`/var/backups/restaurant-staging-accounts/20261119T045630Z-219825`. Both accounts passed real login,
exact roles/permissions, secure cookie, refresh, logout and access-token revocation. The private
credential file is now `LOGIN_VERIFIED`; no passwords, hashes, tokens or connection strings are in this PR.
Duy's existing read-only browser tests passed separately with each account: readiness and real
login/current SalesDay, 2/2 per account (4/4 total). The write test was not selected: no day was opened,
closed or reset. These checks exercised the still-running `7de77d4` application, not the prepared release.

Still required: diagnosing the failed live deploy, completing deployment and repeating account/browser checks,
a controlled rollback exercise, restricted SSH CD key/GitHub environment secrets,
and automatic CD execution. R2 credentials/buckets and daily backup + isolated restore remain separate
HAI-2.2/2.6 work. Financial UI is currently a mock; full HAI-3.2 depends on owners' actual Sales/Billing APIs.
Deployment and rollback are not marked PASS by these account/browser results.

## First live deployment attempt

The operator installed the fixed `95ab29b` root helper, then the agent invoked it once. The attempt
at `20261119T051605Z-220913` exited 1 without a successful deploy result. A subsequent independent
read-only audit confirms API/Web remain on `7de77d4`, all four services are running, internal service
ports remain unpublished, and public readiness returns 200/Healthy. No success or rollback drill is claimed.

The detailed failure logs are root-only. The `deploy` user has no approved read-log command, and
an inherited ERR trap inside a redirected function can send the failure message into the private
log as well. The source now preserves the original error stream on descriptor 8 and reports a
fixed phase label; it never prints the shell command, environment or SQL credentials. This improves
failure reporting but is not a claim that the underlying deploy failure is fixed. The installed
root-owned helper is unchanged until an operator installs a reviewed correction. The precise
failing phase/cause is not yet known; do not guess or rerun deployment.
The prepared [read-only diagnosis](../scripts/restaurant-release-diagnose-95ab29b.sh) reads only a
fixed allowlist of logs from this exact attempt, filters secret-bearing lines, reports current
revisions and scratch-database count, and checks readiness. It performs no database/account write,
deployment, repair or privilege change. Run it in the operator's SSH session and return its output:

```bash
sudo bash /home/deploy/restaurant-release-95ab29b/restaurant-release-diagnose-95ab29b.sh
```

SQL/env payloads, the private probe environment and full container environments are never printed.
The operator subsequently ran this diagnosis: backup/VERIFYONLY, both image pulls and the isolated
restore passed; `verify-seating-1.log` contains SQL Msg 102 on line 1; scratch cleanup completed with
zero remaining scratch databases. Readiness stays Healthy and API/Web revisions remain `7de77d4`.

## Correcting the SQL input transport

The immutable Windows-generated SQL starts with UTF-8 BOM bytes `EF BB BF`. Passing those bytes
directly to Linux ODBC sqlcmd standard input reproduces Msg 102; file encoding metadata is not
handled the same way on that path. The corrected actual `sql_run` strips only the leading BOM
and explicitly selects input code page `i:65001`. Output encoding is left unchanged so numeric
queries and backup file-list parsing retain their prior format. Both SQL files and their checksums
remain unchanged. See [sqlcmd encoding options](https://learn.microsoft.com/en-us/sql/tools/sqlcmd/sqlcmd-utility).

The tooling workflow now uses a disposable SQL Server 2022 Linux container and the actual
`sql_run` function extracted from the deploy script. It requires the old BOM input to fail with
Msg 102, applies real canonical Seating/Sales files twice on both empty and initial-Seating databases,
asserts migration history and unchanged pre-existing row/version, runs CHECKDB, verifies the
Vietnamese Unicode code point and checks that invalid SQL still fails. This is isolated CI evidence,
not a successful staging deployment or proof of an image rollback drill.

After these checks pass, the [bounded repair installer](../scripts/install-approved-staging-sql-fix.sh)
copies only the SHA256-pinned deploy script into its existing root-owned path, retaining a private
backup of the old helper. It checks the old revision, rejects concurrent deploys and runs plan only.
It does not overwrite Compose/environment/SQL files, change sudo rules or run migration/account code:

```bash
sudo bash /home/deploy/restaurant-release-95ab29b/install-approved-staging-sql-fix.sh
```

The agent can then rerun the existing no-argument deployment helper and verify the result.
