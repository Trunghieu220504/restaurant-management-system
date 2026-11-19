# HAI-3.1 — Audit contract for team review

Status: **DRAFT, not registered at runtime and not business acceptance**. Based on develop
`a33acbf`; coordination is [issue #70](https://github.com/Trunghieu220504/restaurant-management-system/issues/70).
Hải owns Audit. Hiếu and Phương review this small contract before their modules depend on it.
Do not add new permission codes, cross-module infrastructure references, or Sales/Billing calls in this PR.

## Proposed write boundary

[IAuditLog](../../src/Modules/Audit/Restaurant.Modules.Audit.Contracts/IAuditLog.cs) accepts the pure
[AuditEntry](../../src/Modules/Audit/Restaurant.Modules.Audit.Contracts/AuditEntry.cs) message.
No EF, SQL, HTTP, connection, transaction or serializer types cross this boundary.

| Field | Proposed validation |
| --- | --- |
| EventId | Non-empty GUID; unique persisted key; generated once at the source and reused on retry |
| OccurredAtUtc | UTC offset zero; source operation time, preserved on redelivery |
| ActorId | Authenticated account ID, nullable only for an explicitly identified system action |
| SourceModule | Approved module name, maximum 40 characters |
| Action / EntityType | Stable owner-agreed codes, maximum 100 / 100 characters |
| EntityId | Stable identifier as text, maximum 100 characters |
| BusinessDayId | Optional GUID, included for day-scoped operations |
| Reason | Optional bounded business reason, maximum 512 characters; never a raw request/response |

Append-only: no edit/delete API. Exact duplicate EventId + payload is a no-op; a different payload
with an existing EventId fails. No credentials, password hashes, cookies, JWTs, payment credentials,
customer contact snapshots, full headers, SQL or exception payloads are accepted as audit metadata.
The server derives the actor from validated authentication; the public client never selects it.

## Transaction decision required from Hiếu and Phương

An independent `SaveChanges` in Audit followed by a business rollback would produce a false record;
a business commit followed by an Audit failure could lose the record. The interface cannot solve that.
Agree one durable producer integration before shipping callers:

1. Source transaction stores an Outbox audit event; dispatcher delivers to this idempotent sink after
   commit. Inbox/EventId uniqueness prevents duplicate audit records. Outbox delivery is currently
   absent on develop and must not be claimed implemented by this contract.
2. An explicitly reviewed shared-transaction adapter enlists the Audit context in the same SQL
   transaction without exposing persistence types through this public contract.

No fire-and-forget producer calls and no suppression of an audit write failure. The chosen strategy
needs actual SQL tests for business rollback, delivery retry, duplicate and conflicting EventId.
Hiếu owns the Sales call and Phương owns the Billing call; Hải supplies/reviews the sink and evidence.

## Proposed query HTTP contract (not implemented)

`GET /api/audit/logs`: authenticated **Manager only**, with live active-account validation.
Proposed filters: `actorId`, `entityType`, `entityId`, `fromUtc` (inclusive), `toUtc` (exclusive),
`page` (default 1), `pageSize` (default 20, maximum 100). Reject invalid ranges, identifiers and bounds.
Sort by `OccurredAtUtc DESC, EventId DESC` to make ordering stable. Response contains `items`, `page`,
`pageSize`, `total`; each item has the bounded message fields and server `recordedAtUtc`.

Hiếu must confirm whether Manager-role authorization is sufficient or a new dedicated permission
is wanted. Do not silently reuse `reporting.read` or change the existing 26-code catalogue.
Expected responses: 200, invalid query 400 ProblemDetails with a stable code, unauthenticated 401,
non-Manager 403. No public write endpoint. The implementation PR must export real OpenAPI and
coordinate generated-client updates with Bảo Anh/Duy; this proposal does not add invented paths to it.

## Follow-up acceptance (not completed by this draft)

- InitialAudit migration/table `audit.NhatKy`, unique EventId and indexes for actor/entity/time filters.
- SQL Server tests: round-trip, filters/paging, duplicate/no overwrite, transaction rollback semantics.
- API tests: Manager permitted; Staff/Admin-only account denied; revoked/disabled sessions rejected.
- At least one real sensitive Sales action and one Billing action deliver audit entries, by their owners.
- Idempotent migration twice on an isolated restored DB, staging deployment and independent review.
- No destructive reset/seed or real financial operation merely to manufacture acceptance evidence.
