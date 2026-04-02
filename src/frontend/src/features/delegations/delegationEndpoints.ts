import { apiSlice } from '@/shared/api/apiSlice';
import type { Delegation, CreateDelegationInput } from './types';

export const delegationEndpoints = apiSlice.injectEndpoints({
  endpoints: (builder) => ({

    getDelegations: builder.query<Delegation[], { tenantId: string }>({
      query: ({ tenantId }) => `/tenants/${tenantId}/delegations`,
      providesTags: (result) =>
        result
          ? [...result.map(({ id }) => ({ type: 'Delegation' as const, id })), { type: 'Delegation' as const, id: 'LIST' }]
          : [{ type: 'Delegation' as const, id: 'LIST' }],
      keepUnusedDataFor: 60,
    }),

    getDelegationById: builder.query<Delegation, { tenantId: string; delegationId: string }>({
      query: ({ tenantId, delegationId }) => `/tenants/${tenantId}/delegations/${delegationId}`,
      providesTags: (_r, _e, { delegationId }) => [{ type: 'Delegation' as const, id: delegationId }],
    }),

    createDelegation: builder.mutation<Delegation, { tenantId: string; body: CreateDelegationInput }>({
      query: ({ tenantId, body }) => ({
        url: `/tenants/${tenantId}/delegations`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_r, _e, { tenantId: _tid }) => [
        { type: 'Delegation', id: 'LIST' },
      ],
    }),

    revokeDelegation: builder.mutation<void, { tenantId: string; delegationId: string; delegateeId: string }>({
      query: ({ tenantId, delegationId }) => ({
        url: `/tenants/${tenantId}/delegations/${delegationId}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_r, _e, { delegationId, delegateeId }) => [
        { type: 'Delegation', id: delegationId },
        { type: 'Delegation', id: 'LIST' },
        { type: 'User', id: delegateeId },
      ],
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetDelegationsQuery,
  useGetDelegationByIdQuery,
  useCreateDelegationMutation,
  useRevokeDelegationMutation,
} = delegationEndpoints;
