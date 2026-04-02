import { useEffect, lazy, Suspense } from 'react';
import { NavLink, Outlet, useNavigate, useParams } from 'react-router-dom';
import { useAppDispatch, useAppSelector } from '@/app/hooks';
import { setTenantId, logout } from '@/features/auth/authSlice';
import { useLogoutMutation } from '@/features/auth/authEndpoints';
import { AbilityProvider } from '@/shared/contexts/AbilityContext';
import { useOnboardingStore } from '@/features/onboarding/onboardingStore';
import { cn } from '@/shared/utils/cn';

const OnboardingWizard = lazy(() => import('@/features/onboarding/components/OnboardingWizard'));

const navItems = [
  { label: 'Dashboard', path: 'dashboard' },
  { label: 'Roles', path: 'roles' },
  { label: 'Permissions', path: 'permissions' },
  { label: 'Users', path: 'users' },
  { label: 'Policies', path: 'policies' },
  { label: 'Delegations', path: 'delegations' },
  { label: 'Audit Log', path: 'audit' },
];

/**
 * Shell layout rendered under /:tenantId.
 *
 * Responsibilities:
 * 1. Syncs URL tenantId → Redux (AuthGuard already validated JWT match)
 * 2. Wraps all children in AbilityProvider — permission checks are available
 *    to every page component and the <Authorized> gate without prop drilling
 * 3. Renders sidebar nav + <Outlet /> for child routes
 * 4. Auto-shows the onboarding wizard for new tenants
 */
export default function TenantLayout() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { tenantId } = useParams<{ tenantId: string }>();
  const user = useAppSelector((s) => s.auth.user);
  const [logoutMutation] = useLogoutMutation();
  const { isCompleted, open: openWizard, isOpen } = useOnboardingStore();

  useEffect(() => {
    if (tenantId) {
      dispatch(setTenantId(tenantId));
    }
  }, [tenantId, dispatch]);

  // Auto-show wizard for users who haven't completed onboarding
  useEffect(() => {
    if (user && !isCompleted(user.id)) {
      openWizard();
    }
  }, [user?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleLogout = async () => {
    try {
      await logoutMutation().unwrap();
    } finally {
      dispatch(logout());
      navigate('/login', { replace: true });
    }
  };

  return (
    <AbilityProvider>
      <div className="min-h-screen flex bg-background">
        {/* Sidebar */}
        <aside
          className="w-56 flex-shrink-0 border-r bg-card flex flex-col"
          aria-label="Main navigation"
        >
          <div className="px-4 py-5 border-b">
            <p className="text-sm font-semibold">RBAC Admin</p>
            <p className="text-xs text-muted-foreground truncate">{user?.email}</p>
          </div>

          <nav className="flex-1 px-2 py-3 space-y-0.5" aria-label="Tenant navigation">
            {navItems.map((item) => (
              <NavLink
                key={item.path}
                to={`/${tenantId}/${item.path}`}
                className={({ isActive }) =>
                  cn(
                    'block px-3 py-2 rounded-md text-sm transition-colors',
                    isActive
                      ? 'bg-primary text-primary-foreground'
                      : 'text-foreground hover:bg-accent hover:text-accent-foreground'
                  )
                }
                aria-current={undefined}
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          <div className="px-4 py-3 border-t space-y-2">
            {user && isCompleted(user.id) && (
              <button
                onClick={openWizard}
                className="w-full text-left text-xs text-muted-foreground hover:text-foreground transition-colors"
                aria-label="Re-open setup wizard"
              >
                Get started guide
              </button>
            )}
            <button
              onClick={handleLogout}
              className="w-full text-left text-sm text-muted-foreground hover:text-foreground transition-colors"
            >
              Sign out
            </button>
          </div>
        </aside>

        {/* Main content */}
        <main className="flex-1 overflow-auto" id="main-content">
          <Outlet />
        </main>
      </div>

      {/* Onboarding wizard overlay — rendered at layout level so it covers the full viewport */}
      {isOpen && (
        <Suspense fallback={null}>
          <OnboardingWizard />
        </Suspense>
      )}
    </AbilityProvider>
  );
}
