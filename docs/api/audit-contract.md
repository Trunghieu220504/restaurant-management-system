# HAI-3.1 — Audit runtime and integration handoff

The pure contract was merged in #72. This branch implements the SQL sink, migration and read API for owner review. Audit is not deployed or business-accepted. Coordination: [issue #70](https://github.com/Trunghieu220504/restaurant-management-system/issues/70) and [Outbox/Inbox PR #79](https://github.com/Trunghieu220504/restaurant-management-system/pull/79).

Hải owns Audit; Hiếu owns Sales and Phương Billing producers. No new permission code, cross-module infrastructure reference, producer call, dispatcher or public write endpoint is added here.

## Write boundary

[IAuditLog](../../src/Modules/Audit/Restaurant.Modules.Audit.Contracts/IAuditLog.cs) accepts the pure [AuditEntry](../../src/Modules/Audit/Restaurant.Modules.Audit.Contracts/AuditEntry.cs). Runtime services are scoped; TimeProvider supplies server recordedAtUtc.

| Field                          | Validation                                                                                                                |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------- |
| EventId                        | Non-empty GUID PK; source generates once and reuses on retry                                                              |
| OccurredAtUtc                  | Non-default UTC offset zero; preserved on redelivery                                                                      |
| ActorId                        | Non-empty authenticated account ID; nullable only for SourceModule System                                                 |
| SourceModule                   | Exact module name: Identity, Seating, Catalog, Sales, Fulfillment, Billing, Inventory, SalesDay, Audit, Reporting, System |
| Action / EntityType / EntityId | Nonblank, no surrounding whitespace/control characters, at most 100 characters each                                       |
| BusinessDayId                  | Optional non-empty GUID                                                                                                   |
| Reason                         | Optional, at most 512 characters, no controls; null and empty remain distinct                                             |

Producers derive actor from validated server authentication, never a public client's requested actor. Credentials, hashes, cookies, tokens, contact snapshots and raw HTTP/SQL/exception payloads must not enter metadata; no automatic sensitive-data classifier is claimed.

Identical EventId + payload is a no-op, retaining the first recording time. A changed payload raises AuditEventConflictException without overwriting. SQL uniqueness races reload/compare the committed winner; unrelated persistence errors propagate. Cancellation is forwarded.

InitialAudit creates audit.NhatKy with EventId PK, UTC check and actor/entity/time indexes. Private setters, SaveChanges guards and an UPDATE/DELETE trigger enforce append-only ordinary DML. Privileged DDL/TRUNCATE, retention and separate DB grants are outside this change. No edit/delete API. Migration Down drops this new table; do not use it to roll back a live release with audit history.

## Durable producer integration — pending owners

An independent Audit SaveChanges does not make a business operation atomic with Audit. Calling before business rollback can leave false evidence; calling after commit without durable delivery can lose evidence. No fire-and-forget or suppression of an audit write failure.

Align with Hiếu's #79 instead of creating another dispatcher:

1. Sales/Billing owners persist a stable, versioned audit event in their Outbox in the business transaction.
2. With #79 now merged, a reviewed Audit consumer commits Inbox marker and NhatKy in one transaction using the shared handler. Failed/conflicting delivery must not be acknowledged as success.
3. Owners agree event/action codes and payload mapping. Action strings in tests are fixture data, not approved event names. Billing still needs its own Outbox; #79 currently supplies Sales only.
4. Acceptance requires actual producer rollback/retry/duplicate/conflict tests and at least one sensitive Sales operation plus one Billing operation.

This branch tests Audit transaction rollback only; it does not add Inbox, run a dispatcher or prove Sales/Billing atomicity.

## Read API

GET /api/audit/logs requires an active authenticated Manager role using live Identity validation. Staff/Admin-only users receive 403, revoked/disabled sessions 401. No implicit Admin bypass or permission catalogue edit.

| Query                 | Meaning                                                                                                                 |
| --------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| actorId               | Optional non-empty dashed UUID (D format)                                                                               |
| entityType / entityId | Optional bounded identifiers; all filters combine with AND                                                              |
| fromUtc / toUtc       | Optional ISO 8601 UTC, Z or +00:00, optional 1–7 fraction digits; inclusive start/exclusive end; start must precede end |
| page / pageSize       | Defaults 1/20; positive decimal integers, pageSize at most 100; reject offset overflow                                  |

Query names are case-sensitive. Empty, unknown, repeated, malformed and out-of-range parameters return 400 application/problem+json with code audit.query.invalid. Inputs are strings in OpenAPI because custom parsing supplies that consistent failure response. Authentication/authorization run first.

200 returns items/page/pageSize/total with Cache-Control: no-store. Each item includes the bounded message plus recordedAtUtc. No-tracking projection, ordering OccurredAtUtc DESC then EventId DESC, count and bounded Skip/Take. This is a live log, not a snapshot across requests; new appends can move offset pages.

Use the [HTTP examples](../../src/Modules/Audit/Restaurant.Modules.Audit.Presentation/Audit.http) against a local API. No live credentials are committed.

## OpenAPI/frontend handoff

New operation/schema graph is exported from the real Development API. Other owners' curated headers/descriptions, paths and server URL are preserved; changed shared schemas stop export for owner review.

```powershell
dotnet run --file scripts/Export-AuditOpenApi.cs --configuration Release
npm --prefix web run gen:api
```

Commit [openapi.json](openapi.json) and [generated schema](../../web/src/lib/api/generated/schema.d.ts) together. Bảo Anh/Duy review client changes and handle their UI integration; no financial mock counts as live acceptance.

## Evidence and remaining acceptance

Local Release: zero warnings/errors. SQL-enabled full suite: 278 integration + 5 architecture tests passed, 0 failed, 0 skipped (19 Nov), including 10 Audit SQL/API and 3 contract tests. [Detailed evidence](../evidence/week-three/audit-runtime-2026-11-19.md).

Pending: CI, independent review/merge, shared delivery adapter using merged #79, owners' Sales/Billing producers, migration on isolated restored staging DB, deploy and live UI/business acceptance. No staging reset, financial operation or password reset was performed for these tests.
