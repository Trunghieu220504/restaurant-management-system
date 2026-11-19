# Audit runtime evidence — 19 Nov 2026

Base: develop `a06708e`; branch `feature/hai-week3-audit-runtime-20261119`. This records local evidence; CI, owner review and deployed acceptance are separate.

| Check                           | Observed result                                                                                       |
| ------------------------------- | ----------------------------------------------------------------------------------------------------- |
| Release build                   | PASS, 0 warnings/errors                                                                               |
| SQL-enabled full solution suite | PASS, 278 integration + 5 architecture; 0 failed/0 skipped                                            |
| Audit SQL/API suite             | PASS, 10 tests on SQL Server 2022, generated database only                                            |
| Domain/query contract           | PASS, 3 tests, included in full suite                                                                 |
| Concurrent delivery             | 12 contexts, one row; conflicting payload rejected                                                    |
| Append-only                     | Tracked and bulk update/delete rejected; original retained                                            |
| Migration                       | Fresh fixture migration; idempotent script twice preserves data and trigger                           |
| Transaction                     | Audit rollback removes pending row and retry can commit; no producer atomicity claim                  |
| HTTP                            | Manager 200; anonymous/revoked/disabled 401; Staff/Admin-only/downgraded 403; POST 405                |
| Validation                      | Stable 400 ProblemDetails for malformed/empty/repeated/unknown filters, invalid UTC/ranges/pagination |
| OpenAPI                         | Actual GET metadata; 200/400/401/403, seven query parameters verified                                 |
| Staging                         | Audit NOT deployed; deployed API/Web is 8af7ab3                                                       |

```powershell
dotnet build RestaurantManagement.slnx -c Release --no-restore
$env:IDENTITY_TEST_SQLSERVER = & ./scripts/Get-WeekOneLocalSql.ps1
dotnet test RestaurantManagement.slnx -c Release --no-build --no-restore --logger trx --results-directory artifacts/audit-full-results
```

SDK 10.0.303, VSTest/xUnit 2.9.3. Fixture uses `RestaurantAuditTest_<Guid>` and verifies exact generated database name before cleanup. Raw TRX/logs remain ignored under artifacts. No operational database, password reset, business transaction, token or connection secret is involved.

Shared delivery builds on merged [PR #79](https://github.com/Trunghieu220504/restaurant-management-system/pull/79). [Coordination comment](https://github.com/Trunghieu220504/restaurant-management-system/pull/79#issuecomment-7002892569) requests event/action codes from Hiếu/Phương. No independent Outbox, Sales/Billing caller, Inbox, dispatcher enablement, automatic merge or ruleset change is included.

Frontend handoff regression: generated API types synchronized, TypeScript noEmit PASS, 71/71 Vitest tests PASS. Audit remains unmerged/undeployed; UI acceptance is for runtime 8af7ab3 only.
