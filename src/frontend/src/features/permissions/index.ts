import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';
export const permissionRoutes: AppRoute[] = [
  { path: 'permissions', component: lazy(() => import('./components/PermissionMatrixPage')) },
];
