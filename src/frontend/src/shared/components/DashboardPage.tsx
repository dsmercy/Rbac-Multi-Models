import { useAppSelector } from '@/app/hooks';

export default function DashboardPage() {
  const user = useAppSelector((s) => s.auth.user);

  return (
    <div className="p-8 space-y-4">
      <h1 className="text-2xl font-semibold">Dashboard</h1>
      <p className="text-muted-foreground">
        Welcome, <span className="font-medium text-foreground">{user?.displayName ?? user?.email}</span>
      </p>
      <p className="text-sm text-muted-foreground">
        Use the sidebar to manage roles, permissions, users, policies, delegations, and audit logs.
      </p>
    </div>
  );
}
