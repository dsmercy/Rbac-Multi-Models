import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';
export const auditRoutes: AppRoute[] = [
  { path: 'audit', component: lazy(() => import('./components/AuditLogViewerPage')) },
];
