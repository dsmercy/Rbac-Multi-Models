import { useNavigate, useParams } from 'react-router-dom';
import { useEffect } from 'react';
import { useAppSelector } from '@/app/hooks';
import { useAbility } from '@/shared/hooks/useAbility';
import type { RouteGuardContext } from '@/routes/types';

interface RouteGuardProps {
  guard?: (context: RouteGuardContext) => boolean;
  children: React.ReactNode;
}

/**
 * Evaluates the optional route-level guard function using the real AbilityContext.
 *
 * If no guard is defined the route renders freely (auth is handled by AuthGuard).
 * If the guard returns false, redirects to the tenant dashboard.
 *
 * NOTE: This is a client-side visibility guard only — a UX convenience.
 * The backend enforces every permission check authoritatively on every API call.
 * Never rely on this guard as a security boundary.
 */
export default function RouteGuard({ guard, children }: RouteGuardProps) {
  const navigate = useNavigate();
  const { tenantId } = useParams<{ tenantId: string }>();
  const isAuthenticated = useAppSelector((s) => s.auth.isAuthenticated);
  const stateTenantId = useAppSelector((s) => s.auth.tenantId);

  // Real can() backed by AbilityContext — populated from /permissions/check
  const { can } = useAbility();

  const context: RouteGuardContext = {
    isAuthenticated,
    tenantId: stateTenantId,
    can,
  };

  const allowed = !guard || guard(context);

  useEffect(() => {
    if (!allowed && tenantId) {
      navigate(`/${tenantId}/dashboard`, { replace: true });
    }
  }, [allowed, tenantId, navigate]);

  if (!allowed) return null;

  return <>{children}</>;
}
