import type { AppRoute } from './types';
import { roleRoutes } from '@/features/roles';
import { permissionRoutes } from '@/features/permissions';
import { userRoutes } from '@/features/users';
import { policyRoutes } from '@/features/policies';
import { delegationRoutes } from '@/features/delegations';
import { auditRoutes } from '@/features/audit';
import { notificationRoutes } from '@/features/notifications';

/**
 * All tenant-scoped routes composed from feature modules.
 * These are mounted under /:tenantId by TenantLayout in router.tsx.
 * Each route is lazy-loaded and optionally guarded.
 */
export const tenantRoutes: AppRoute[] = [
  ...roleRoutes,
  ...permissionRoutes,
  ...userRoutes,
  ...policyRoutes,
  ...delegationRoutes,
  ...auditRoutes,
  ...notificationRoutes,
];
