import { useEffect } from 'react';
import type { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import type { UseQueryResult } from '@tanstack/react-query';
import { ApiError } from '../api/client';
import { EmptyState, Panel } from './Layout';

/**
 * Turns the server's error contract into the three screens BrandX kept separate:
 *
 *   401 -> sign in
 *   403 -> access denied, which is deliberately not the not-found screen, so a
 *          refusal never implies the record is missing
 *   404 -> not found, with the server's own message
 *
 * Anything else is an unexpected failure and says so plainly.
 */
export function useAuthErrorRedirect(error: unknown): void {
  const navigate = useNavigate();

  useEffect(() => {
    if (!(error instanceof ApiError)) return;
    if (error.status === 401) navigate('/login', { replace: true });
    else if (error.status === 403) navigate('/access-denied', { replace: true });
  }, [error, navigate]);
}

export function Loadable<T>({
  query,
  children,
}: {
  query: UseQueryResult<T>;
  children: (data: T) => ReactNode;
}) {
  useAuthErrorRedirect(query.error);

  if (query.isPending) {
    return <p className="muted">Loading…</p>;
  }

  if (query.error) {
    const error = query.error;

    if (error instanceof ApiError) {
      // Handled by the redirect above; render nothing while it happens.
      if (error.status === 401 || error.status === 403) return null;

      if (error.status === 404) {
        return (
          <Panel>
            <p>{error.message}</p>
          </Panel>
        );
      }
    }

    return (
      <Panel>
        <EmptyState>{error instanceof Error ? error.message : 'Something went wrong.'}</EmptyState>
      </Panel>
    );
  }

  return <>{children(query.data)}</>;
}
