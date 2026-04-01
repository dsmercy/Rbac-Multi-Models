import { useNavigate, useParams } from 'react-router-dom';
import { useEffect } from 'react';
import { useAppSelector } from '@/app/hooks';
import type { RouteGuardContext } from '@/routes/types';

interface RouteGuardProps {
  guard?: (context: RouteGuardContext) => boolean;
  children: React.ReactNode;
}

/**
 * Evaluates the optional route-level guard function.
 * If no guard is provided the route renders freely (auth is already handled by AuthGuard).
 * If the guard returns false, redirects back to the tenant dashboard.
 *
 * NOTE: This is a client-side visibility guard only.
 * Real permission enforcement lives in the backend on every API call.
 */
export default function RouteGuard({ guard, children }: RouteGuardProps) {
  const navigate = useNavigate();
  const { tenantId } = useParams<{ tenantId: string }>();
  const isAuthenticated = useAppSelector((s) => s.auth.isAuthenticated);
  const statetenantId = useAppSelector((s) => s.auth.tenantId);

  // Minimal can() stub — Phase 1 scaffold.
  // Phase 2 will replace this with a real AbilityContext backed by /permissions/check.
  const can = (_action: string, _resource: string): boolean => true;

  const context: RouteGuardContext = {
    isAuthenticated,
    tenantId: statetenantId,
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
