import {
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query/react';

// Typed just enough to read the token — avoids a circular dep with store.ts
interface StateWithAuth {
  auth: { accessToken: string | null };
}

const rawBaseQuery = fetchBaseQuery({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? '/api/v1',
  credentials: 'include',
  prepareHeaders: (headers, { getState }) => {
    const token = (getState() as StateWithAuth).auth.accessToken;
    if (token) headers.set('Authorization', `Bearer ${token}`);
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
    return !result.error;
  })();
  return refreshPromise;
}

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
