import { useEffect } from 'react';
import { NavLink, Outlet, useNavigate, useParams } from 'react-router-dom';
import { useAppDispatch, useAppSelector } from '@/app/hooks';
import { setTenantId, logout } from '@/features/auth/authSlice';
import { useLogoutMutation } from '@/features/auth/authEndpoints';
import { AbilityProvider } from '@/shared/contexts/AbilityContext';
import { cn } from '@/shared/utils/cn';

const navItems = [
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
 */
export default function TenantLayout() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { tenantId } = useParams<{ tenantId: string }>();
  const user = useAppSelector((s) => s.auth.user);
  const [logoutMutation] = useLogoutMutation();

  useEffect(() => {
    if (tenantId) {
      dispatch(setTenantId(tenantId));
    }
  }, [tenantId, dispatch]);

  const handleLogout = async () => {
    try {
      await logoutMutation().unwrap();
    } finally {
      dispatch(logout());
      navigate('/login', { replace: true });
    }
  };

  return (
    // AbilityProvider scoped to the tenant — all child routes can call useAbility()
    <AbilityProvider>
      <div className="min-h-screen flex bg-background">
        {/* Sidebar */}
        <aside className="w-56 flex-shrink-0 border-r bg-card flex flex-col">
          <div className="px-4 py-5 border-b">
            <p className="text-sm font-semibold">RBAC Admin</p>
            <p className="text-xs text-muted-foreground truncate">{user?.email}</p>
          </div>

          <nav className="flex-1 px-2 py-3 space-y-0.5">
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
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          <div className="px-4 py-3 border-t">
            <button
              onClick={handleLogout}
              className="w-full text-left text-sm text-muted-foreground hover:text-foreground transition-colors"
            >
              Sign out
            </button>
          </div>
        </aside>

        {/* Main content */}
        <main className="flex-1 overflow-auto">
          <Outlet />
        </main>
      </div>
    </AbilityProvider>
  );
}
