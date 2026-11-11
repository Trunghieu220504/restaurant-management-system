import { clearAccessToken, getAccessToken, setAccessToken } from "./token-store";
import type { components } from "@/lib/api/generated";

export type Account = components["schemas"]["CurrentAccount"];
export type LoginResult = components["schemas"]["LoginResult"];
export const isRefreshEnabled = () => process.env.NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED === "true";

// Single-flight refresh lock: ensures multiple concurrent 401 requests trigger only ONE refresh call
let refreshPromise: Promise<string | null> | null = null;
let unauthorizedListener: (() => void) | null = null;

export function onUnauthorized(callback: () => void) {
  unauthorizedListener = callback;
  return () => { if (unauthorizedListener === callback) unauthorizedListener = null; };
}

/**
 * Performs a single-flight token refresh.
 * If multiple callers invoke this concurrently, they all await the exact same in-flight request.
 */
export async function refreshAccessTokenSingleFlight(): Promise<string | null> {
  if (!isRefreshEnabled()) return null;
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async () => {
    try {
      const response = await fetch("/api/identity/refresh", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        credentials: "same-origin",
      });

      if (!response.ok) {
        clearAccessToken();
        return null;
      }

      const data: LoginResult = await response.json();
      setAccessToken(data.accessToken);
      return data.accessToken;
    } catch {
      clearAccessToken();
      return null;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

export async function login(userName: string, password: string): Promise<LoginResult> {
  const response = await fetch("/api/identity/login", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    credentials: "same-origin",
    body: JSON.stringify({ userName, password }),
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error(errorData.title || `Login failed with status ${response.status}`);
  }

  const data: LoginResult = await response.json();
  setAccessToken(data.accessToken);
  return data;
}

export async function getMe(): Promise<Account> {
  const response = await authFetch("/api/identity/me");
  if (!response.ok) {
    throw new Error(`Failed to fetch current account (${response.status})`);
  }
  return response.json();
}

export async function logout(): Promise<void> {
  const token = getAccessToken();
    const response = await fetch("/api/identity/logout", {
      method: "POST",
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      credentials: "same-origin",
    });
    if (!response.ok && response.status !== 401) throw new Error("Chưa đăng xuất được. Vui lòng thử lại.");
    clearAccessToken();
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  const response = await authFetch("/api/identity/change-password", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ currentPassword, newPassword }),
  });

  if (!response.ok) {
    const err = await response.json().catch(() => ({}));
    throw new Error(err.title || "Password change failed");
  }

  clearAccessToken();
}

/**
 * Fetch wrapper that automatically attaches in-memory access token,
 * intercepts 401 responses, triggers single-flight token refresh,
 * and retries the original request.
 */
export async function authFetch(input: RequestInfo | URL, init: RequestInit = {}): Promise<Response> {
  const token = getAccessToken();
  const headers = new Headers(input instanceof Request ? input.headers : undefined);
  new Headers(init.headers).forEach((value, key) => headers.set(key, value));

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  let response = await fetch(input, {
    ...init,
    headers,
    credentials: "same-origin",
  });

  // If 401 Unauthorized, attempt single-flight refresh and retry once
  if (response.status === 401) {
    if (getAccessToken() !== token) return response;
    const newToken = await refreshAccessTokenSingleFlight();

    if (newToken) {
      const retryHeaders = new Headers(headers);
      retryHeaders.set("Authorization", `Bearer ${newToken}`);
      response = await fetch(input, {
        ...init,
        headers: retryHeaders,
        credentials: "same-origin",
      });
      if (response.status === 401 && getAccessToken() === newToken) { clearAccessToken(); unauthorizedListener?.(); }
    } else {
      clearAccessToken();
      if (unauthorizedListener) {
        unauthorizedListener();
      }
    }
  }

  return response;
}
