import { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  useGetRoleByIdQuery,
  useCreateRoleMutation,
  useUpdateRoleMutation,
} from '../roleEndpoints';
import { createRoleSchema, type CreateRoleSchema } from '../schemas';
import { useToastStore } from '@/shared/stores/toastStore';
import { Authorized } from '@/shared/components/Authorized';
import { SkeletonBlock } from '@/shared/components/Skeleton';

export default function RoleEditorPage() {
  const { tenantId, roleId } = useParams<{ tenantId: string; roleId: string }>();
  const navigate = useNavigate();
  const toast = useToastStore();
  // roleId is undefined on /roles/new (static route has no :roleId param)
  const isEdit = !!roleId;

  const { data: role, isLoading: roleLoading, isError: roleError, refetch: refetchRole } = useGetRoleByIdQuery(
    { tenantId: tenantId!, roleId: roleId! },
    { skip: !isEdit || !tenantId || !roleId }
  );

  const [createRole, { isLoading: isCreating }] = useCreateRoleMutation();
  const [updateRole, { isLoading: isUpdating }] = useUpdateRoleMutation();
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

  const isLoading = isEdit && roleLoading;

  if (isEdit && roleError) {
    return (
      <div className="p-6">
        <div className="border border-red-200 bg-red-50 text-red-700 rounded-md px-4 py-3 text-sm flex justify-between">
          <span>Failed to load role.</span>
          <button onClick={() => void refetchRole()} className="underline">Retry</button>
        </div>
      </div>
    );
  }

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
    </div>
  );
}
