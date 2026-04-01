import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';
export const delegationRoutes: AppRoute[] = [
  { path: 'delegations', component: lazy(() => import('./components/DelegationManagerPage')) },
];
