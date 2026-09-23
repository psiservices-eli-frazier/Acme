import { createContext, useCallback, useContext, useMemo } from 'react';
import type { ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, invalidateCsrfToken } from '../api/client';
import { authApi } from '../api/resources';
import type { CurrentUser } from '../api/types';

interface AuthValue {
  user: CurrentUser | null;
  loading: boolean;
  isAdmin: boolean;
  signIn: (username: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
}

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();

  const { data, isPending } = useQuery({
    queryKey: ['me'],
    staleTime: Infinity,
    retry: false,
    queryFn: async () => {
      try {
        return await authApi.me();
      } catch (error) {
        // Signed out is an expected answer here, not a failure.
        if (error instanceof ApiError && error.status === 401) return null;
        throw error;
      }
    },
  });

  const user = data ?? null;

  const signIn = useCallback(
    async (username: string, password: string) => {
      const signedIn = await authApi.login(username, password);

      // The antiforgery token is bound to the identity that asked for it, so the one
      // used to post this sign-in is already stale.
      invalidateCsrfToken();

      queryClient.clear();
      queryClient.setQueryData(['me'], signedIn);
    },
    [queryClient],
  );

  const signOut = useCallback(async () => {
    await authApi.logout();
    invalidateCsrfToken();
    queryClient.clear();
    queryClient.setQueryData(['me'], null);
  }, [queryClient]);

  const value = useMemo<AuthValue>(
    () => ({
      user,
      loading: isPending,
      isAdmin: user?.roles.includes('ADMIN') ?? false,
      signIn,
      signOut,
    }),
    [user, isPending, signIn, signOut],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}

export function useAuth(): AuthValue {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used inside an AuthProvider');
  return value;
}

/**
 * Shows its children only to administrators.
 *
 * This is presentation, exactly like the `sec:authorize` attributes it replaces. The
 * enforcement is the policy check in the server's services -- a control that is only
 * hidden here is not protected.
 */
export function AdminOnly({ children }: { children: ReactNode }) {
  const { isAdmin } = useAuth();
  return isAdmin ? <>{children}</> : null;
}
