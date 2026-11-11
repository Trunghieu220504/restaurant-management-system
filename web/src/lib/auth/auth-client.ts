import { clearAccessToken, getAccessToken, setAccessToken } from "./token-store";

export interface Account {
  id: string;
  userName: string;
  email: string | null;
  roles: string[];
  permissions: string[];
}

export interface LoginResult {
  accessToken: string;
  expiresAt: string;
  account: Account;
}

// Single-flight refresh lock: ensures multiple concurrent 401 requests trigger only ONE refresh call
let refreshPromise: Promise<string | null> | null = null;
let unauthorizedListener: (() => void) | null = null;

export function onUnauthorized(callback: () => void) {
  unauthorizedListener = callback;
}

/**
 * Performs a single-flight token refresh.
 * If multiple callers invoke this concurrently, they all await the exact same in-flight request.
 */
export async function refreshAccessTokenSingleFlight(): Promise<string | null> {
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
  try {
    await fetch("/api/identity/logout", {
      method: "POST",
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      credentials: "same-origin",
    });
  } finally {
    clearAccessToken();
  }
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
  const headers = new Headers(init.headers || {});

  if (token && !headers.has("Authorization")) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  let response = await fetch(input, {
    ...init,
    headers,
    credentials: "same-origin",
  });

  // If 401 Unauthorized, attempt single-flight refresh and retry once
  if (response.status === 401) {
    const newToken = await refreshAccessTokenSingleFlight();

    if (newToken) {
      const retryHeaders = new Headers(init.headers || {});
      retryHeaders.set("Authorization", `Bearer ${newToken}`);
      response = await fetch(input, {
        ...init,
        headers: retryHeaders,
        credentials: "same-origin",
      });
    } else {
      if (unauthorizedListener) {
        unauthorizedListener();
      }
    }
  }

  return response;
}
