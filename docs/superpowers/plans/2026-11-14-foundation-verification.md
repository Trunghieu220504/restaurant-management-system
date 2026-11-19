# Shared Foundation Verification Plan

Goal: finish existing HIE-1.2 against merged ADR-01 (PR #29).
Scope: BCL contracts, SQL day-lock helper and architecture tests only.
No runtime consumers, endpoints, migrations, worker or permission policies.

- [x] Confirm ADR-only PR and inspect local code and historical TRX.
- [x] Extend rollback, cancellation and boundary tests.
- [x] Run locked restore, Release build and full SQL regression (3 architecture, 99 integration, zero skip).
- [x] Verify source compatibility with current develop; import its four C03 tests without overwriting work. Git branch synchronization remains a separate pre-PR step.
- [x] Run format/diff checks and record exact results in the handoff and ledger.

Use disposable local SQL only. Preserve existing work; no commit/push/merge.
