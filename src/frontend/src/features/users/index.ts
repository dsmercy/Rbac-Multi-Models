import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';
export const userRoutes: AppRoute[] = [
  { path: 'users', component: lazy(() => import('./components/UserListPage')) },
  { path: 'users/:userId', component: lazy(() => import('./components/UserDetailPage')) },
];
