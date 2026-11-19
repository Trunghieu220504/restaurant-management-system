# HIE-1.2 implementation handoff (2026-11-14)

Status: PR #36 open into develop; implementation synchronized and locally verified.
Requested reviewers: phuongdtrai (Phuong) and Hai249 (Hai). Auto-merge is disabled.
Implementation HEAD fd5ef01 passed push and pull-request CI; this handoff update
is documentation only and must retain green CI on the final PR head.
ADR-01 was merged in PR #29 and the owner confirmed approval. This is not a
request to approve the architecture again; implementation still needs review.

## Scope verified

- Existing BCL-only BusinessError, IdempotencyKeys, IntegrationEvent and IOutboxWriter.
- SQL Server day-lock helper using caller-owned connection/transaction.
- Architecture checks for optional pure Contracts and explicit project paths.
- Added rollback/cancellation and invalid-argument coverage; checked framework isolation.
- Imported the four C03 tests already merged in PR #28, unchanged from develop.
- Added scripts/Test-SharedFoundation.ps1 for disposable localhost SQL verification.
- No business API, schema, runtime consumer or Outbox worker changes.

## Verification

- Locked restore: passed on .NET SDK 10.0.300.
- Final synchronized runner: scripts/Test-SharedFoundation.ps1, exit 0 (includes locked restore).
- Release build: 0 warnings, 0 errors.
- Architecture: 3 passed, 0 failed, 0 skipped.
- Integration: 99 passed, 0 failed, 0 skipped, real disposable SQL Server 2022.
- Final results: TestResults/FoundationCurrent/52c13de116e1441888115392977f9f52/*.trx.
- Earlier 95-test full regressions also passed before importing four upstream tests.
- Historical Identity migration timeouts did not reproduce; no Identity runtime
  changes or timeout increases were needed. This does not establish a historical root cause.
- Format verification and git diff checks are recorded in the ledger.
- GitHub PR Backend CI run 43615894018: Build and test success; Identity SQL
  Server tests success. Downloaded SQL artifact was parsed in memory: 43/43
  passed, zero failed/skipped, including all three BusinessDayLockSqlServerTests.
  This is separate from the 99-test local full integration result.

## Git and review boundary

Implementation commit: ad4c42d. The feature branch includes origin/develop
at 6db075c; the only conflict was ADR documentation, resolved without changing
the merged architectural decision or dropping historical ledger entries.
Tracked production backend sources and shared project configuration have no
upstream differences; the only upstream backend test change was the imported
PhuongWeekOneSqlServerTests.cs. Local verification is not a remote CI result
or independent reviewer approval. The pre-sync safety stash remains available.

The synchronized tree passed the zero-skip SQL runner and format verification.
PR #36 is open on the feature branch; wait for independent implementation review.
Do not merge until required CI and reviewer gates are satisfied; squash merge
per CONTRIBUTING.md. No develop/main push, self-approval or auto-merge was used.
No staging acceptance, frontend revalidation or production deployment was done.

## Repeat

Run from the repository root with Docker Engine available:

```powershell
./scripts/Test-SharedFoundation.ps1
```

The script generates a temporary password, exposes SQL only on loopback, restores
previous environment variables and removes only its own container in finally.
It does not use staging credentials or stop unrelated containers.
