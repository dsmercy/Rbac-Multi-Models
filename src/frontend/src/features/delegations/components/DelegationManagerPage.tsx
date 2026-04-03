import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useGetDelegationsQuery, useCreateDelegationMutation, useRevokeDelegationMutation } from '../delegationEndpoints';
import { useGetPermissionsQuery } from '@/features/permissions/permissionEndpoints';
import { useGetUsersQuery } from '@/features/users/userEndpoints';
import { createDelegationSchema, type CreateDelegationSchema } from '../schemas';
import { useToastStore } from '@/shared/stores/toastStore';
import { Authorized } from '@/shared/components/Authorized';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { ScopeTreePicker } from '@/shared/components/ScopeTreePicker';
import { SkeletonTable } from '@/shared/components/Skeleton';
import type { Delegation } from '../types';

const STATUS_STYLE: Record<string, string> = {
  Active: 'bg-green-100 text-green-700',
  Revoked: 'bg-red-100 text-red-600',
  Expired: 'bg-muted text-muted-foreground',
};

export default function DelegationManagerPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const toast = useToastStore();
  const [showForm, setShowForm] = useState(false);
  const [revokingDelegation, setRevokingDelegation] = useState<Delegation | null>(null);

  const { data: delegations, isLoading, isError, refetch } = useGetDelegationsQuery(
    { tenantId: tenantId! }, { skip: !tenantId }
  );
  const { data: permissions = [] } = useGetPermissionsQuery({ tenantId: tenantId! }, { skip: !tenantId });
  const { data: usersPage } = useGetUsersQuery({ tenantId: tenantId!, pageSize: 100 }, { skip: !tenantId });
  const users = usersPage?.items ?? [];

  const [createDelegation, { isLoading: isCreating }] = useCreateDelegationMutation();
  const [revokeDelegation, { isLoading: isRevoking }] = useRevokeDelegationMutation();

  const { register, handleSubmit, setValue, watch, reset, formState: { errors } } = useForm<CreateDelegationSchema>({
    resolver: zodResolver(createDelegationSchema),
    defaultValues: { permissionIds: [] },
  });

  const rawPermIds = watch('permissionIds');
  const selectedPermIds: string[] = Array.isArray(rawPermIds) ? rawPermIds : [];
  const scopeId = (watch('scopeId') as string | undefined) ?? null;

  const togglePermission = (id: string) => {
    const current = selectedPermIds;
    setValue(
      'permissionIds',
      current.includes(id) ? current.filter((p) => p !== id) : [...current, id],
      { shouldValidate: true }
    );
  };

  const onSubmit = async (data: CreateDelegationSchema) => {
    if (!tenantId) return;
    try {
      const body = {
        ...data,
        // Convert datetime-local string ("YYYY-MM-DDTHH:mm") to full ISO 8601
        expiresAt: new Date(data.expiresAt).toISOString(),
      };
      await createDelegation({ tenantId, body }).unwrap();
      toast.success('Delegation created', 'Permissions delegated successfully.');
      setShowForm(false);
      reset();
    } catch {
      toast.error('Delegation failed', 'Could not create delegation. Please try again.');
    }
  };

  const handleRevoke = async () => {
    if (!revokingDelegation || !tenantId) return;
    try {
      await revokeDelegation({ tenantId, delegationId: revokingDelegation.id, delegateeId: revokingDelegation.delegateeId }).unwrap();
      toast.success('Delegation revoked', 'Access has been revoked immediately.');
    } catch {
      toast.error('Revoke failed', 'Could not revoke delegation. Please try again.');
    } finally {
      setRevokingDelegation(null);
    }
  };

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Delegations</h1>
          <p className="text-sm text-muted-foreground mt-0.5">Time-bound permission delegation between users.</p>
        </div>
        <Authorized action="delegation:create" resource="delegations">
          <button onClick={() => setShowForm(true)} className="px-4 py-2 bg-primary text-primary-foreground text-sm font-medium rounded-md hover:bg-primary/90 transition-colors">
            + New delegation
          </button>
        </Authorized>
      </div>

      {/* Create form */}
      {showForm && (
        <form onSubmit={handleSubmit(onSubmit)} className="border rounded-lg p-5 space-y-4 bg-muted/10">
          <h3 className="text-sm font-semibold">Delegate permissions</h3>

          <div className="space-y-1">
            <label className="text-xs font-medium">Delegatee (user to delegate to)</label>
            <select {...register('delegateeUserId')} className="w-full border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring">
              <option value="">Select a user…</option>
              {users.map((u) => <option key={u.id} value={u.id}>{u.displayName || u.email}</option>)}
            </select>
            {errors.delegateeUserId && <p className="text-xs text-red-500">{errors.delegateeUserId.message}</p>}
          </div>

          <div className="space-y-1">
            <label className="text-xs font-medium">Permissions to delegate</label>
            <div className="border rounded-md p-3 max-h-40 overflow-y-auto space-y-1">
              {permissions.length === 0
                ? <p className="text-xs text-muted-foreground">No permissions available.</p>
                : permissions.map((p) => (
                  <label key={p.id} className="flex items-center gap-2 text-xs cursor-pointer">
                    <input
                      type="checkbox"
                      checked={selectedPermIds.includes(p.id)}
                      onChange={() => togglePermission(p.id)}
                      className="rounded"
                    />
                    <span>{p.action}</span>
                    <span className="text-muted-foreground">({p.resourceType})</span>
                  </label>
                ))
              }
            </div>
            {errors.permissionIds && <p className="text-xs text-red-500">{errors.permissionIds.message}</p>}
          </div>

          <div className="space-y-1">
            <label className="text-xs font-medium">Scope</label>
            <ScopeTreePicker value={scopeId} onChange={(id) => setValue('scopeId', id)} />
            {errors.scopeId && <p className="text-xs text-red-500">{errors.scopeId.message}</p>}
          </div>

          <div className="space-y-1">
            <label className="text-xs font-medium">Expires at</label>
            <input type="datetime-local" {...register('expiresAt')} className="w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
            {errors.expiresAt && <p className="text-xs text-red-500">{errors.expiresAt.message}</p>}
          </div>

          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => { setShowForm(false); reset(); }} className="px-3 py-1.5 text-sm border rounded-md hover:bg-accent">Cancel</button>
            <button type="submit" disabled={isCreating} className="px-3 py-1.5 text-sm bg-primary text-primary-foreground rounded-md hover:bg-primary/90 disabled:opacity-50">
              {isCreating ? 'Delegating…' : 'Create delegation'}
            </button>
          </div>
        </form>
      )}

      {isLoading && <SkeletonTable rows={4} cols={5} />}

      {isError && (
        <div className="border border-red-200 bg-red-50 text-red-700 rounded-md px-4 py-3 text-sm flex justify-between">
          <span>Failed to load delegations.</span>
          <button onClick={() => void refetch()} className="underline">Retry</button>
        </div>
      )}

      {!isLoading && !isError && delegations?.length === 0 && (
        <EmptyState
          icon="🔁"
          title="No delegations"
          description="Delegate permissions temporarily to another user for time-bound access."
        />
      )}

      {delegations && delegations.length > 0 && (
        <div className="border rounded-lg overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50 text-muted-foreground">
              <tr>
                <th className="text-left px-4 py-3 font-medium">Delegatee</th>
                <th className="text-left px-4 py-3 font-medium">Permissions</th>
                <th className="text-left px-4 py-3 font-medium">Status</th>
                <th className="text-left px-4 py-3 font-medium">Expires</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {delegations.map((d) => (
                <tr key={d.id} className="hover:bg-muted/30 transition-colors">
                  <td className="px-4 py-3 font-medium text-sm">
                    {(() => { const u = users.find((x) => x.id === d.delegateeId); return u ? (u.displayName || u.email) : d.delegateeId.slice(0, 8) + '…'; })()}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">{(d.permissionCodes ?? []).length} permission{(d.permissionCodes ?? []).length !== 1 ? 's' : ''}</td>
                  <td className="px-4 py-3">
                    <span className={`text-xs px-2 py-0.5 rounded-full ${STATUS_STYLE[d.status] ?? 'bg-muted text-muted-foreground'}`}>
                      {d.status}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">{new Date(d.expiresAt).toLocaleString()}</td>
                  <td className="px-4 py-3">
                    {d.status === 'Active' && (
                      <Authorized action="delegation:delete" resource="delegations" fallback={null}>
                        <button onClick={() => setRevokingDelegation(d)} className="text-xs px-3 py-1 border border-red-200 text-red-600 rounded hover:bg-red-50 transition-colors">
                          Revoke
                        </button>
                      </Authorized>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <ConfirmDialog
        open={!!revokingDelegation}
        title="Revoke delegation"
        description="This will immediately revoke the delegation and bust the permission cache for the delegatee."
        confirmLabel="Revoke"
        destructive
        isLoading={isRevoking}
        onConfirm={handleRevoke}
        onCancel={() => setRevokingDelegation(null)}
      />
    </div>
  );
}
