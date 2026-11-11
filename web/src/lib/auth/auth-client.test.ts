// @vitest-environment jsdom

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  authFetch,
  changePassword,
  getMe,
  login,
  logout,
  onUnauthorized,
  refreshAccessTokenSingleFlight,
} from "./auth-client";
import { clearAccessToken, getAccessToken, setAccessToken } from "./token-store";
import {
  defaultMockAccount,
  getRefreshCallCount,
  resetMockIdentityState,
  seedMockSession,
} from "@/test/msw/handlers/identity";

describe("Identity Authentication Client & Concurrency", () => {
  beforeEach(() => {
    document.cookie = "refreshToken=; Path=/api/identity; Max-Age=0";
    document.cookie = "refreshToken=; Path=/; Max-Age=0";
    vi.stubEnv("NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED", "true");
    clearAccessToken();
    resetMockIdentityState();
  });
  afterEach(() => vi.unstubAllEnvs());
  it("does not call an unimplemented refresh endpoint by default", async () => {
    vi.stubEnv("NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED", "false");
    setAccessToken("stale");
    expect(await refreshAccessTokenSingleFlight()).toBeNull();
    expect((await authFetch("/api/identity/me")).status).toBe(401);
    expect(getRefreshCallCount()).toBe(0);
    expect(getAccessToken()).toBeNull();
  });

  it("1. login stores access token in memory and getMe retrieves account", async () => {
    expect(getAccessToken()).toBeNull();

    const result = await login("nguyen the vinh", "Password123!");

    expect(result.accessToken).toBeTruthy();
    expect(getAccessToken()).toBe(result.accessToken);
    expect(result.account.userName).toBe("nguyen the vinh");

    // Can fetch current user
    const account = await getMe();
    expect(account.userName).toBe("nguyen the vinh");
    expect(account.roles).toContain("Manager");
  });

  it("2. login rejects invalid password with 401 error", async () => {
    await expect(login("nguyen the vinh", "WrongPass")).rejects.toThrow();
    expect(getAccessToken()).toBeNull();
  });

  it("3. logout revokes session and clears in-memory access token", async () => {
    await login("nguyen the vinh", "Password123!");
    expect(getAccessToken()).not.toBeNull();

    await logout();
    expect(getAccessToken()).toBeNull();

    // After logout, me fails with 401
    await expect(getMe()).rejects.toThrow();
  });

  it("4. changePassword verifies current password, updates, and revokes sessions", async () => {
    await login("nguyen the vinh", "Password123!");
    expect(getAccessToken()).not.toBeNull();

    await changePassword("Password123!", "NewStrongPass456!");
    expect(getAccessToken()).toBeNull();
  });

  it("5. CRITICAL: multiple concurrent requests hitting 401 trigger refresh ONLY ONCE", async () => {
    // 1. Seed a session so MSW has a valid refresh token cookie
    seedMockSession();

    // 2. Set an expired/stale access token in memory to force a 401 on all requests
    setAccessToken("stale_expired_token_123");

    expect(getRefreshCallCount()).toBe(0);

    // 3. Fire 5 concurrent requests simultaneously
    const concurrentRequests = [
      authFetch("/api/identity/me"),
      authFetch("/api/identity/me"),
      authFetch("/api/identity/me"),
      authFetch("/api/identity/me"),
      authFetch("/api/identity/me"),
    ];

    const responses = await Promise.all(concurrentRequests);

    // 4. Assert: /api/identity/refresh must have been called EXACTLY ONCE!
    expect(getRefreshCallCount()).toBe(1);

    // 5. Assert: All 5 requests recovered and succeeded with HTTP 200
    for (const res of responses) {
      expect(res.status).toBe(200);
      const data = await res.json();
      expect(data.userName).toBe(defaultMockAccount.userName);
    }

    // 6. Access token in memory has been updated with the refreshed token
    expect(getAccessToken()).toBeTruthy();
    expect(getAccessToken()).not.toBe("stale_expired_token_123");
  });

  it("6. concurrent calls directly to refreshAccessTokenSingleFlight share the same in-flight promise", async () => {
    await login("nguyen the vinh", "Password123!");
    expect(getRefreshCallCount()).toBe(0);

    // Call single-flight refresh 4 times simultaneously
    const [token1, token2, token3, token4] = await Promise.all([
      refreshAccessTokenSingleFlight(),
      refreshAccessTokenSingleFlight(),
      refreshAccessTokenSingleFlight(),
      refreshAccessTokenSingleFlight(),
    ]);

    expect(getRefreshCallCount()).toBe(1);
    expect(token1).toBeTruthy();
    expect(token1).toBe(token2);
    expect(token2).toBe(token3);
    expect(token3).toBe(token4);
  });

  it("7. when refresh fails, all concurrent requests fail and onUnauthorized listener is triggered", async () => {
    const unauthorizedSpy = vi.fn();
    onUnauthorized(unauthorizedSpy);

    // No valid refresh token in mock state
    setAccessToken("invalid_token");

    const responses = await Promise.all([
      authFetch("/api/identity/me"),
      authFetch("/api/identity/me"),
    ]);

    expect(getRefreshCallCount()).toBe(1);
    for (const res of responses) {
      expect(res.status).toBe(401);
    }

    expect(getAccessToken()).toBeNull();
    expect(unauthorizedSpy).toHaveBeenCalled();
  });
});
