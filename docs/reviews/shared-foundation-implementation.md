# Shared Foundation Implementation Review

Date: 2026-11-14. Scope: HIE-1.2 implementation against merged ADR-01 (#29).
Reviewer: Codex self-review; not an independent teammate approval.

## Findings

- P2, fixed: the SQL runner trusted dotnet exit 0 without rejecting skipped or
  missing test suites. It now uses a unique output directory, requires both TRX
  files and requires positive totals with all tests passed and zero skipped.
- P2, documentation: old handoff/checklist statements described implementation
  as uncommitted and ADR as awaiting approval. Historical evidence is retained
  and current approval, synchronization and PR status are recorded separately.

No blocking runtime correctness issue found in this review. No runtime module
uses the new projects yet; this PR is additive infrastructure, not production
Outbox delivery or integration of every business writer with the day lock.

## Checks

- Contracts contain only BCL types and no package/framework/project references.
- Lock resources are day-scoped, parameterized and transaction-owned; connection
  and transaction stay caller-owned. Timeout/cancellation cannot continue work.
- Existing SalesDay API, opening lock, idempotency storage and schema stay intact.
- Module reference allowlists stay strict; optional Contracts do not grant
  blanket cross-module access. No worker, broker or migration is introduced.
- CI selects new SQL tests through FullyQualifiedName~SqlServer.
- No credential, local database password or generated test artifact belongs in PR.

## Remaining gates

Run locked restore, Release build, SQL regression and format checks on the
synchronized tree. Require green GitHub CI and teammate implementation review
before squash merge into develop. No staging acceptance is claimed.
