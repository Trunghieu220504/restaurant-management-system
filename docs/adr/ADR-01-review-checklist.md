# HIE-1.2 review checklist

This branch proposes ADR-01 and provides an additive foundation prototype.
Do not mark the ADR Accepted or HIE-1.2 Done until Phuong and Hai review it.

## Actual scope

- Framework-free BusinessError, IdempotencyKeys, IntegrationEvent and IOutboxWriter.
- SQL Server day-lock helper with caller-owned connection and transaction.
- Optional Contracts purity checks and exact project-path validation.
- No changes to current module APIs, data model, migrations or business logic.
- No runtime consumer, Outbox storage/worker, cross-module adapter or automatic deployment.

## Review requests

- Phuong: approve resource naming and lock order, opening lock separation, timeout
  behavior, and idempotency compatibility with SalesDay. Run the SQL evidence on
  your machine using the companion guide.
- Hai: verify CI selects the new SqlServer tests; check required status names,
  artifacts and timeout reporting. No new NuGet package or runtime configuration.
- Hieu: introduce an explicit dependency allowlist entry only when adding a real
  Contracts consumer. Current module reference policy remains strict.

## Validation and release

Historical local result (12/11/2026): 13 new foundation tests passed, 3 architecture tests
passed. Full integration run: 92 passed, 2 failed during Identity database setup
with SQL command timeouts, zero skipped. Locked restore and format checks passed.
This is not a green full regression result; rerun on a stable isolated SQL test
instance before merge. Code and documentation remain uncommitted for review.

Run locked restore, build, format verification, full tests with a dedicated SQL
Server test connection, and git diff --check. Report passed/failed/skipped counts
and the exact SHA. Connection failures must not be described as business failures
or silently ignored. See AGENT_PROGRESS.md for actual local results.

Current evidence (14/11/2026): full SQL regression reached 99/99 integration and
3/3 architecture tests, zero skipped; the prior failures are historical, not
the latest result. See `../shared-foundation-handoff.md` for the reviewed run.

Create a PR into develop after review of local changes; request Phuong and Hai.
The user authorized commit, push and PR creation on 14/11/2026. Do not deploy the feature branch to staging. Existing
database schema does not change; no migration or rollback script is needed.
