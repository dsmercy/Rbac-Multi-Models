import { useState, useMemo } from 'react';
import { useParams } from 'react-router-dom';
import { useGetRolesQuery, useGetRolePermissionsQuery, useAssignPermissionToRoleMutation, useRevokePermissionFromRoleMutation } from '@/features/roles/roleEndpoints';
import { useGetPermissionsQuery } from '../permissionEndpoints';
import { useToastStore } from '@/shared/stores/toastStore';
import { Authorized } from '@/shared/components/Authorized';
import { SkeletonBlock } from '@/shared/components/Skeleton';

// ── Permission checkboxes for a selected role ────────────────────────────────

function RolePermissionsPanel({ tenantId, roleId, roleName, isSystem }: {
  tenantId: string;
  roleId: string;
  roleName: string;
  isSystem: boolean;
}) {
  const toast = useToastStore();

  const { data: allPermissions = [], isLoading: permsLoading } = useGetPermissionsQuery(
    { tenantId },
    { skip: !tenantId },
  );

  const { data: rolePermissions = [], isLoading: rolePermsLoading } = useGetRolePermissionsQuery(
    { tenantId, roleId },
    { skip: !tenantId || !roleId },
  );

  const [assignPermission] = useAssignPermissionToRoleMutation();
  const [revokePermission] = useRevokePermissionFromRoleMutation();

  const assignedIds = useMemo(
    () => new Set(rolePermissions.map((p) => p.id)),
    [rolePermissions],
  );

  const permissionsByResource = useMemo(() => {
    const grouped: Record<string, typeof allPermissions> = {};
    for (const p of allPermissions) {
      (grouped[p.resourceType] ??= []).push(p);
    }
    return grouped;
  }, [allPermissions]);

  const handleToggle = async (permissionCode: string, checked: boolean) => {
    try {
      if (checked) {
        await assignPermission({ tenantId, roleId, permissionCode }).unwrap();
      } else {
        await revokePermission({ tenantId, roleId, permissionCode }).unwrap();
      }
    } catch {
      toast.error('Permission update failed', 'Could not update permission. Please try again.');
    }
  };

  const isLoading = permsLoading || rolePermsLoading;

  return (
    <div className="p-6 space-y-5 max-w-2xl">
      {/* Header */}
      <div>
        <h1 className="text-xl font-semibold">Manage Permissions</h1>
        <p className="text-sm text-muted-foreground mt-0.5">{roleName}</p>
      </div>

      {/* Permission checkboxes */}
      <div className="border rounded-lg overflow-hidden">
        <div className="px-5 py-3 bg-muted/50 border-b">
          <p className="text-sm font-medium">Permissions</p>
          <p className="text-xs text-muted-foreground mt-0.5">
            Check permissions to grant them to this role.
          </p>
        </div>

        {isLoading ? (
          <div className="p-4 space-y-2">
            {Array.from({ length: 4 }).map((_, i) => (
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
                <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-2">
                  {resource}
                </p>
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                  {perms.map((perm) => (
                    <Authorized
                      key={perm.id}
                      action="role:update"
                      resource="roles"
                      fallback={
                        <label className="flex items-center gap-2 text-sm opacity-50 cursor-not-allowed">
                          <input
                            type="checkbox"
                            checked={assignedIds.has(perm.id)}
                            disabled
                            readOnly
                            className="rounded"
                          />
                          <span>{perm.action}</span>
                        </label>
                      }
                    >
                      <label className="flex items-center gap-2 text-sm cursor-pointer hover:text-foreground">
                        <input
                          type="checkbox"
                          checked={assignedIds.has(perm.id)}
                          disabled={isSystem}
                          onChange={(e) => void handleToggle(perm.code, e.target.checked)}
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

      {isSystem && (
        <p className="text-xs text-muted-foreground">System roles are immutable — permissions cannot be changed.</p>
      )}
    </div>
  );
}

// ── Page ─────────────────────────────────────────────────────────────────────

export default function ManageRolePermissionsPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);

  const { data: roles, isLoading: rolesLoading } = useGetRolesQuery(
    { tenantId: tenantId! },
    { skip: !tenantId },
  );

  const selectedRole = roles?.find((r) => r.id === selectedRoleId);

  return (
    <div className="flex" style={{ height: 'calc(100vh - 64px)' }}>
      {/* ── Left panel: roles list ────────────────────────────────────────── */}
      <aside className="w-64 flex-shrink-0 border-r overflow-y-auto">
        <div className="px-4 py-3 border-b">
          <h2 className="text-sm font-semibold">Roles</h2>
        </div>

        {rolesLoading ? (
          <div className="p-4 space-y-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <SkeletonBlock key={i} className="h-8 w-full" />
            ))}
          </div>
        ) : (
          <ul className="py-1">
            {(roles ?? []).map((role) => (
              <li key={role.id}>
                <button
                  type="button"
                  onClick={() => setSelectedRoleId(role.id)}
                  className={`w-full text-left px-4 py-2.5 text-sm transition-colors ${
                    selectedRoleId === role.id
                      ? 'bg-primary text-primary-foreground font-medium'
                      : 'hover:bg-muted/50 text-foreground'
                  }`}
                >
                  <span className="truncate block">{role.name}</span>
                  {role.isSystem && (
                    <span
                      className={`text-xs ${
                        selectedRoleId === role.id
                          ? 'text-primary-foreground/70'
                          : 'text-muted-foreground'
                      }`}
                    >
                      system
                    </span>
                  )}
                </button>
              </li>
            ))}
          </ul>
        )}
      </aside>

      {/* ── Right panel ──────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto">
        {!selectedRoleId ? (
          <div className="flex items-center justify-center h-full text-muted-foreground text-sm">
            Select a role on the left to manage its permissions.
          </div>
        ) : (
          <RolePermissionsPanel
            key={selectedRoleId}
            tenantId={tenantId!}
            roleId={selectedRoleId}
            roleName={selectedRole?.name ?? ''}
            isSystem={selectedRole?.isSystem ?? false}
          />
        )}
      </div>
    </div>
  );
}
