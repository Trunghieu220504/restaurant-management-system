import type { Page } from "@playwright/test";

// Only a UI fixture. It does not prove backend cookie/session revocation.
export async function mockIdentity(
    page: Page,
    options: {
        signedIn?: boolean;
        permissions?: string[];
        roles?: string[];
    } = {},
) {
    const state = {
        signedIn: options.signedIn ?? true,
        loginCalls: 0,
        refreshCalls: 0,
        passwordCalls: 0,
        passwordStatus: 204,
        account: {
            id: "ui-test-account",
            userName: "ui-test",
            email: null,
            roles: options.roles ?? ["Staff"],
            permissions: options.permissions ?? [],
        },
    };
    const result = () => ({
        accessToken: "ui-test-access",
        expiresAt: new Date(Date.now() + 900_000).toISOString(),
        account: state.account,
    });

    await page.route("**/api/identity/**", async (route) => {
        const path = new URL(route.request().url()).pathname;
        switch (path) {
            case "/api/identity/login": {
                state.loginCalls++;
                const body = route.request().postDataJSON();
                if (body.password === "invalid-password") {
                    return route.fulfill({ status: 401, json: {} });
                }
                state.signedIn = true;
                return route.fulfill({ json: result() });
            }
            case "/api/identity/refresh":
                state.refreshCalls++;
                return state.signedIn
                    ? route.fulfill({ json: result() })
                    : route.fulfill({ status: 401 });
            case "/api/identity/me":
                return state.signedIn
                    ? route.fulfill({ json: state.account })
                    : route.fulfill({ status: 401 });
            case "/api/identity/logout":
                state.signedIn = false;
                return route.fulfill({ status: 204 });
            case "/api/identity/change-password":
                state.passwordCalls++;
                if (state.passwordStatus === 204) {
                    state.signedIn = false;
                    return route.fulfill({ status: 204 });
                }
                return route.fulfill({
                    status: state.passwordStatus,
                    json: { title: "Internal password validator detail" },
                });
            default:
                throw new Error(`Unexpected Identity path: ${path}`);
        }
    });
    await page.route("**/api/salesday/current", (route) =>
        route.fulfill({ json: null }),
    );
    return state;
}
