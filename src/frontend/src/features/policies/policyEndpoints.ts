import { apiSlice } from '@/shared/api/apiSlice';
import type { Policy, CreatePolicyInput, UpdatePolicyInput } from './types';

export const policyEndpoints = apiSlice.injectEndpoints({
  endpoints: (builder) => ({

    getPolicies: builder.query<Policy[], { tenantId: string }>({
      query: ({ tenantId }) => `/tenants/${tenantId}/policies`,
      providesTags: (result) =>
        result
          ? [...result.map(({ id }) => ({ type: 'Policy' as const, id })), { type: 'Policy' as const, id: 'LIST' }]
          : [{ type: 'Policy' as const, id: 'LIST' }],
      keepUnusedDataFor: 300,
    }),

    getPolicyById: builder.query<Policy, { tenantId: string; policyId: string }>({
      query: ({ tenantId, policyId }) => `/tenants/${tenantId}/policies/${policyId}`,
      providesTags: (_r, _e, { policyId }) => [{ type: 'Policy' as const, id: policyId }],
    }),

    createPolicy: builder.mutation<Policy, { tenantId: string; body: CreatePolicyInput }>({
      query: ({ tenantId, body }) => ({
        url: `/tenants/${tenantId}/policies`,
        method: 'POST',
        body,
      }),
      invalidatesTags: [{ type: 'Policy', id: 'LIST' }],
    }),

    updatePolicy: builder.mutation<Policy, { tenantId: string; policyId: string; body: UpdatePolicyInput }>({
      query: ({ tenantId, policyId, body }) => ({
        url: `/tenants/${tenantId}/policies/${policyId}`,
        method: 'PUT',
        body,
      }),
      invalidatesTags: (_r, _e, { policyId }) => [{ type: 'Policy', id: policyId }],
    }),

    deletePolicy: builder.mutation<void, { tenantId: string; policyId: string }>({
      query: ({ tenantId, policyId }) => ({
        url: `/tenants/${tenantId}/policies/${policyId}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_r, _e, { policyId }) => [
        { type: 'Policy', id: policyId },
        { type: 'Policy', id: 'LIST' },
      ],
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetPoliciesQuery,
  useGetPolicyByIdQuery,
  useCreatePolicyMutation,
  useUpdatePolicyMutation,
  useDeletePolicyMutation,
} = policyEndpoints;
