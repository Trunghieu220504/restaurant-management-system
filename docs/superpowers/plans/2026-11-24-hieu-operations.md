# Hieu operations implementation plan

Goal: HIE-4.2, HIE-4.4, HIE-6.1, HIE-5.3 in order, separate PRs to develop with Vietnamese descriptions and reviewers.
Architecture: Reuse module transactions, Outbox, guest access and incident tables.
Spec: docs/phan-cong-hoan-thien-du-an.md and docs/phan-cong-chi-tiet-7-tuan.md.

- [ ] HIE-4.2: authorization tests; transactional ticket events; host SignalR hub; reconnect snapshot; commit/rollback/retry SQL tests; documentation and CI.
- [ ] HIE-4.4: invite/exchange/revocation review; public menu; expired/revoked/wrong-session tests.
- [ ] HIE-6.1: incident decisions/remake without new revenue; prohibit serving superseded attempts; concurrency and Billing contract tests.
- [ ] HIE-5.3: sensitive command inventory; atomic audit and Shared day locks with rollback tests.

Review focus: current permissions, credential confidentiality, idempotency, stale event versions after reconnect, failed delivery retries. Skipped SQL tests are not SQL verification.
Before every push: required restore/build/test/format, diff review. After PR creation: request human review and inspect CI/conflicts. Do not merge without the required review.
