import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';

export const auditRoutes: AppRoute[] = [
  { path: 'audit', component: lazy(() => import('./components/AuditLogViewerPage')) },
];

export {
  useGetAuditLogsQuery,
  useExportAuditLogsMutation,
} from './auditEndpoints';

export { useAuditLogFilterStore } from './auditStore';
export type { AuditLog, AuditLogFilters } from './types';
