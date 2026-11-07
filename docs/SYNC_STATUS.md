# Synchronization and design status

## Reading order

1. `superpowers/specs/2026-11-05-restaurant-mvp-scope.md`: approved scope.
2. `superpowers/specs/2026-11-05-database-revised-design.md`: proposed logical
   model and C01-C06 approval gates, NOT production DDL.
3. `report/deliverables/Bao_cao_de_tai_quan_ly_nha_hang_v2_2.docx`: report
   snapshot. Its 13 images are preserved but have NOT been visually verified.
4. `backlog-va-phan-cong.md`: work allocation; scope above wins on conflicts.

Backend is a foundation: ten modules, including SalesDay instead of Workforce.
Passing scaffold tests does not validate order/payment/stock business workflows.
Identity currently uses Guid keys. Business migrations must wait for approved
contracts, explicit EF mappings and transactional integration tests.
Identity now has an initial migration/admin bootstrap plus an additive session
migration and login/me/logout/password-change APIs. See identity-authentication.md
for revocation behavior, tests and limits; no production DB is auto-migrated.

Do not regenerate this report from old report scripts or treat historical ERDs
as migration-ready. Only selected current documents were imported; old SQL,
temporary QA files, local tools and earlier reports were deliberately excluded.

## Branch synchronization

This synchronization branch preserves existing local changes without rewriting
history. The remote develop branch was still at the initial README; main already
contained the backend foundation. Bootstrap develop from this reviewed branch,
then promote develop to main through a separate PR, following CONTRIBUTING.md.
No force push and no automatic merge are required.
