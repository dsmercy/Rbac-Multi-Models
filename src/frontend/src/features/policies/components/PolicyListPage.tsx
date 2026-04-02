import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useGetPoliciesQuery, useDeletePolicyMutation, useUpdatePolicyMutation } from '../policyEndpoints';
import { useToastStore } from '@/shared/stores/toastStore';
import { Authorized } from '@/shared/components/Authorized';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { SkeletonTable } from '@/shared/components/Skeleton';
import type { Policy } from '../types';

export default function PolicyListPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const navigate = useNavigate();
  const toast = useToastStore();
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const { data: policies, isLoading, isError, refetch } = useGetPoliciesQuery(
    { tenantId: tenantId! }, { skip: !tenantId }
  );
  const [deletePolicy, { isLoading: isDeleting }] = useDeletePolicyMutation();
  const [updatePolicy] = useUpdatePolicyMutation();

  const handleDelete = async () => {
    if (!deletingId || !tenantId) return;
    try {
      await deletePolicy({ tenantId, policyId: deletingId }).unwrap();
      toast.success('Policy deleted');
    } catch {
      toast.error('Delete failed', 'Could not delete this policy. Please try again.');
    } finally {
      setDeletingId(null);
    }
  };

  const handleToggleActive = async (policy: Policy) => {
    if (!tenantId) return;
    try {
      await updatePolicy({ tenantId, policyId: policy.id, body: { isActive: !policy.isActive } }).unwrap();
      toast.success(policy.isActive ? 'Policy deactivated' : 'Policy activated');
    } catch {
      toast.error('Update failed', 'Could not update policy status.');
    }
  };

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Policies</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            {policies ? `${policies.length} ABAC polic${policies.length !== 1 ? 'ies' : 'y'}` : 'JSON condition tree policies'}
          </p>
        </div>
        <Authorized action="policy:create" resource="policies">
          <button
            onClick={() => navigate(`/${tenantId}/policies/new`)}
            className="px-4 py-2 bg-primary text-primary-foreground text-sm font-medium rounded-md hover:bg-primary/90 transition-colors"
          >
            + New policy
          </button>
        </Authorized>
      </div>

      {isLoading && <SkeletonTable rows={4} cols={5} />}

      {isError && (
        <div className="border border-red-200 bg-red-50 text-red-700 rounded-md px-4 py-3 text-sm flex justify-between">
          <span>Failed to load policies.</span>
          <button onClick={() => void refetch()} className="underline">Retry</button>
        </div>
      )}

      {!isLoading && !isError && policies?.length === 0 && (
        <EmptyState
          icon="📋"
          title="No policies yet"
          description="Create ABAC policies to add attribute-based conditions to access control."
          action={
            <Authorized action="policy:create" resource="policies">
              <button onClick={() => navigate(`/${tenantId}/policies/new`)} className="px-4 py-2 bg-primary text-primary-foreground text-sm rounded-md">
                Create policy
              </button>
            </Authorized>
          }
        />
      )}

      {policies && policies.length > 0 && (
        <div className="border rounded-lg overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50 text-muted-foreground">
              <tr>
                <th className="text-left px-4 py-3 font-medium">Name</th>
                <th className="text-left px-4 py-3 font-medium">Effect</th>
                <th className="text-left px-4 py-3 font-medium">Status</th>
                <th className="text-left px-4 py-3 font-medium">Updated</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {policies.map((policy) => (
                <tr key={policy.id} className="hover:bg-muted/30 transition-colors">
                  <td className="px-4 py-3">
                    <p className="font-medium">{policy.name}</p>
                    {policy.description && <p className="text-xs text-muted-foreground truncate max-w-xs">{policy.description}</p>}
                  </td>
                  <td className="px-4 py-3">
                    {policy.effect === 'Allow'
                      ? <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded-full">Allow</span>
                      : <span className="text-xs bg-red-100 text-red-600 px-2 py-0.5 rounded-full">Deny</span>}
                  </td>
                  <td className="px-4 py-3">
                    <Authorized action="policy:update" resource="policies" fallback={
                      <span className={`text-xs px-2 py-0.5 rounded-full ${policy.isActive ? 'bg-blue-100 text-blue-700' : 'bg-muted text-muted-foreground'}`}>
                        {policy.isActive ? 'Active' : 'Inactive'}
                      </span>
                    }>
                      <button
                        onClick={() => void handleToggleActive(policy)}
                        className={`text-xs px-2 py-0.5 rounded-full border transition-colors ${
                          policy.isActive
                            ? 'bg-blue-100 text-blue-700 border-blue-200 hover:bg-blue-200'
                            : 'bg-muted text-muted-foreground border-muted hover:bg-accent'
                        }`}
                      >
                        {policy.isActive ? 'Active' : 'Inactive'}
                      </button>
                    </Authorized>
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {policy.updatedAt ? new Date(policy.updatedAt).toLocaleDateString() : new Date(policy.createdAt).toLocaleDateString()}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-2">
                      <Authorized action="policy:update" resource="policies">
                        <button
                          onClick={() => navigate(`/${tenantId}/policies/${policy.id}/edit`)}
                          className="text-xs px-3 py-1 border rounded hover:bg-accent transition-colors"
                        >
                          Edit
                        </button>
                      </Authorized>
                      <Authorized action="policy:delete" resource="policies" fallback={null}>
                        <button
                          onClick={() => setDeletingId(policy.id)}
                          className="text-xs px-3 py-1 border border-red-200 text-red-600 rounded hover:bg-red-50 transition-colors"
                        >
                          Delete
                        </button>
                      </Authorized>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <ConfirmDialog
        open={!!deletingId}
        title="Delete policy"
        description="This will remove the policy from all permission evaluations. Cannot be undone."
        confirmLabel="Delete"
        destructive
        isLoading={isDeleting}
        onConfirm={handleDelete}
        onCancel={() => setDeletingId(null)}
      />
    </div>
  );
}
