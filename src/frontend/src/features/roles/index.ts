import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';

export const roleRoutes: AppRoute[] = [
  { path: 'roles', component: lazy(() => import('./components/RoleListPage')) },
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
} from './roleEndpoints';

export { useRoleEditorStore } from './roleStore';
export type { Role, CreateRoleInput, UpdateRoleInput } from './types';
