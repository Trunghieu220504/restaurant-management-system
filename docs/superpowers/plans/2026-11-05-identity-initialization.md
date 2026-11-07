# Identity Initialization Plan

Approved: Guid Identity keys, five migration-seeded roles and permission catalog.
Admin has identity administration only; Manager has subordinate account management.
Bootstrap runs explicitly via CLI with environment secrets, never HTTP/startup.
Serialize and atomically persist account, role and singleton bootstrap marker.
Reject existing usernames/admins; no password reset or privilege overwrite.

- [x] Add failing model/seed/bootstrap tests.
- [x] Implement catalog, mappings and secure initialization.
- [x] Generate/review migration and idempotent SQL without applying to real DB.
- [x] Run locked restore, tests, format and pending-model check.
- [x] Push feature branch and open draft PR without merging.

Local verification: 35 passed; six real SQL tests require the dedicated CI job.
Docker daemon is unavailable locally. CLI missing-input smoke test returns code 1
without starting a server or printing secrets. No live database/admin was changed.

GitHub draft PR #7 targets develop. Both build/test and real SQL Server jobs
passed on commit 9a2cfbf (runs 41985552236 and 41985640195). The SQL job executes
the six isolated migration/bootstrap tests including concurrent creation and
injected rollback. PR remains draft for review; no automatic merge was performed.
