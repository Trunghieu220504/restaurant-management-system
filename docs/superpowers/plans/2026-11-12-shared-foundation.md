# HIE-1.2 implementation plan

Goal: provide reviewable ADR-01 and a tested, additive shared foundation without
changing current business endpoints or implementing an Outbox worker.

Spec: ../../adr/ADR-01-shared-foundation.md (Proposed; Phuong/Hai review required).

## Tasks

- [x] Read SalesDay transaction/idempotency and existing architecture tests.
- [x] Draft ADR with alternatives, ownership, compatibility and acceptance gates.
- [x] Add architecture coverage for optional pure Contracts projects and explicit dependencies. No cross-module references enabled without a consumer review.
- [x] Add small shared primitives for validated error codes, idempotency keys and event envelopes.
- [x] Add SQL day-lock helper accepting caller-owned transaction; test actual locking on SQL Server.
- [ ] Run locked restore, format, full backend and SQL tests; document actual results.
- [ ] Prepare handoff and update progress; user owns push and remote PR creation.

## Verification 2026-11-12

Locked restore and whitespace verification passed. Architecture: 3/3 passed.
Targeted new foundation suite: 13/13 passed including two SQL lock tests.
Final complete integration run: 92 passed, 2 failed, zero skipped; both failures
are existing Identity tests timing out in database creation/migration during
InitializeAsync, not assertion failures. See TestResults/Foundation/verified_*.trx.
Do not describe this as a green full regression run. No commit or push performed.
Phuong/Hai review and a complete green regression run remain outstanding.

Constraints: preserve previous documentation changes; no production migration or
deployment; no direct main/develop writes; do not claim team approval or production
Outbox delivery. Test failures must precede behavior fixes. Do not weaken tests
to permit unrestricted cross-module references.
