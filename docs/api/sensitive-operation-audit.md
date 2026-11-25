# HIE-5.3: Transactional business audit

IBusinessAudit is a framework-free producer contract in Shared.Contracts. Infrastructure invokes it after its
business SaveChanges and before Commit. The Host resolves the source context and enlists AuditDbContext in that
same SQL transaction, following the existing InventoryAudit adapter pattern. Missing producer transaction,
missing Audit schema or sink failure fails the command: no best-effort fallback and no background loss window.

Apply the existing Audit migration before deploying this version. No new schema or secrets are required.
Identity/Seating add a Shared.Contracts project reference; updated lockfiles carry those graph changes.

## Sensitive operation coverage

| Producer | Operations/actions | Verification |
| --- | --- | --- |
| Identity accounts | account.create, account.roles, account.activate/deactivate, account.reset-password | IdentityBusinessAuditSqlServerTests: account writes, no-ops, failed audit rollback |
| Identity authentication | login.success, known-account login.failure, refresh.rotate, refresh.replay-revoke, logout, password.change | IdentityBusinessAuditSqlServerTests: session IDs, repeat revocation, failed login/password audit rollback |
| Seating catalog | area.create/update/archive; table.create/update/archive | SeatingBusinessAuditSqlServerTests; BusinessAuditSqlServerTests real sink |
| Seating sessions | MO_PHIEN, CHUYEN_BAN, DONG_PHIEN; closure includes grant revocation | SeatingBusinessAuditSqlServerTests: replay and rollback; real sink integration |
| Seating guests | qr.invite, qr.exchange | SeatingBusinessAuditSqlServerTests: no code/token/hash metadata, day gate; real sink checks no secret persistence |
| Sales orders/batches | sales.order.created/updated/closed; sales.batch.added/confirmed/rejected/cancelled | BusinessAuditSqlServerTests and existing Sales/Billing SQL fixtures |
| Sales portions | sales.portion.edited/cancelled | Existing transactional command path and SQL regressions; uses same actor/day/reason metadata as its Outbox fact |
| Fulfillment tickets | fulfillment.ticket.created/bat_dau/san_sang/giao_mon/huy_mon | Operations/incident SQL tests and source transaction checks |
| Fulfillment incidents | fulfillment.incident.reported/rejected/lam_lai/huy_van_thu/huy_giam_nghia_vu | FulfillmentIncidentSqlServerTests: exact audit count under replay, atomic Billing rollback |

Records contain business IDs, current actor, business day, action and bounded reason. Account/session records
contain GUIDs, never usernames, email, credentials, security stamps, raw requests, token hashes or tokens.
Known-account login failures identify the attempted account; unknown names are not persisted in business audit.
Guest exchange and automated ticket creation have no staff actor and use SourceModule=System, as required by the
existing Audit domain. Other missing actors are resolved from the authenticated HTTP principal where available.
Reasons are stripped of control characters and capped at 512 characters for the existing Audit schema; full
validated business reasons remain in the producer's own tables. Technical rows (Outbox/Inbox/leases) are not
separate sensitive business actions. Deployment bootstrap remains an infrastructure provisioning operation.

Idempotent replay branches return before AppendAsync. Repeated role/active settings and already-revoked sessions
do not produce another change event. Each real source transaction may contain several records (order and batch);
all roll back together if pricing, Billing, concurrency, constraints, audit or commit fails.

## Shared day-lock inventory

| Writes | Gate and order |
| --- | --- |
| Seating open/transfer/close | IBusinessDayDirectory -> Shared day -> table/session, existing behavior |
| QR issue/exchange | Immutable session-day lookup -> Shared day -> session -> invitation/grant, added here |
| Sales create/add/confirm/reject/cancel batch | ISalesDayGate -> Shared day -> order; existing behavior |
| Sales portion edit/cancel | IStaffPortionDay -> Shared day -> order; existing behavior |
| Order close | Shared day -> order -> fulfillment checks -> invoice; existing behavior, now audited |
| Fulfillment state commands and batch consumer | IFulfillmentIncidentContext -> Shared day -> order -> ticket; added here |
| Fulfillment report/decision | Same day/order gate from HIE-6.1, then ticket and optional invoice |
| Identity and table/area catalog | No business-day amount/state ownership, so no day gate |

Commands cannot write to CHO_CHOT or DA_CHOT. A previously committed idempotent result remains replayable where
the service already supports it. Delayed fulfillment events retry through the existing dispatcher if their day
is no longer writable; they must not silently change closed-day state.

Isolated module tests explicitly inject TestBusinessAudit. Production AddRestaurantModules always registers the
real mandatory writer. Full Host SQL fixtures migrate Audit alongside business schemas.
