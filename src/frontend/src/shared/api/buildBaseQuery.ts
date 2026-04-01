import {
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query/react';

const rawBaseQuery = fetchBaseQuery({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? '/api/v1',
  credentials: 'include', // send httpOnly cookies on every request
});

let isRefreshing = false;
let refreshPromise: Promise<boolean> | null = null;

async function performRefresh(api: Parameters<BaseQueryFn>[1]): Promise<boolean> {
  if (isRefreshing && refreshPromise !== null) return refreshPromise;
  isRefreshing = true;
  refreshPromise = (async () => {
    const result = await rawBaseQuery(
      { url: '/auth/refresh', method: 'POST' },
      api,
      {}
    );
    isRefreshing = false;
    refreshPromise = null;
    return !result.error;
  })();
  return refreshPromise;
}

/**
 * Custom base query with silent token refresh.
 * On 401, attempts one silent refresh via /auth/refresh (httpOnly cookie).
 * If refresh succeeds, retries the original request.
 * If refresh fails, returns the 401 — AuthGuard redirects to login.
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
