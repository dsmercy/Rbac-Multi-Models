import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';

export const roleRoutes: AppRoute[] = [
  { path: 'roles', component: lazy(() => import('./components/RoleListPage')) },
  { path: 'roles/members', component: lazy(() => import('./components/RoleMembersPage')) },
  { path: 'roles/new', component: lazy(() => import('./components/RoleEditorPage')), guard: ({ can }) => can('role:create', 'roles') },
  { path: 'roles/:roleId/edit', component: lazy(() => import('./components/RoleEditorPage')), guard: ({ can }) => can('role:update', 'roles') },
];

export {
  useGetRolesQuery,
  useGetRoleByIdQuery,
  useCreateRoleMutation,
  useUpdateRoleMutation,
  useDeleteRoleMutation,
  useGetRolePermissionsQuery,
  useAssignPermissionToRoleMutation,
  useRevokePermissionFromRoleMutation,
  useGetRoleMembersQuery,
} from './roleEndpoints';

export { useRoleEditorStore } from './roleStore';
export type { Role, CreateRoleInput, UpdateRoleInput } from './types';
