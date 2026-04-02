import { apiSlice } from '@/shared/api/apiSlice';
import type { Role, CreateRoleInput, UpdateRoleInput } from './types';
import type { Permission } from '@/features/permissions/types';

export const roleEndpoints = apiSlice.injectEndpoints({
  endpoints: (builder) => ({

    getRoles: builder.query<Role[], { tenantId: string }>({
      query: ({ tenantId }) => `/tenants/${tenantId}/roles`,
      providesTags: (result) =>
        result
          ? [...result.map(({ id }) => ({ type: 'Role' as const, id })), { type: 'Role' as const, id: 'LIST' }]
          : [{ type: 'Role' as const, id: 'LIST' }],
      keepUnusedDataFor: 300,
    }),

    getRoleById: builder.query<Role, { tenantId: string; roleId: string }>({
      query: ({ tenantId, roleId }) => `/tenants/${tenantId}/roles/${roleId}`,
      providesTags: (_r, _e, { roleId }) => [{ type: 'Role' as const, id: roleId }],
    }),

    createRole: builder.mutation<Role, { tenantId: string; body: CreateRoleInput }>({
      query: ({ tenantId, body }) => ({
        url: `/tenants/${tenantId}/roles`,
        method: 'POST',
        body,
      }),
      invalidatesTags: [{ type: 'Role', id: 'LIST' }],
    }),

    updateRole: builder.mutation<Role, { tenantId: string; roleId: string; body: UpdateRoleInput }>({
      query: ({ tenantId, roleId, body }) => ({
        url: `/tenants/${tenantId}/roles/${roleId}`,
        method: 'PUT',
        body,
      }),
      invalidatesTags: (_r, _e, { roleId }) => [{ type: 'Role', id: roleId }],
    }),

    deleteRole: builder.mutation<void, { tenantId: string; roleId: string }>({
      query: ({ tenantId, roleId }) => ({
        url: `/tenants/${tenantId}/roles/${roleId}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_r, _e, { roleId }) => [
        { type: 'Role', id: roleId },
        { type: 'Role', id: 'LIST' },
      ],
    }),

    getRolePermissions: builder.query<Permission[], { tenantId: string; roleId: string }>({
      query: ({ tenantId, roleId }) => `/tenants/${tenantId}/roles/${roleId}/permissions`,
      providesTags: (_r, _e, { roleId }) => [{ type: 'Role' as const, id: roleId }],
    }),

    assignPermissionToRole: builder.mutation<void, { tenantId: string; roleId: string; permissionCode: string }>({
      query: ({ tenantId, roleId, permissionCode }) => ({
        url: `/tenants/${tenantId}/roles/${roleId}/permissions/${encodeURIComponent(permissionCode)}`,
        method: 'POST',
      }),
      invalidatesTags: (_r, _e, { roleId }) => [
        { type: 'Role', id: roleId },
        { type: 'Permission', id: 'LIST' },
      ],
    }),

    revokePermissionFromRole: builder.mutation<void, { tenantId: string; roleId: string; permissionCode: string }>({
      query: ({ tenantId, roleId, permissionCode }) => ({
        url: `/tenants/${tenantId}/roles/${roleId}/permissions/${encodeURIComponent(permissionCode)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_r, _e, { roleId }) => [
        { type: 'Role', id: roleId },
        { type: 'Permission', id: 'LIST' },
      ],
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetRolesQuery,
  useGetRoleByIdQuery,
  useCreateRoleMutation,
  useUpdateRoleMutation,
  useDeleteRoleMutation,
  useGetRolePermissionsQuery,
  useAssignPermissionToRoleMutation,
  useRevokePermissionFromRoleMutation,
} = roleEndpoints;
