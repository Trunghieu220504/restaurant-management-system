# HIE-6.1: Incidents and remakes

POST `/api/fulfillment/tickets/{id}/incidents`: `{version, station, reason, requestKey}`.
GET `/api/fulfillment/incidents`: pending incidents for the caller's permitted stations.
POST `/api/fulfillment/incidents/{id}/decision`: `{version, decision, reason, requestKey}`.

Report/read require `fulfillment.incidents.report`; Kitchen and Bar remain station scoped.
Decision requires Manager and `fulfillment.incidents.approve`. Every write requires a UUID request key,
the rowversion returned by the relevant resource, an actor and a nonempty reason (up to 1000 characters).
The decision's version belongs to the incident, not the ticket.

Decisions:
- `LAM_LAI`: original attempt becomes `LOI`; a new `MOI` ticket keeps the same portion and station,
  increments attempt, references the prior ticket and approved incident. No Sales portion or invoice line is added.
- `HUY_VAN_THU`: ticket becomes `DA_HUY`; original charge is retained.
- `HUY_GIAM_NGHIA_VU`: ticket becomes `DA_HUY`; Billing writes a negative adjustment equal to the
  original invoice line, linked to this incident, on the order's open business day. No automatic refund.
- `null`: reject the incident; leave the original ticket state unchanged and unblock its commands.

Only an open order on an open day can receive reports/decisions. Shared day lock and order lock precede
ticket and invoice locks. A pending incident blocks every ticket transition. A superseded/cancelled ticket
cannot be reported again or served. A served portion can be reported while its order/day remain open.
Retries return the current incident without repeating effects; a changed request under the same key conflicts.
Report keys are globally unique; decision keys are actor scoped. Billing uses an incident-derived key.

If the original line has zero value, use bookkeeping cancellation (`billing.incident.no_obligation`).
If waivers/other adjustments leave less obligation than the original line, the full reduction is refused
with `billing.amount.exceeds_available`; no partial decision is committed.

The incident decision, replacement/cancelled ticket, Billing adjustment and ticket Outbox events commit or
roll back together. Rejected decisions do not emit ticket changes. Read the pending incident list to refresh it.
`LOI` tickets are terminal tombstones for station snapshots, like `DA_GIAO` and `DA_HUY`.

No migration is required: existing incident/remake constraints and request storage are used.
Frontend incident screens remain the frontend owner's integration work. Staging acceptance is not implied by unit tests.
