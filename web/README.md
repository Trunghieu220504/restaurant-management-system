# Web application

The Next.js app uses same-origin requests under `/api/*`; Next.js rewrites
forward them to `API_URL`. For local development, create `.env.local` from
`.env.example` and keep the backend running at `http://localhost:5080`.
Set `API_URL` in the build environment when producing the standalone output so
the rewrite destination is included in the deployed Next.js configuration.

Run `npm install`, then use `npm run dev`, `npm run build`, `npm run lint`,
`npm test`, or `npm run test:e2e`.

## API Client Generation

Client types are generated with `openapi-typescript` into the dedicated directory
`src/lib/api/generated/` and must never be edited manually.

Run `npm run gen:api` to regenerate types from the committed specification
at `docs/api/openapi.json` (or pass a spec file or live URL via `OPENAPI_SPEC`).
CI enforces that `src/lib/api/generated/` remains strictly in sync with
`docs/api/openapi.json`—any commit with an updated OpenAPI spec that omits regenerated
client types will fail CI (exit code 1). Use `npm run gen:api:check` locally to verify.

## Application UI starter

The dashboard shell includes permission-filtered navigation, loading skeletons,
route and global error boundaries, 403/404 pages, a searchable TanStack table,
an RHF/Zod form example, Sonner toasts, and an accessible confirmation dialog.
Dashboard metrics and rows are sample data; replace them with API-backed data.
The permissions each route needs live in `src/lib/auth/route-permissions.ts`, using the
codes seeded in `IdentityCatalog.cs`. The same table hides menu entries (sidebar, mobile
tabs, the "Thêm" page) and renders the 403 view when a route is opened directly.

The root layout connects `AuthProvider` and `PermissionProvider` to the authenticated
user's permissions before enabling protected navigation. Client-side menu filtering is only
a usability feature: the backend must authorize every protected API request independently.

## Identity Authentication & Client-side Route Guard

Follows the specification in `docs/api/identity-contract.md`:

- **In-Memory Access Token**: Token is held strictly in RAM (`token-store.ts`) to prevent XSS.
- **Refresh Cookie**: Scoped to `Path=/api/identity; HttpOnly; SameSite=Strict`.
- **Client-side Route Guard**: Because the refresh cookie path is limited to `/api/identity`,
  Next.js middleware cannot inspect it. Session restoration runs on client mount via
  `refreshAccessTokenSingleFlight()`. Unauthenticated users are redirected to `/login`.
- **Concurrent 401 Single-Flight Refresh**: If multiple requests hit 401 simultaneously,
  only one `/api/identity/refresh` call is made; concurrent requests wait on that single promise
  and automatically retry once refreshed. A 401 that arrives after another request already
  refreshed retries with the new token instead of failing.
- **Cross-tab refresh**: The backend revokes the whole session when a rotated refresh secret is
  reused, so refreshes are serialized across tabs with the Web Locks API (`navigator.locks`).
- **Refresh flag**: `NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED=true` turns refresh on. It is a
  build-time value, so it must be present when `npm run build` runs (Docker build arg on staging).
  Without it, reloading the page or waiting 15 minutes sends the user back to `/login`.
- **Change password**: `/profile/password` (RHF + Zod, same policy as the backend). The backend
  revokes every session afterwards, so the user is sent back to `/login`.
- **Testing**: Run `npm test` to execute Vitest suites, including the concurrency test in
  `src/lib/auth/auth-client.test.ts`.
