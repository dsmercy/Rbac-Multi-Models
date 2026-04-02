import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';

export const permissionRoutes: AppRoute[] = [
  { path: 'permissions', component: lazy(() => import('./components/PermissionMatrixPage')) },
];

export {
  useGetPermissionsQuery,
  useCreatePermissionMutation,
  useCheckPermissionMutation,
} from './permissionEndpoints';

export { usePermissionMatrixStore } from './permissionStore';
export type { Permission, CreatePermissionInput } from './types';
