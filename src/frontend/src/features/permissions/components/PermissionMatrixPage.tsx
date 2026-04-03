import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useGetPermissionsQuery, useCreatePermissionMutation } from '../permissionEndpoints';
import { useGetRolesQuery, useGetRolePermissionsQuery } from '@/features/roles/roleEndpoints';
import { useToastStore } from '@/shared/stores/toastStore';
import { Authorized } from '@/shared/components/Authorized';
import { EmptyState } from '@/shared/components/EmptyState';
import { SkeletonTable } from '@/shared/components/Skeleton';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { createPermissionSchema, type CreatePermissionSchema } from '../schemas';
import type { Role } from '@/features/roles/types';

/** Fetches role permissions and returns assigned permission ids for a single role */
function RolePermissionCell({ tenantId, roleId, permissionId }: { tenantId: string; roleId: string; permissionId: string }) {
  const { data: rolePerms = [] } = useGetRolePermissionsQuery({ tenantId, roleId });
  const has = rolePerms.some((p) => p.id === permissionId);
  return (
    <td className="px-4 py-3 text-center" role="cell">
      {has
        ? <span className="inline-block w-4 h-4 rounded-full bg-green-500" role="img" aria-label="Granted" />
        : <span className="inline-block w-4 h-4 rounded-full bg-muted" role="img" aria-label="Not granted" />}
    </td>
  );
}

function AddPermissionForm({ tenantId, onClose }: { tenantId: string; onClose: () => void }) {
  const toast = useToastStore();
  const [createPermission, { isLoading }] = useCreatePermissionMutation();
  const { register, handleSubmit, formState: { errors } } = useForm<CreatePermissionSchema>({
    resolver: zodResolver(createPermissionSchema),
  });

  const onSubmit = async (data: CreatePermissionSchema) => {
    try {
      // `action` is already in "resource:verb" format (enforced by the schema regex).
      // Use it directly as the permission code; split out the verb for the backend's Action field.
      const code = data.action.trim().toLowerCase();
      const verb = code.split(':')[1] ?? code;
      await createPermission({ tenantId, body: { code, action: verb, resourceType: data.resourceType, description: data.description } }).unwrap();
      toast.success('Permission created', `${data.action} on ${data.resourceType} is now available.`);
      onClose();
    } catch {
      toast.error('Create failed', 'Could not create permission. Please try again.');
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="border rounded-lg p-5 space-y-4 bg-muted/20">
      <h3 className="text-sm font-semibold">New permission</h3>
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-1">
          <label className="text-xs font-medium">Action</label>
          <input {...register('action')} placeholder="e.g. user:read" className="w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
          {errors.action && <p className="text-xs text-red-500">{errors.action.message}</p>}
        </div>
        <div className="space-y-1">
          <label className="text-xs font-medium">Resource type</label>
          <input {...register('resourceType')} placeholder="e.g. users" className="w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
          {errors.resourceType && <p className="text-xs text-red-500">{errors.resourceType.message}</p>}
        </div>
      </div>
      <div className="space-y-1">
        <label className="text-xs font-medium">Description (optional)</label>
        <input {...register('description')} placeholder="What this permission allows" className="w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
      </div>
      <div className="flex justify-end gap-2">
        <button type="button" onClick={onClose} className="px-3 py-1.5 text-sm border rounded-md hover:bg-accent">Cancel</button>
        <button type="submit" disabled={isLoading} className="px-3 py-1.5 text-sm bg-primary text-primary-foreground rounded-md hover:bg-primary/90 disabled:opacity-50">
          {isLoading ? 'Creating…' : 'Create'}
        </button>
      </div>
    </form>
  );
}

export default function PermissionMatrixPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const [showForm, setShowForm] = useState(false);
  const [scopeFilter] = useState<string>('');

  const { data: permissions = [], isLoading: permsLoading, isError: permsError, refetch } = useGetPermissionsQuery(
    { tenantId: tenantId! }, { skip: !tenantId }
  );
  const { data: roles = [], isLoading: rolesLoading } = useGetRolesQuery(
    { tenantId: tenantId! }, { skip: !tenantId }
  );

  const isLoading = permsLoading || rolesLoading;

  const permissionsByResource: Record<string, typeof permissions> = {};
  for (const p of permissions) {
    (permissionsByResource[p.resourceType] ??= []).push(p);
  }

  const activeRoles = roles.filter((r: Role) => !scopeFilter || r.id === scopeFilter);

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Permission Matrix</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            {permissions.length > 0
              ? `${permissions.length} permission${permissions.length !== 1 ? 's' : ''} across ${roles.length} role${roles.length !== 1 ? 's' : ''}`
              : 'Roles × permissions grid'}
          </p>
        </div>
        <Authorized action="permission:create" resource="permissions">
          <button onClick={() => setShowForm(true)} className="px-4 py-2 bg-primary text-primary-foreground text-sm font-medium rounded-md hover:bg-primary/90 transition-colors">
            + New permission
          </button>
        </Authorized>
      </div>

      {showForm && tenantId && (
        <AddPermissionForm tenantId={tenantId} onClose={() => setShowForm(false)} />
      )}

      {isLoading && <SkeletonTable rows={6} cols={roles.length + 1 || 4} />}

      {permsError && (
        <div className="border border-red-200 bg-red-50 text-red-700 rounded-md px-4 py-3 text-sm flex justify-between">
          <span>Failed to load permissions.</span>
          <button onClick={() => void refetch()} className="underline">Retry</button>
        </div>
      )}

      {!isLoading && !permsError && permissions.length === 0 && (
        <EmptyState
          icon="🔑"
          title="No permissions yet"
          description="Create permissions to define what actions can be granted to roles."
          action={
            <Authorized action="permission:create" resource="permissions">
              <button onClick={() => setShowForm(true)} className="px-4 py-2 bg-primary text-primary-foreground text-sm rounded-md">
                Create permission
              </button>
            </Authorized>
          }
        />
      )}

      {!isLoading && permissions.length > 0 && roles.length > 0 && (
        <div className="border rounded-lg overflow-auto">
          <table className="w-full text-sm">
            <thead className="bg-muted/50 text-muted-foreground sticky top-0">
              <tr>
                <th className="text-left px-4 py-3 font-medium min-w-48">Permission</th>
                {activeRoles.map((role: Role) => (
                  <th key={role.id} className="px-4 py-3 font-medium text-center whitespace-nowrap">
                    {role.name}
                    {role.isSystem && <span className="ml-1 text-xs text-blue-500">(sys)</span>}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y">
              {Object.entries(permissionsByResource).map(([resource, perms]) => (
                <>
                  <tr key={`group-${resource}`} className="bg-muted/20">
                    <td colSpan={activeRoles.length + 1} className="px-4 py-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                      {resource}
                    </td>
                  </tr>
                  {perms.map((perm) => (
                    <tr key={perm.id} className="hover:bg-muted/30 transition-colors">
                      <td className="px-4 py-3">
                        <span className="font-medium">{perm.action}</span>
                        {perm.description && <p className="text-xs text-muted-foreground">{perm.description}</p>}
                      </td>
                      {activeRoles.map((role: Role) => (
                        <RolePermissionCell
                          key={role.id}
                          tenantId={tenantId!}
                          roleId={role.id}
                          permissionId={perm.id}
                        />
                      ))}
                    </tr>
                  ))}
                </>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
