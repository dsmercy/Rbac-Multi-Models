import { apiSlice } from './apiSlice';
import type { Scope } from '@/shared/types';

export const scopeEndpoints = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getScopes: builder.query<Scope[], { tenantId: string }>({
      query: ({ tenantId }) => `/tenants/${tenantId}/scopes`,
      providesTags: [{ type: 'Scope', id: 'LIST' }],
      keepUnusedDataFor: 3600,
    }),
  }),
  overrideExisting: false,
});

export const { useGetScopesQuery } = scopeEndpoints;
