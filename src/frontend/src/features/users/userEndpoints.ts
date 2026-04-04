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

    getUserRoleAssignments: builder.query<UserRoleAssignment[], { tenantId: string; userId: string }>({
      query: ({ tenantId, userId }) => `/tenants/${tenantId}/users/${userId}/roles`,
      providesTags: (_r, _e, { userId }) => [{ type: 'User' as const, id: `roles-${userId}` }],
      keepUnusedDataFor: 120,
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
      // Optimistic update: append the new assignment immediately so the UI
      // reflects the change before the server responds.
      // Cast required: updateQueryData can't infer injected endpoint names within injectEndpoints
      async onQueryStarted({ tenantId, userId, body }, { dispatch, queryFulfilled }) {
        const tempId = `optimistic-${Date.now()}`;
        const patchResult = dispatch(
          (apiSlice.util.updateQueryData as any)(
            'getUserRoleAssignments',
            { tenantId, userId },
            (draft: UserRoleAssignment[]) => {
              draft.push({
                id: tempId,
                tenantId,
                userId,
                roleId: body.roleId,
                roleName: '',           // filled in from server response on success
                scopeId: body.scopeId ?? '',
                assignedAt: new Date().toISOString(),
                expiresAt: body.expiresAt ?? null,
                isActive: true,
              });
            },
          ),
        );
        try {
          const { data: assignment } = await queryFulfilled;
          // Replace the optimistic placeholder with the real server record
          dispatch(
            (apiSlice.util.updateQueryData as any)(
              'getUserRoleAssignments',
              { tenantId, userId },
              (draft: UserRoleAssignment[]) => {
                const idx = draft.findIndex((a) => a.id === tempId);
                if (idx !== -1) draft[idx] = assignment;
              },
            ),
          );
        } catch {
          // Server rejected — undo the optimistic insert
          patchResult.undo();
        }
      },
      invalidatesTags: (_r, _e, { userId }) => [
        { type: 'User', id: userId },
        { type: 'User', id: `roles-${userId}` },
        { type: 'Role', id: 'LIST' },
      ],
    }),

    revokeRoleFromUser: builder.mutation<void, { tenantId: string; userId: string; roleId: string; scopeId?: string }>({
      query: ({ tenantId, userId, roleId, scopeId }) => ({
        url: `/tenants/${tenantId}/users/${userId}/roles/${roleId}`,
        method: 'DELETE',
        params: scopeId ? { scopeId } : undefined,
      }),
      // Optimistic update: mark the matching assignment inactive immediately.
      // Match on both roleId AND scopeId so only the targeted assignment is hidden.
      async onQueryStarted({ tenantId, userId, roleId, scopeId }, { dispatch, queryFulfilled }) {
        const patchResult = dispatch(
          (apiSlice.util.updateQueryData as any)(
            'getUserRoleAssignments',
            { tenantId, userId },
            (draft: UserRoleAssignment[]) => {
              const assignment = draft.find(
                (a) => a.roleId === roleId && a.isActive && (a.scopeId ?? null) === (scopeId ?? null),
              );
              if (assignment) assignment.isActive = false;
            },
          ),
        );
        try {
          await queryFulfilled;
        } catch {
          patchResult.undo();
        }
      },
      invalidatesTags: (_r, _e, { userId }) => [
        { type: 'User', id: userId },
        { type: 'User', id: `roles-${userId}` },
      ],
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetUsersQuery,
  useGetUserByIdQuery,
  useGetUserRoleAssignmentsQuery,
  useCreateUserMutation,
  useUpdateUserMutation,
  useAssignRoleToUserMutation,
  useRevokeRoleFromUserMutation,
} = userEndpoints;
