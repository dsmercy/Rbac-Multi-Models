import { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useGetMeQuery } from '@/features/auth/authEndpoints';
import { useAppDispatch, useAppSelector } from '@/app/hooks';
import { setLoading } from '@/features/auth/authSlice';
import { Skeleton } from './Skeleton';

interface AuthGuardProps {
  children: React.ReactNode;
}

/**
 * Validates session on mount via GET /auth/me (httpOnly cookie).
 * Populates Redux auth state on success.
 * Redirects to /login on 401.
 * Shows skeleton while the check is in-flight.
 */
export default function AuthGuard({ children }: AuthGuardProps) {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { tenantId } = useParams<{ tenantId: string }>();

  const isAuthenticated = useAppSelector((s) => s.auth.isAuthenticated);
  const isLoading = useAppSelector((s) => s.auth.isLoading);
  const user = useAppSelector((s) => s.auth.user);

  const { data: profile, error, isLoading: isFetching } = useGetMeQuery(undefined, {
    skip: isAuthenticated, // already have a valid session in this tab
  });

  // On boot, /auth/me will fail (no token in memory after page refresh) →
  // error effect redirects to /login. This is expected — token is in-memory only.
  useEffect(() => {
    if (profile) {
      // profile loaded on boot — accessToken is null (page refresh scenario),
      // so auth state stays read-only. Full re-login is required after refresh.
      dispatch(setLoading(false));
    }
  }, [profile, dispatch]);

  useEffect(() => {
    if (error) {
      dispatch(setLoading(false));
      navigate('/login', { replace: true });
    }
  }, [error, dispatch, navigate]);

  // Still resolving session on boot
  if (isFetching || isLoading) return <Skeleton />;

  if (!isAuthenticated) return null;

  // Validate URL tenantId matches the user's tenantId (superAdmin can access any)
  if (tenantId && user && user.tenantId !== tenantId && !user.isSuperAdmin) {
    navigate('/login', { replace: true });
    return null;
  }

  return <>{children}</>;
}
