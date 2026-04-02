import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  useGetUserByIdQuery,
  useUpdateUserMutation,
  useAssignRoleToUserMutation,
  useRevokeRoleFromUserMutation,
} from '../userEndpoints';
import { useGetRolesQuery } from '@/features/roles/roleEndpoints';
import { assignRoleSchema, type AssignRoleSchema } from '../schemas';
import { useToastStore } from '@/shared/stores/toastStore';
import { Authorized } from '@/shared/components/Authorized';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { ScopeTreePicker } from '@/shared/components/ScopeTreePicker';
import { SkeletonBlock } from '@/shared/components/Skeleton';

export default function UserDetailPage() {
  const { tenantId, userId } = useParams<{ tenantId: string; userId: string }>();
  const navigate = useNavigate();
  const toast = useToastStore();
  const [revokingRoleId, setRevokingRoleId] = useState<string | null>(null);
  const [showAssignForm, setShowAssignForm] = useState(false);

  const { data: user, isLoading, isError, refetch } = useGetUserByIdQuery(
    { tenantId: tenantId!, userId: userId! },
    { skip: !tenantId || !userId || userId === 'new' }
  );
  const { data: roles = [] } = useGetRolesQuery({ tenantId: tenantId! }, { skip: !tenantId });
  const [updateUser, { isLoading: isUpdating }] = useUpdateUserMutation();
  const [assignRole, { isLoading: isAssigning }] = useAssignRoleToUserMutation();
  const [revokeRole, { isLoading: isRevoking }] = useRevokeRoleFromUserMutation();

  const { register, handleSubmit, setValue, watch, reset, formState: { errors } } = useForm<AssignRoleSchema>({
    resolver: zodResolver(assignRoleSchema),
    defaultValues: { roleId: '', scopeId: '' },
  });

  const scopeId = watch('scopeId');

  const handleToggleActive = async () => {
    if (!user || !tenantId || !userId) return;
    try {
      await updateUser({ tenantId, userId, body: { isActive: !user.isActive } }).unwrap();
      toast.success(user.isActive ? 'User deactivated' : 'User activated');
    } catch {
      toast.error('Update failed', 'Could not update user status.');
    }
  };

  const handleAssignRole = async (data: AssignRoleSchema) => {
    if (!tenantId || !userId) return;
    try {
      await assignRole({ tenantId, userId, body: data }).unwrap();
      toast.success('Role assigned', 'Role has been assigned to the user.');
      setShowAssignForm(false);
      reset();
    } catch {
      toast.error('Assignment failed', 'Could not assign role. Please try again.');
    }
  };

  const handleRevokeRole = async () => {
    if (!revokingRoleId || !tenantId || !userId) return;
    try {
      await revokeRole({ tenantId, userId, roleId: revokingRoleId }).unwrap();
      toast.success('Role revoked', 'Role assignment removed.');
    } catch {
      toast.error('Revoke failed', 'Could not revoke role. Please try again.');
    } finally {
      setRevokingRoleId(null);
    }
  };

  if (isLoading) {
    return (
      <div className="p-6 space-y-4">
        <SkeletonBlock className="h-6 w-48" />
        <SkeletonBlock className="h-32 w-full" />
      </div>
    );
  }

  if (isError) {
    return (
      <div className="p-6">
        <div className="border border-red-200 bg-red-50 text-red-700 rounded-md px-4 py-3 text-sm flex justify-between">
          <span>Failed to load user.</span>
          <button onClick={() => void refetch()} className="underline">Retry</button>
        </div>
      </div>
    );
  }

  if (!user) return <div className="p-6 text-muted-foreground">User not found.</div>;

  return (
    <div className="p-6 max-w-3xl space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(`/${tenantId}/users`)} className="text-muted-foreground hover:text-foreground transition-colors text-sm">
          ← Users
        </button>
        <span className="text-muted-foreground">/</span>
        <h1 className="text-xl font-semibold">{user.displayName || user.email}</h1>
        {user.isActive
          ? <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded-full">Active</span>
          : <span className="text-xs bg-red-100 text-red-600 px-2 py-0.5 rounded-full">Inactive</span>}
      </div>

      {/* User info card */}
      <div className="border rounded-lg p-5 space-y-3">
        <h2 className="text-sm font-medium text-muted-foreground uppercase tracking-wide">Profile</h2>
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <p className="text-muted-foreground text-xs">Email</p>
            <p className="font-medium">{user.email}</p>
          </div>
          <div>
            <p className="text-muted-foreground text-xs">Display name</p>
            <p className="font-medium">{user.displayName || '—'}</p>
          </div>
          <div>
            <p className="text-muted-foreground text-xs">Created</p>
            <p>{new Date(user.createdAt).toLocaleDateString()}</p>
          </div>
        </div>
        <Authorized action="user:update" resource="users">
          <div className="pt-2">
            <button
              onClick={() => void handleToggleActive()}
              disabled={isUpdating}
              className={`text-xs px-3 py-1.5 border rounded-md transition-colors disabled:opacity-50 ${
                user.isActive ? 'border-red-200 text-red-600 hover:bg-red-50' : 'hover:bg-accent'
              }`}
            >
              {isUpdating ? 'Saving…' : user.isActive ? 'Deactivate user' : 'Activate user'}
            </button>
          </div>
        </Authorized>
      </div>

      {/* Role assignments */}
      <div className="border rounded-lg overflow-hidden">
        <div className="px-5 py-3 bg-muted/50 border-b flex items-center justify-between">
          <div>
            <h2 className="text-sm font-medium">Role assignments</h2>
            <p className="text-xs text-muted-foreground">Roles assigned to this user.</p>
          </div>
          <Authorized action="user:update" resource="users">
            <button onClick={() => setShowAssignForm((s) => !s)} className="text-xs px-3 py-1.5 border rounded-md hover:bg-accent transition-colors">
              + Assign role
            </button>
          </Authorized>
        </div>

        {showAssignForm && (
          <form onSubmit={handleSubmit(handleAssignRole)} className="p-5 space-y-4 border-b bg-muted/10">
            <div className="space-y-1">
              <label className="text-xs font-medium">Role</label>
              <select {...register('roleId')} className="w-full border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring">
                <option value="">Select a role…</option>
                {roles.map((r) => (
                  <option key={r.id} value={r.id}>{r.name}</option>
                ))}
              </select>
              {errors.roleId && <p className="text-xs text-red-500">{errors.roleId.message}</p>}
            </div>

            <div className="space-y-1">
              <label className="text-xs font-medium">Scope</label>
              <ScopeTreePicker value={scopeId} onChange={(id) => setValue('scopeId', id)} />
              {errors.scopeId && <p className="text-xs text-red-500">{errors.scopeId.message}</p>}
            </div>

            <div className="space-y-1">
              <label className="text-xs font-medium">Expires at (optional)</label>
              <input type="datetime-local" {...register('expiresAt')} className="w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
              {errors.expiresAt && <p className="text-xs text-red-500">{errors.expiresAt.message}</p>}
            </div>

            <div className="flex justify-end gap-2">
              <button type="button" onClick={() => { setShowAssignForm(false); reset(); }} className="px-3 py-1.5 text-sm border rounded-md hover:bg-accent">Cancel</button>
              <button type="submit" disabled={isAssigning} className="px-3 py-1.5 text-sm bg-primary text-primary-foreground rounded-md hover:bg-primary/90 disabled:opacity-50">
                {isAssigning ? 'Assigning…' : 'Assign'}
              </button>
            </div>
          </form>
        )}

        {/* We'd normally get assignments from a separate endpoint — showing placeholder */}
        <div className="px-5 py-8 text-center text-sm text-muted-foreground">
          Role assignments are loaded from the user's profile. Use the assign form above to add roles.
        </div>
      </div>

      <ConfirmDialog
        open={!!revokingRoleId}
        title="Revoke role"
        description="This will deactivate the role assignment for this user. The user will lose access associated with this role."
        confirmLabel="Revoke"
        destructive
        isLoading={isRevoking}
        onConfirm={handleRevokeRole}
        onCancel={() => setRevokingRoleId(null)}
      />
    </div>
  );
}
