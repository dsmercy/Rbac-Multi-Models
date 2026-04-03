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
      // Optimistic update: immediately mark the delegation as Revoked in the
      // list cache so the UI reflects the change without a server round-trip.
      // If the server rejects, undo() restores the original status.
      async onQueryStarted({ tenantId, delegationId }, { dispatch, queryFulfilled }) {
        const revokedAt = new Date().toISOString();
        // Cast required: updateQueryData can't infer injected endpoint names within injectEndpoints
        const patchResult = dispatch(
          (apiSlice.util.updateQueryData as any)('getDelegations', { tenantId }, (draft: Delegation[]) => {
            const d = draft.find((item) => item.id === delegationId);
            if (d) {
              d.status    = 'Revoked';
              d.isRevoked = true;
              d.revokedAt = revokedAt;
            }
          }),
        );
        try {
          await queryFulfilled;
        } catch {
          patchResult.undo();
        }
      },
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
