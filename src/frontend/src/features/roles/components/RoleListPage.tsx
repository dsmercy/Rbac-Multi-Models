import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useGetRolesQuery, useDeleteRoleMutation } from '../roleEndpoints';
import { useToastStore } from '@/shared/stores/toastStore';
import { Authorized } from '@/shared/components/Authorized';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { SkeletonTable } from '@/shared/components/Skeleton';

export default function RoleListPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const navigate = useNavigate();
  const toast = useToastStore();
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const { data: roles, isLoading, isError, refetch } = useGetRolesQuery(
    { tenantId: tenantId! }, { skip: !tenantId }
  );
  const [deleteRole, { isLoading: isDeleting }] = useDeleteRoleMutation();

  const handleDelete = async () => {
    if (!deletingId || !tenantId) return;
    try {
      await deleteRole({ tenantId, roleId: deletingId }).unwrap();
      toast.success('Role deleted', 'All assignments have been deactivated.');
    } catch {
      toast.error('Delete failed', 'Could not delete this role. Please try again.');
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Roles</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            {roles ? `${roles.length} role${roles.length !== 1 ? 's' : ''}` : 'Manage roles and their permissions'}
          </p>
        </div>
        <Authorized action="role:create" resource="roles">
          <button
            onClick={() => navigate(`/${tenantId}/roles/new`)}
            className="px-4 py-2 bg-primary text-primary-foreground text-sm font-medium rounded-md hover:bg-primary/90 transition-colors"
          >
            + New role
          </button>
        </Authorized>
      </div>

      {isLoading && <SkeletonTable rows={5} cols={4} />}

      {isError && (
        <div className="border border-red-200 bg-red-50 text-red-700 rounded-md px-4 py-3 text-sm flex justify-between">
          <span>Failed to load roles.</span>
          <button onClick={() => void refetch()} className="underline">Retry</button>
        </div>
      )}

      {!isLoading && !isError && roles?.length === 0 && (
        <EmptyState
          icon="🗂"
          title="No roles yet"
          description="Create your first role to start assigning permissions to users."
          action={
            <Authorized action="role:create" resource="roles">
              <button onClick={() => navigate(`/${tenantId}/roles/new`)}
                className="px-4 py-2 bg-primary text-primary-foreground text-sm rounded-md">
                Create role
              </button>
            </Authorized>
          }
        />
      )}

      {roles && roles.length > 0 && (
        <div className="border rounded-lg overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50 text-muted-foreground">
              <tr>
                <th className="text-left px-4 py-3 font-medium">Name</th>
                <th className="text-left px-4 py-3 font-medium">Description</th>
                <th className="text-left px-4 py-3 font-medium">Type</th>
                <th className="text-left px-4 py-3 font-medium">Created</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {roles.map((role) => (
                <tr key={role.id} className="hover:bg-muted/30 transition-colors">
                  <td className="px-4 py-3 font-medium">{role.name}</td>
                  <td className="px-4 py-3 text-muted-foreground max-w-xs truncate">{role.description ?? '—'}</td>
                  <td className="px-4 py-3">
                    {role.isSystem
                      ? <span className="text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full">System</span>
                      : <span className="text-xs bg-muted text-muted-foreground px-2 py-0.5 rounded-full">Custom</span>}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">{new Date(role.createdAt).toLocaleDateString()}</td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-2">
                      <Authorized action="role:update" resource="roles">
                        <button onClick={() => navigate(`/${tenantId}/roles/${role.id}/edit`)}
                          className="text-xs px-3 py-1 border rounded hover:bg-accent transition-colors">
                          Edit
                        </button>
                      </Authorized>
                      <Authorized action="role:delete" resource="roles" fallback={null}>
                        <button onClick={() => setDeletingId(role.id)} disabled={role.isSystem}
                          className="text-xs px-3 py-1 border border-red-200 text-red-600 rounded hover:bg-red-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed">
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
        title="Delete role"
        description="This will soft-delete the role and deactivate all user assignments. Cannot be undone."
        confirmLabel="Delete"
        destructive
        isLoading={isDeleting}
        onConfirm={handleDelete}
        onCancel={() => setDeletingId(null)}
      />
    </div>
  );
}
