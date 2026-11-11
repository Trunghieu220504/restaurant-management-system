"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { useRouter } from "next/navigation";
import {
  getMe,
  login as clientLogin,
  logout as clientLogout,
  onUnauthorized,
  refreshAccessTokenSingleFlight,
  type Account,
} from "@/lib/auth/auth-client";
import { clearAccessToken, getAccessToken } from "@/lib/auth/token-store";
import { PermissionProvider } from "@/components/permission-provider";

interface AuthContextValue {
  user: Account | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  login: (u: string, p: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const [user, setUser] = useState<Account | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Initial session restoration: Since refresh cookie is scoped to Path=/api/identity,
  // Next.js middleware cannot see it. We restore session client-side on mount.
  useEffect(() => {
    let mounted = true;

    async function restoreSession() {
      try {
        const token = await refreshAccessTokenSingleFlight();
        if (token && mounted) {
          const account = await getMe();
          if (mounted) {
            setUser(account);
          }
        }
      } catch {
        if (mounted) {
          setUser(null);
          clearAccessToken();
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    }

    restoreSession();

    // Listen for global 401 unrecoverable event
    onUnauthorized(() => {
      setUser(null);
      clearAccessToken();
      router.push("/login");
    });

    return () => {
      mounted = false;
    };
  }, [router]);

  const login = useCallback(
    async (u: string, p: string) => {
      const result = await clientLogin(u, p);
      setUser(result.account);
    },
    [],
  );

  const logout = useCallback(async () => {
    await clientLogout();
    setUser(null);
    router.push("/login");
  }, [router]);

  const permissions = user?.permissions || [];

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading,
        isAuthenticated: !!user && !!getAccessToken(),
        login,
        logout,
      }}
    >
      <PermissionProvider permissions={permissions}>
        {children}
      </PermissionProvider>
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return context;
}
