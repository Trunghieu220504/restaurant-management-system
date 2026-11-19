import { http, HttpResponse } from "msw";

export interface MockAccount {
    id: string;
    userName: string;
    email: string | null;
    roles: string[];
    permissions: string[];
}

export const defaultMockAccount: MockAccount = {
    id: "e4293f01-6b45-4299-8f0a-7098e9c8f001",
    userName: "quanly01",
    email: "quanly01@restaurant.local",
    roles: ["Manager"],
    permissions: [
        "sales.orders.manage",
        "catalog.menu.manage",
        "identity.accounts.manage",
        "identity.subordinates.manage",
        "identity.roles.manage",
    ],
};

// In-memory mock database state
const validRefreshTokens = new Set<string>();
const activeAccessTokens = new Map<string, MockAccount>();
let activeCookieValue: string | null = null;
let refreshCallCount = 0;

export function getRefreshCallCount() {
    return refreshCallCount;
}

export function resetMockIdentityState() {
    validRefreshTokens.clear();
    activeAccessTokens.clear();
    activeCookieValue = null;
    refreshCallCount = 0;
}

// Seed a default valid session for testing convenience
export function seedMockSession(account = defaultMockAccount) {
    const token = `mock_access_token_${Math.random().toString(36).substring(2)}`;
    const refreshToken = `mock_refresh_token_${Math.random().toString(36).substring(2)}`;
    activeAccessTokens.set(token, account);
    validRefreshTokens.add(refreshToken);
    activeCookieValue = refreshToken;
    return { accessToken: token, refreshToken };
}

function extractRefreshToken(request: Request): string | null {
    // Check request cookie header or fallback to mock cookie state
    const cookieHeader = request.headers.get("cookie") || "";
    const match = cookieHeader.match(/refreshToken=([^;]+)/);
    if (match) {
        return match[1];
    }
    return activeCookieValue;
}

export const identityHandlers = [
    // 1. POST /api/identity/login
    http.post("*/api/identity/login", async ({ request }) => {
        let body: { userName?: string; password?: string } | null;
        try {
            body = (await request.json()) as typeof body;
        } catch {
            return HttpResponse.json(
                { status: 400, title: "Invalid JSON input." },
                { status: 400 },
            );
        }

        const { userName, password } = body || {};
        if (!userName || !password) {
            return HttpResponse.json(
                { status: 400, title: "Username and password are required." },
                { status: 400 },
            );
        }

        if (password !== "Password123!") {
            return HttpResponse.json(
                {
                    status: 401,
                    title: "Unable to sign in with the supplied credentials.",
                },
                { status: 401 },
            );
        }

        const accessToken = `access_${Date.now()}_${Math.random().toString(36).substring(2)}`;
        const refreshToken = `refresh_${Date.now()}_${Math.random().toString(36).substring(2)}`;

        activeAccessTokens.set(accessToken, defaultMockAccount);
        validRefreshTokens.add(refreshToken);
        activeCookieValue = refreshToken;

        const expiresAt = new Date(Date.now() + 15 * 60 * 1000).toISOString();

        return HttpResponse.json(
            {
                accessToken,
                expiresAt,
                account: defaultMockAccount,
            },
            {
                status: 200,
                headers: {
                    "Set-Cookie": `refreshToken=${refreshToken}; Path=/api/identity; HttpOnly; SameSite=Strict; Max-Age=604800`,
                },
            },
        );
    }),

    // 2. POST /api/identity/refresh
    http.post("*/api/identity/refresh", async ({ request }) => {
        refreshCallCount++;

        const refreshToken = extractRefreshToken(request);
        if (!refreshToken || !validRefreshTokens.has(refreshToken)) {
            return HttpResponse.json(
                { status: 401, title: "Invalid or expired refresh token." },
                { status: 401 },
            );
        }

        // Issue new access token and rotate refresh token
        const newAccessToken = `access_refreshed_${Date.now()}_${Math.random().toString(36).substring(2)}`;
        const newRefreshToken = `refresh_rotated_${Date.now()}_${Math.random().toString(36).substring(2)}`;

        validRefreshTokens.delete(refreshToken);
        validRefreshTokens.add(newRefreshToken);
        activeAccessTokens.set(newAccessToken, defaultMockAccount);
        activeCookieValue = newRefreshToken;

        const expiresAt = new Date(Date.now() + 15 * 60 * 1000).toISOString();

        return HttpResponse.json(
            {
                accessToken: newAccessToken,
                expiresAt,
                account: defaultMockAccount,
            },
            {
                status: 200,
                headers: {
                    "Set-Cookie": `refreshToken=${newRefreshToken}; Path=/api/identity; HttpOnly; SameSite=Strict; Max-Age=604800`,
                },
            },
        );
    }),

    // 3. GET /api/identity/me
    http.get("*/api/identity/me", ({ request }) => {
        const authHeader = request.headers.get("authorization") || "";
        if (!authHeader.startsWith("Bearer ")) {
            return HttpResponse.json(
                { status: 401, title: "Missing Bearer token." },
                { status: 401 },
            );
        }

        const token = authHeader.substring(7);
        const account = activeAccessTokens.get(token);
        if (!account) {
            return HttpResponse.json(
                { status: 401, title: "Invalid or expired access token." },
                { status: 401 },
            );
        }

        return HttpResponse.json(account, { status: 200 });
    }),

    // 4. POST /api/identity/logout
    http.post("*/api/identity/logout", ({ request }) => {
        const authHeader = request.headers.get("authorization") || "";
        if (authHeader.startsWith("Bearer ")) {
            const token = authHeader.substring(7);
            activeAccessTokens.delete(token);
        }

        const refreshToken = extractRefreshToken(request);
        if (refreshToken) {
            validRefreshTokens.delete(refreshToken);
        }
        activeCookieValue = null;

        return new HttpResponse(null, {
            status: 204,
            headers: {
                "Set-Cookie":
                    "refreshToken=; Path=/api/identity; HttpOnly; SameSite=Strict; Max-Age=0",
            },
        });
    }),

    // 5. POST /api/identity/change-password
    http.post("*/api/identity/change-password", async ({ request }) => {
        const authHeader = request.headers.get("authorization") || "";
        if (!authHeader.startsWith("Bearer ")) {
            return HttpResponse.json(
                { status: 401, title: "Unauthorized" },
                { status: 401 },
            );
        }

        let body: { currentPassword?: string; newPassword?: string } | null;
        try {
            body = (await request.json()) as typeof body;
        } catch {
            return HttpResponse.json(
                { status: 400, title: "Invalid input" },
                { status: 400 },
            );
        }

        const { currentPassword, newPassword } = body || {};
        if (currentPassword !== "Password123!") {
            return HttpResponse.json(
                { status: 400, title: "Current password does not match." },
                { status: 400 },
            );
        }

        if (!newPassword || newPassword.length < 12) {
            return HttpResponse.json(
                { status: 400, title: "Password policy violation." },
                { status: 400 },
            );
        }

        // Revoke all sessions
        activeAccessTokens.clear();
        validRefreshTokens.clear();
        activeCookieValue = null;

        return new HttpResponse(null, {
            status: 204,
            headers: {
                "Set-Cookie":
                    "refreshToken=; Path=/api/identity; HttpOnly; SameSite=Strict; Max-Age=0",
            },
        });
    }),
];
