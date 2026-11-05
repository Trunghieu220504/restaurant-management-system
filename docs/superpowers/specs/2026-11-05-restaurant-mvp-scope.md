# MVP scope - 2026-11-05

Approved scope reduction; keep customer QR ordering and sound notifications.
This baseline overrides conflicting MVP scope in older project documents.
Architecture and 10 modules remain unchanged; no business implementation is claimed.

## Keep
- Accounts, sessions and backend authorization; menu/variants/shared options and sold-out flags.
- Tables/sessions/transfers; staff dine-in/takeaway orders and additional batches.
- Customer QR ordering/tracking within open sessions, scoped tokens, rate limits and staff confirmation before kitchen dispatch.
- Kitchen/bar routing, partial completion/serving and manager-approved exceptions/remakes.
- SignalR plus new-work sounds for kitchen/bar and ready-dish sounds for serving. Include mute, browser user activation, visible fallback, event deduplication and no historical sound replay on reconnect.
- Cash and manually confirmed bank transfers. Multiple payments/mixed methods per invoice; each payment belongs to exactly one invoice.
- Manager-approved waivers and amount-based refunds linked to original payments, with limits/reasons/audit and correct payment-obligation adjustment; receipts.
- Ingredients, units/conversions, purchases/prices, basic suppliers, daily counts, material cost and gross profit.
- Sales-day opening/closing, cash reconciliation and basic daily/date-range reports.
- Audit, idempotency, concurrency protection, backups and restore checks.

## Defer
- Cards; one payment covering multiple invoices.
- Item-selection refunds and automatic item-level refund allocation.
- Editing/reopening closed sales days, including the latest. Use audited current-day corrections referencing the original; preserve closed snapshots.
- Advanced suppliers/payables/contracts/ratings; advanced reports/rankings.
- Automatic overdue-dish alerts and detailed operational analytics. Keep visible waiting times and the sounds above.
- Previous exclusions remain, especially attendance, employee shifts/payroll, recipes/per-dish inventory and offline.

## Backlog overrides
- BL10 QR and sound BL13/BL18 stay required.
- BL19 cash/bank only, one payment per invoice, repeated payments allowed.
- BL21 amount refunds only; BL24 current-day corrections, no closed-day editing.
- BL23 basic suppliers; BL27 basic reports. Keep IDs/ownership.

## Design gate
Old SQL/ERDs and the 44-table design are not migration-ready for this scope. Revise payment allocation, defer DongHoanTien/THE and remove closed-day editing.
Finalize the six reviewed contracts before money/stock migrations: obligation adjustments versus refunds; adjustment valuation; closing locks/shared transactions; archived ingredients with stock; current table/token after transfer; signed report results and distinct quantity units. This scope decision does not approve every proposed technical solution.

## Delivery
1. Foundation/accounts/menu/tables/day opening.
2. Staff order/kitchen/serving/SignalR/sound.
3. QR batches/confirmation/reconnect.
4. Collections/waivers/amount refunds/receipts.
5. Purchases/counts/closing/basic reports/backups.
Acceptance includes expired QR sessions, no unconfirmed dispatch, duplicate events, blocked audio with visible fallback, payment/refund limits and concurrent writes during closing.
