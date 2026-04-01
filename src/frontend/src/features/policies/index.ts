import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';
export const policyRoutes: AppRoute[] = [
  { path: 'policies', component: lazy(() => import('./components/PolicyListPage')) },
  { path: 'policies/new', component: lazy(() => import('./components/PolicyBuilderPage')), guard: ({ can }) => can('policy:create', 'policies') },
  { path: 'policies/:policyId', component: lazy(() => import('./components/PolicyBuilderPage')), guard: ({ can }) => can('policy:update', 'policies') },
];
