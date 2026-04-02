import { useEffect, useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  useGetRoleByIdQuery,
  useCreateRoleMutation,
  useUpdateRoleMutation,
  useGetRolePermissionsQuery,
  useAssignPermissionToRoleMutation,
  useRevokePermissionFromRoleMutation,
} from '../roleEndpoints';
import { useGetPermissionsQuery } from '@/features/permissions/permissionEndpoints';
import { createRoleSchema, type CreateRoleSchema } from '../schemas';
import { useToastStore } from '@/shared/stores/toastStore';
import { Authorized } from '@/shared/components/Authorized';
import { SkeletonBlock } from '@/shared/components/Skeleton';

export default function RoleEditorPage() {
  const { tenantId, roleId } = useParams<{ tenantId: string; roleId: string }>();
  const navigate = useNavigate();
  const toast = useToastStore();
  const isEdit = roleId !== 'new';

  const { data: role, isLoading: roleLoading } = useGetRoleByIdQuery(
    { tenantId: tenantId!, roleId: roleId! },
    { skip: !isEdit || !tenantId || !roleId }
  );

  const { data: allPermissions = [], isLoading: permsLoading } = useGetPermissionsQuery(
    { tenantId: tenantId! },
    { skip: !isEdit || !tenantId }
  );

  const { data: rolePermissions = [] } = useGetRolePermissionsQuery(
    { tenantId: tenantId!, roleId: roleId! },
    { skip: !isEdit || !tenantId || !roleId }
  );

  const [createRole, { isLoading: isCreating }] = useCreateRoleMutation();
  const [updateRole, { isLoading: isUpdating }] = useUpdateRoleMutation();
  const [assignPermission] = useAssignPermissionToRoleMutation();
  const [revokePermission] = useRevokePermissionFromRoleMutation();

  const isSaving = isCreating || isUpdating;

  const { register, handleSubmit, reset, formState: { errors } } = useForm<CreateRoleSchema>({
    resolver: zodResolver(createRoleSchema),
  });

  useEffect(() => {
    if (role) reset({ name: role.name, description: role.description ?? '' });
  }, [role, reset]);

  const onSubmit = async (data: CreateRoleSchema) => {
    try {
      if (isEdit) {
        await updateRole({ tenantId: tenantId!, roleId: roleId!, body: data }).unwrap();
        toast.success('Role updated', 'Changes saved successfully.');
      } else {
        const created = await createRole({ tenantId: tenantId!, body: data }).unwrap();
        toast.success('Role created', `"${created.name}" is ready to assign permissions.`);
        navigate(`/${tenantId}/roles/${created.id}/edit`);
      }
    } catch {
      toast.error('Save failed', 'Could not save the role. Please try again.');
    }
  };

  const assignedIds = useMemo(() => new Set(rolePermissions.map((p) => p.id)), [rolePermissions]);

  const permissionsByResource = useMemo(() => {
    const grouped: Record<string, typeof allPermissions> = {};
    for (const p of allPermissions) {
      (grouped[p.resourceType] ??= []).push(p);
    }
    return grouped;
  }, [allPermissions]);

  const handlePermissionToggle = async (permissionId: string, checked: boolean) => {
    if (!isEdit || !tenantId || !roleId) return;
    try {
      if (checked) {
        await assignPermission({ tenantId, roleId, permissionId }).unwrap();
      } else {
        await revokePermission({ tenantId, roleId, permissionId }).unwrap();
      }
    } catch {
      toast.error('Permission update failed', 'Could not update permission. Please try again.');
    }
  };

  const isLoading = isEdit && roleLoading;

  return (
    <div className="p-6 max-w-3xl space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <button
          onClick={() => navigate(`/${tenantId}/roles`)}
          className="text-muted-foreground hover:text-foreground transition-colors text-sm"
        >
          ← Roles
        </button>
        <span className="text-muted-foreground">/</span>
        <h1 className="text-xl font-semibold">{isEdit ? 'Edit role' : 'New role'}</h1>
      </div>

      {isLoading ? (
        <div className="space-y-3">
          <SkeletonBlock className="h-10 w-full" />
          <SkeletonBlock className="h-20 w-full" />
        </div>
      ) : (
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="border rounded-lg p-5 space-y-4">
            <h2 className="text-sm font-medium text-muted-foreground uppercase tracking-wide">Role details</h2>

            <div className="space-y-1">
              <label className="text-sm font-medium" htmlFor="name">Name</label>
              <input
                id="name"
                {...register('name')}
                className="w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                placeholder="e.g. tenant-admin"
                disabled={role?.isSystem}
              />
              {errors.name && <p className="text-xs text-red-500">{errors.name.message}</p>}
            </div>

            <div className="space-y-1">
              <label className="text-sm font-medium" htmlFor="description">Description</label>
              <textarea
                id="description"
                {...register('description')}
                className="w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                rows={3}
                placeholder="What this role is for…"
                disabled={role?.isSystem}
              />
              {errors.description && <p className="text-xs text-red-500">{errors.description.message}</p>}
            </div>
          </div>

          <Authorized action={isEdit ? 'role:update' : 'role:create'} resource="roles">
            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => navigate(`/${tenantId}/roles`)}
                className="px-4 py-2 text-sm border rounded-md hover:bg-accent transition-colors"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isSaving || role?.isSystem}
                className="px-4 py-2 text-sm bg-primary text-primary-foreground rounded-md hover:bg-primary/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isSaving ? 'Saving…' : isEdit ? 'Save changes' : 'Create role'}
              </button>
            </div>
          </Authorized>
        </form>
      )}

      {/* Permission Matrix — edit mode only */}
      {isEdit && (
        <div className="border rounded-lg overflow-hidden">
          <div className="px-5 py-3 bg-muted/50 border-b">
            <h2 className="text-sm font-medium">Permissions</h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              Check permissions to grant them to this role.
            </p>
          </div>

          {permsLoading ? (
            <div className="p-4 space-y-2">
              {Array.from({ length: 3 }).map((_, i) => (
                <SkeletonBlock key={i} className="h-8 w-full" />
              ))}
            </div>
          ) : allPermissions.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-8">
              No permissions defined for this tenant yet.
            </p>
          ) : (
            <div className="divide-y">
              {Object.entries(permissionsByResource).map(([resource, perms]) => (
                <div key={resource} className="px-5 py-3">
                  <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-2">{resource}</p>
                  <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                    {perms.map((perm) => (
                      <Authorized key={perm.id} action="role:update" resource="roles" fallback={
                        <label className="flex items-center gap-2 text-sm opacity-50 cursor-not-allowed">
                          <input type="checkbox" checked={assignedIds.has(perm.id)} disabled className="rounded" readOnly />
                          <span>{perm.action}</span>
                        </label>
                      }>
                        <label className="flex items-center gap-2 text-sm cursor-pointer hover:text-foreground">
                          <input
                            type="checkbox"
                            checked={assignedIds.has(perm.id)}
                            disabled={role?.isSystem}
                            onChange={(e) => void handlePermissionToggle(perm.id, e.target.checked)}
                            className="rounded"
                          />
                          <span>{perm.action}</span>
                        </label>
                      </Authorized>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
