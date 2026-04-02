import { useParams, Link } from 'react-router-dom';
import { useAppSelector } from '@/app/hooks';
import { useGetRolesQuery } from '@/features/roles/roleEndpoints';
import { useGetUsersQuery } from '@/features/users/userEndpoints';
import { useGetPoliciesQuery } from '@/features/policies/policyEndpoints';
import { useGetDelegationsQuery } from '@/features/delegations/delegationEndpoints';
import { SkeletonBlock } from './Skeleton';

interface StatCardProps {
  label: string;
  value: number | undefined;
  isLoading: boolean;
  sub?: string;
}

function StatCard({ label, value, isLoading, sub }: StatCardProps) {
  return (
    <div className="border rounded-lg p-5 space-y-1">
      <p className="text-xs text-muted-foreground uppercase tracking-wide font-medium">{label}</p>
      {isLoading ? (
        <SkeletonBlock className="h-8 w-16" />
      ) : (
        <p className="text-3xl font-bold">{value ?? '—'}</p>
      )}
      {sub && <p className="text-xs text-muted-foreground">{sub}</p>}
    </div>
  );
}

export default function DashboardPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const user = useAppSelector((s) => s.auth.user);

  const { data: roles, isLoading: rolesLoading } = useGetRolesQuery(
    { tenantId: tenantId! }, { skip: !tenantId }
  );
  const { data: usersPage, isLoading: usersLoading } = useGetUsersQuery(
    { tenantId: tenantId!, pageSize: 1 }, { skip: !tenantId }
  );
  const { data: policies, isLoading: policiesLoading } = useGetPoliciesQuery(
    { tenantId: tenantId! }, { skip: !tenantId }
  );
  const { data: delegations, isLoading: delegationsLoading } = useGetDelegationsQuery(
    { tenantId: tenantId! }, { skip: !tenantId }
  );

  const activePolicies = policies?.filter((p) => p.isActive).length;
  const activeDelegations = delegations?.filter((d) => d.status === 'Active').length;
  const customRoles = roles?.filter((r) => !r.isSystem).length;

  return (
    <div className="p-6 space-y-6">
      {/* Welcome */}
      <div>
        <h1 className="text-xl font-semibold">Dashboard</h1>
        <p className="text-sm text-muted-foreground mt-0.5">
          Welcome back, <span className="font-medium text-foreground">{user?.displayName ?? user?.email}</span>
        </p>
      </div>

      {/* Summary stats */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4" role="status" aria-label="Tenant summary statistics">
        <StatCard
          label="Users"
          value={usersPage?.totalCount}
          isLoading={usersLoading}
          sub="in this tenant"
        />
        <StatCard
          label="Roles"
          value={roles?.length}
          isLoading={rolesLoading}
          sub={customRoles !== undefined ? `${customRoles} custom` : undefined}
        />
        <StatCard
          label="Active policies"
          value={activePolicies}
          isLoading={policiesLoading}
          sub="ABAC conditions"
        />
        <StatCard
          label="Active delegations"
          value={activeDelegations}
          isLoading={delegationsLoading}
          sub="time-bound grants"
        />
      </div>

      {/* Quick links */}
      <div className="border rounded-lg p-5 space-y-3">
        <h2 className="text-sm font-medium text-muted-foreground uppercase tracking-wide">Quick navigation</h2>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 text-sm">
          {[
            { label: 'Manage roles', path: `/${tenantId}/roles` },
            { label: 'Permission matrix', path: `/${tenantId}/permissions` },
            { label: 'Manage users', path: `/${tenantId}/users` },
            { label: 'ABAC policies', path: `/${tenantId}/policies` },
            { label: 'Delegations', path: `/${tenantId}/delegations` },
            { label: 'Audit logs', path: `/${tenantId}/audit` },
          ].map(({ label, path }) => (
            <Link
              key={path}
              to={path}
              className="px-3 py-2 border rounded-md hover:bg-accent transition-colors text-center"
            >
              {label}
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}
