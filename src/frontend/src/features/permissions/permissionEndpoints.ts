import { apiSlice } from '@/shared/api/apiSlice';
import type { Permission, CreatePermissionInput } from './types';
import type { CheckPermissionInput, AccessResult } from '@/shared/types';

export const permissionEndpoints = apiSlice.injectEndpoints({
  endpoints: (builder) => ({

    getPermissions: builder.query<Permission[], { tenantId: string }>({
      query: ({ tenantId }) => `/tenants/${tenantId}/permissions`,
      providesTags: (result) =>
        result
          ? [...result.map(({ id }) => ({ type: 'Permission' as const, id })), { type: 'Permission' as const, id: 'LIST' }]
          : [{ type: 'Permission' as const, id: 'LIST' }],
      keepUnusedDataFor: 300,
    }),

    createPermission: builder.mutation<Permission, { tenantId: string; body: CreatePermissionInput }>({
      query: ({ tenantId, body }) => ({
        url: `/tenants/${tenantId}/permissions`,
        method: 'POST',
        body,
      }),
      invalidatesTags: [{ type: 'Permission', id: 'LIST' }],
    }),

    /**
     * Full permission evaluation — returns the complete AccessResult including
     * evaluated policies, effective roles, delegation chain, and cache hit status.
     * Used by AbilityContext and the permission check UI panel.
     */
    checkPermission: builder.mutation<AccessResult, { tenantId: string; body: CheckPermissionInput }>({
      query: ({ tenantId, body }) => ({
        url: `/tenants/${tenantId}/permissions/check`,
        method: 'POST',
        body,
      }),
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetPermissionsQuery,
  useCreatePermissionMutation,
  useCheckPermissionMutation,
} = permissionEndpoints;
