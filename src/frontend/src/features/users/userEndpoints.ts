import { apiSlice } from '@/shared/api/apiSlice';
import type { User, CreateUserInput, UpdateUserInput, AssignRoleInput, UserRoleAssignment } from './types';
import type { PagedResult } from '@/shared/types';

export interface GetUsersParams {
  tenantId: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export const userEndpoints = apiSlice.injectEndpoints({
  endpoints: (builder) => ({

    getUsers: builder.query<PagedResult<User>, GetUsersParams>({
      query: ({ tenantId, search, page = 1, pageSize = 20 }) => ({
        url: `/tenants/${tenantId}/users`,
        params: { search, page, pageSize },
      }),
      providesTags: (result) =>
        result
          ? [...result.items.map(({ id }) => ({ type: 'User' as const, id })), { type: 'User' as const, id: 'LIST' }]
          : [{ type: 'User' as const, id: 'LIST' }],
      keepUnusedDataFor: 120,
    }),

    getUserById: builder.query<User, { tenantId: string; userId: string }>({
      query: ({ tenantId, userId }) => `/tenants/${tenantId}/users/${userId}`,
      providesTags: (_r, _e, { userId }) => [{ type: 'User' as const, id: userId }],
    }),

    createUser: builder.mutation<User, { tenantId: string; body: CreateUserInput }>({
      query: ({ tenantId, body }) => ({
        url: `/tenants/${tenantId}/users`,
        method: 'POST',
        body,
      }),
      invalidatesTags: [{ type: 'User', id: 'LIST' }],
    }),

    updateUser: builder.mutation<User, { tenantId: string; userId: string; body: UpdateUserInput }>({
      query: ({ tenantId, userId, body }) => ({
        url: `/tenants/${tenantId}/users/${userId}`,
        method: 'PUT',
        body,
      }),
      invalidatesTags: (_r, _e, { userId }) => [{ type: 'User', id: userId }],
    }),

    assignRoleToUser: builder.mutation<UserRoleAssignment, { tenantId: string; userId: string; body: AssignRoleInput }>({
      query: ({ tenantId, userId, body }) => ({
        url: `/tenants/${tenantId}/users/${userId}/roles`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_r, _e, { userId }) => [
        { type: 'User', id: userId },
        { type: 'Role', id: 'LIST' },
      ],
    }),

    revokeRoleFromUser: builder.mutation<void, { tenantId: string; userId: string; roleId: string }>({
      query: ({ tenantId, userId, roleId }) => ({
        url: `/tenants/${tenantId}/users/${userId}/roles/${roleId}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_r, _e, { userId }) => [{ type: 'User', id: userId }],
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetUsersQuery,
  useGetUserByIdQuery,
  useCreateUserMutation,
  useUpdateUserMutation,
  useAssignRoleToUserMutation,
  useRevokeRoleFromUserMutation,
} = userEndpoints;
