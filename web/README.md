# Web application

The Next.js app uses same-origin requests under `/api/*`; Next.js rewrites
forward them to `API_URL`. For local development, create `.env.local` from
`.env.example` and keep the backend running at `http://localhost:5080`.
Set `API_URL` in the build environment when producing the standalone output so
the rewrite destination is included in the deployed Next.js configuration.

Run `npm install`, then use `npm run dev`, `npm run build`, `npm run lint`,
`npm test`, or `npm run test:e2e`. `npm run gen:api` fetches the backend OpenAPI
document from `${API_URL}/openapi/v1.json` and writes types to
`src/lib/api/schema.d.ts`.

## Application UI starter

The dashboard shell includes permission-filtered navigation, loading skeletons,
route and global error boundaries, 403/404 pages, a searchable TanStack table,
an RHF/Zod form example, Sonner toasts, and an accessible confirmation dialog.
Dashboard metrics and rows are sample data; replace them with API-backed data.
The menu's permission codes correspond to the current identity permission seed:
`sales.orders.manage`, `catalog.menu.manage`, `identity.accounts.manage`,
`identity.subordinates.manage`, and `identity.roles.manage`.

The root layout currently supplies an empty permission list. Connect
`PermissionProvider` to the authenticated user's permissions before enabling
protected navigation. Client-side menu filtering is only a usability feature:
the backend must authorize every protected API request independently. Current
backend documentation notes that permissions are returned for display and
business authorization handlers are future work.
