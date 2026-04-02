import {
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query/react';
import { v4 as uuidv4 } from 'uuid';
import { logout } from '@/features/auth/authSlice';

// Typed just enough to read auth state — avoids a circular dep with store.ts
interface StateWithAuth {
  auth: { accessToken: string | null; tenantId: string | null };
}

const rawBaseQuery = fetchBaseQuery({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? '/api/v1',
  credentials: 'include',
  prepareHeaders: (headers, { getState }) => {
    const state = (getState() as StateWithAuth).auth;

    // Bearer token — stored in Redux memory only, never localStorage
    if (state.accessToken) {
      headers.set('Authorization', `Bearer ${state.accessToken}`);
    }

    // Tenant context — injected on every request so the backend does not need
    // to parse it from JWT on each call (belt-and-suspenders; JWT tid still enforced)
    if (state.tenantId) {
      headers.set('X-Tenant-Id', state.tenantId);
    }

    // Unique correlation ID per request — propagated through backend spans for
    // distributed tracing. Matches the CorrelationId field in audit logs.
    headers.set('X-Correlation-Id', uuidv4());

    return headers;
  },
});

let isRefreshing = false;
let refreshPromise: Promise<boolean> | null = null;

async function performRefresh(api: Parameters<BaseQueryFn>[1]): Promise<boolean> {
  if (isRefreshing && refreshPromise !== null) return refreshPromise;
  isRefreshing = true;
  refreshPromise = (async () => {
    const result = await rawBaseQuery({ url: '/auth/refresh', method: 'POST' }, api, {});
    isRefreshing = false;
    refreshPromise = null;
    if (result.error) {
      // Refresh failed — session dead, force logout
      api.dispatch(logout());
      return false;
    }
    return true;
  })();
  return refreshPromise;
}

/**
 * Custom base query with:
 *  - Bearer token from Redux state (in-memory, no localStorage)
 *  - X-Tenant-Id and X-Correlation-Id headers on every request
 *  - Transparent 401 → refresh → retry with concurrent-refresh guard
 *  - logout() dispatch when refresh fails
 */
export const buildBaseQuery: BaseQueryFn<
  string | FetchArgs,
  unknown,
  FetchBaseQueryError
> = async (args, api, extraOptions) => {
  let result = await rawBaseQuery(args, api, extraOptions);

  if (result.error?.status === 401) {
    const refreshed = await performRefresh(api);
    if (refreshed) {
      result = await rawBaseQuery(args, api, extraOptions);
    }
  }

  return result;
};
