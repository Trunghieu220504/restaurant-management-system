# HIE-4.2: Operations realtime

Endpoint: `/hubs/operations` (SignalR JSON protocol, authenticated staff JWT).
Enable the existing dispatcher with `Outbox__Enabled=true`; apply existing module migrations first.
No new schema migration is needed. Keep one API instance until a shared SignalR backplane is configured.

Kitchen sockets automatically join `station:BEP`, Bar sockets join `station:BAR`.
Staff/Manager join both stations and `serving`. Admin alone has no operational access.
Methods: `JoinStation("BEP"|"BAR")`, `JoinServing()`, `JoinOrder(orderId)`, `LeaveOrder(orderId)`.
Order and serving groups require Staff/Manager. A socket has at most 100 subscriptions.
The serving group is a shared queue; the domain has no per-waiter assignment.

`TicketChanged` carries `{eventId, occurredAt, version, payload}` where payload is the existing TicketView.
Version is the ticket SQL rowversion encoded as base64, not the event schema version.
Compare decoded eight-byte rowversions as unsigned big-endian values, never as base64 strings.
The Outbox aggregate key includes the rowversion because its existing version column is an int.
Creation and state changes stage an event before the business transaction commits.
Delivery is at least once: deduplicate eventId; ignore older or equal ticket versions.
Client send errors retry through the existing lease/backoff dispatcher. Offline clients recover from snapshot.
The dispatcher validates current account/session and roles before sending to each connected recipient.
Tokens are accepted from query strings only for WebSocket/SSE on the exact hub route.
JWT expiration closes the connection. Never log access_token or full hub request URLs in reverse proxies.

Reconnect sequence:
1. Register TicketChanged listener and buffer events. Connect, then await `JoinStation` or `JoinServing` completion, plus any `JoinOrder` calls. Repeat on every reconnect. `hub.start()` alone is not a subscription-ready barrier.
2. GET `/api/fulfillment/snapshot?station=BEP` (or BAR). Without station, return stations the current role may read.
3. Replace the queue with response.tickets and retain every ID/version in response.tombstones. Then apply buffered events only if their version exceeds both the visible and tombstone version. Do not play snapshot sounds.
4. Remove tickets in DA_GIAO/DA_HUY/LOI from station queues and retain their terminal versions, including buffered terminal events. Never discard tombstones while consuming retries from the Outbox.

Optional `orderId` requires Staff/Manager and returns all ticket states for that order, including terminal states.
Unknown orders return an empty list. Cross-station reads by Kitchen/Bar return 403.
Snapshots set Cache-Control: no-store. Connect and join before fetching the snapshot to close the reconnect gap.

This backend contract is ready for the frontend owner to integrate. Production Nginx/Cloudflare and the
50-connection/30-minute staging test remain HAI-4.1; frontend acceptance remains DUY-4.1.
