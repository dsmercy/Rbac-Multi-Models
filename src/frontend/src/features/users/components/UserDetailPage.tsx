import { useState, useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  useGetUserByIdQuery,
  useGetUserRoleAssignmentsQuery,
  useUpdateUserMutation,
  useAssignRoleToUserMutation,
  useRevokeRoleFromUserMutation,
} from '../userEndpoints';
import { useGetScopesQuery } from '@/shared/api/scopeEndpoints';
import { useToastStore } from '@/shared/stores/toastStore';
import { Authorized } from '@/shared/components/Authorized';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { SkeletonBlock } from '@/shared/components/Skeleton';
import type { Scope } from '@/shared/types';
import type { UserRoleAssignment } from '../types';

// ── Collapsible scope tree with checkboxes ────────────────────────────────────

function ScopeCheckboxNode({
  scope,
  allScopes,
  selected,
  assigned,
  expanded,
  onToggle,
  onToggleExpand,
  depth,
}: {
  scope: Scope;
  allScopes: Scope[];
  selected: Set<string>;
  assigned: Set<string>;
  expanded: Set<string>;
  onToggle: (id: string, checked: boolean) => void;
  onToggleExpand: (id: string) => void;
  depth: number;
}) {
  const children = allScopes.filter((s) => s.parentId === scope.id);
  const isExpanded = expanded.has(scope.id);
  const isAssigned = assigned.has(scope.id);
  const isSelected = selected.has(scope.id);

  return (
    <div>
      <div
        className="flex items-center gap-1.5 py-1.5 pr-3 hover:bg-muted/30 rounded transition-colors"
        style={{ paddingLeft: `${depth * 18 + 8}px` }}
      >
        {/* Expand/collapse toggle */}
        <button
          type="button"
          onClick={() => onToggleExpand(scope.id)}
          className={`w-4 h-4 flex items-center justify-center text-muted-foreground shrink-0 ${
            children.length === 0 ? 'invisible' : ''
          }`}
          aria-label={isExpanded ? 'Collapse' : 'Expand'}
        >
          {isExpanded ? '▾' : '▸'}
        </button>

        {/* Checkbox */}
        <input
          type="checkbox"
          checked={isAssigned || isSelected}
          disabled={isAssigned}
          onChange={(e) => onToggle(scope.id, e.target.checked)}
          className="rounded shrink-0"
          aria-label={`${scope.name} scope`}
        />

        {/* Label */}
        <span className="text-xs text-muted-foreground w-24 shrink-0">{scope.type}</span>
        <span className="text-sm truncate">{scope.name}</span>

        {isAssigned && (
          <span className="ml-auto text-xs bg-blue-50 text-blue-700 px-1.5 py-0.5 rounded-full shrink-0">
            Assigned
          </span>
        )}
      </div>

      {isExpanded &&
        children.map((child) => (
          <ScopeCheckboxNode
            key={child.id}
            scope={child}
            allScopes={allScopes}
            selected={selected}
            assigned={assigned}
            expanded={expanded}
            onToggle={onToggle}
            onToggleExpand={onToggleExpand}
            depth={depth + 1}
          />
        ))}
    </div>
  );
}

function CollapsibleScopeTree({
  scopes,
  selected,
  assigned,
  onToggle,
}: {
  scopes: Scope[];
  selected: Set<string>;
  assigned: Set<string>;
  onToggle: (id: string, checked: boolean) => void;
}) {
  // Start with all non-leaf nodes expanded
  const [expanded, setExpanded] = useState<Set<string>>(() => {
    const parents = new Set(scopes.filter((s) => s.parentId).map((s) => s.parentId!));
    return new Set(scopes.filter((s) => parents.has(s.id) || !s.parentId).map((s) => s.id));
  });

  const handleToggleExpand = (id: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const roots = scopes.filter((s) => !s.parentId);

  if (roots.length === 0) {
    return <p className="text-sm text-muted-foreground text-center py-6">No scopes found.</p>;
  }

  return (
    <div className="space-y-0.5">
      {roots.map((root) => (
        <ScopeCheckboxNode
          key={root.id}
          scope={root}
          allScopes={scopes}
          selected={selected}
          assigned={assigned}
          expanded={expanded}
          onToggle={onToggle}
          onToggleExpand={handleToggleExpand}
          depth={0}
        />
      ))}
    </div>
  );
}

// ── Right panel — scope assignment for a selected role ────────────────────────

function ScopeAssignmentPanel({
  tenantId,
  userId,
  roleAssignments,
  selectedRoleId,
  roleName,
  scopes,
}: {
  tenantId: string;
  userId: string;
  roleAssignments: UserRoleAssignment[];
  selectedRoleId: string;
  roleName: string;
  scopes: Scope[];
}) {
  const toast = useToastStore();
  const [selectedScopeIds, setSelectedScopeIds] = useState<Set<string>>(new Set());
  const [expiresAt, setExpiresAt] = useState('');
  const [revokingAssignment, setRevokingAssignment] = useState<UserRoleAssignment | null>(null);

  const [assignRole, { isLoading: isAssigning }] = useAssignRoleToUserMutation();
  const [revokeRole, { isLoading: isRevoking }] = useRevokeRoleFromUserMutation();

  // Scopes where this role is already assigned to the user
  const assignedScopeIds = useMemo(
    () =>
      new Set(
        roleAssignments
          .filter((a) => a.roleId === selectedRoleId && a.isActive && a.scopeId)
          .map((a) => a.scopeId as string),
      ),
    [roleAssignments, selectedRoleId],
  );

  // Tenant-wide assignment (null scopeId) for this role
  const tenantWideAssignment = roleAssignments.find(
    (a) => a.roleId === selectedRoleId && a.isActive && !a.scopeId,
  );

  const handleToggle = (scopeId: string, checked: boolean) => {
    if (assignedScopeIds.has(scopeId)) return; // already assigned — ignore
    setSelectedScopeIds((prev) => {
      const next = new Set(prev);
      if (checked) next.add(scopeId);
      else next.delete(scopeId);
      return next;
    });
  };

  const handleAssign = async () => {
    const toAssign = [...selectedScopeIds].filter((id) => !assignedScopeIds.has(id));
    if (toAssign.length === 0) {
      toast.error('No scope selected', 'Check at least one scope to assign.');
      return;
    }
    let failCount = 0;
    for (const scopeId of toAssign) {
      try {
        await assignRole({
          tenantId,
          userId,
          body: {
            roleId: selectedRoleId,
            scopeId,
            expiresAt: expiresAt ? new Date(expiresAt).toISOString() : undefined,
          },
        }).unwrap();
      } catch {
        failCount++;
      }
    }
    if (failCount > 0) {
      toast.error('Partial failure', `${failCount} scope(s) could not be assigned.`);
    } else {
      toast.success('Role assigned', `Assigned at ${toAssign.length} scope(s).`);
    }
    setSelectedScopeIds(new Set());
    setExpiresAt('');
  };

  const handleRevoke = async () => {
    if (!revokingAssignment) return;
    try {
      await revokeRole({
        tenantId,
        userId,
        roleId: revokingAssignment.roleId,
        scopeId: revokingAssignment.scopeId ?? undefined,
      }).unwrap();
      toast.success('Revoked', 'Role assignment removed.');
    } catch {
      toast.error('Revoke failed', 'Could not revoke. Please try again.');
    } finally {
      setRevokingAssignment(null);
    }
  };

  const hasNewSelections = selectedScopeIds.size > 0;
  // All assignments for this role (for the "current" list)
  const currentAssignments = roleAssignments.filter(
    (a) => a.roleId === selectedRoleId && a.isActive,
  );

  return (
    <div className="p-5 space-y-5 max-w-xl">
      {/* Header */}
      <div>
        <h2 className="text-base font-semibold">{roleName}</h2>
        <p className="text-xs text-muted-foreground mt-0.5">
          Select scopes to assign this role, then click Assign.
        </p>
      </div>

      {/* Current assignments for this role */}
      {currentAssignments.length > 0 && (
        <div className="space-y-1">
          <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
            Currently assigned at
          </p>
          <div className="border rounded-md divide-y">
            {tenantWideAssignment && (
              <div className="flex items-center justify-between px-3 py-2 text-sm">
                <span className="text-muted-foreground">Tenant-wide</span>
                <Authorized action="user:update" resource="users" fallback={null}>
                  <button
                    type="button"
                    onClick={() => setRevokingAssignment(tenantWideAssignment)}
                    className="text-xs text-red-600 hover:underline"
                  >
                    Revoke
                  </button>
                </Authorized>
              </div>
            )}
            {currentAssignments
              .filter((a) => a.scopeId)
              .map((a) => {
                const scope = scopes.find((s) => s.id === a.scopeId);
                return (
                  <div key={a.id} className="flex items-center justify-between px-3 py-2 text-sm">
                    <div>
                      <span className="font-medium">{scope?.name ?? a.scopeId?.slice(0, 8) + '…'}</span>
                      {scope && (
                        <span className="ml-2 text-xs text-muted-foreground">{scope.type}</span>
                      )}
                      {a.expiresAt && (
                        <span className="ml-2 text-xs text-amber-600">
                          expires {new Date(a.expiresAt).toLocaleDateString()}
                        </span>
                      )}
                    </div>
                    <Authorized action="user:update" resource="users" fallback={null}>
                      <button
                        type="button"
                        onClick={() => setRevokingAssignment(a)}
                        className="text-xs text-red-600 hover:underline"
                      >
                        Revoke
                      </button>
                    </Authorized>
                  </div>
                );
              })}
          </div>
        </div>
      )}

      {/* Scope tree */}
      <div className="space-y-1">
        <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
          Add scope assignment
        </p>
        <div className="border rounded-md p-2 max-h-64 overflow-y-auto">
          <CollapsibleScopeTree
            scopes={scopes}
            selected={selectedScopeIds}
            assigned={assignedScopeIds}
            onToggle={handleToggle}
          />
        </div>
      </div>

      {/* Expires at */}
      <div className="space-y-1">
        <label className="text-xs font-medium text-muted-foreground">Expires at (optional)</label>
        <input
          type="datetime-local"
          value={expiresAt}
          onChange={(e) => setExpiresAt(e.target.value)}
          className="w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        />
      </div>

      {/* Actions */}
      <div className="flex justify-end gap-2">
        <button
          type="button"
          onClick={() => { setSelectedScopeIds(new Set()); setExpiresAt(''); }}
          className="px-3 py-1.5 text-sm border rounded-md hover:bg-accent transition-colors"
        >
          Cancel
        </button>
        <button
          type="button"
          onClick={() => void handleAssign()}
          disabled={isAssigning || !hasNewSelections}
          className="px-3 py-1.5 text-sm bg-primary text-primary-foreground rounded-md hover:bg-primary/90 disabled:opacity-50 transition-colors"
        >
          {isAssigning ? 'Assigning…' : `Assign${hasNewSelections ? ` (${selectedScopeIds.size})` : ''}`}
        </button>
      </div>

      {/* Revoke confirm */}
      <ConfirmDialog
        open={!!revokingAssignment}
        title="Revoke role assignment"
        description="This will deactivate the assignment. The user will lose access associated with this role at this scope."
        confirmLabel="Revoke"
        destructive
        isLoading={isRevoking}
        onConfirm={() => void handleRevoke()}
        onCancel={() => setRevokingAssignment(null)}
      />
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function UserDetailPage() {
  const { tenantId, userId } = useParams<{ tenantId: string; userId: string }>();
  const navigate = useNavigate();
  const toast = useToastStore();
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);

  const { data: user, isLoading, isError, refetch } = useGetUserByIdQuery(
    { tenantId: tenantId!, userId: userId! },
    { skip: !tenantId || !userId || userId === 'new' },
  );
  const { data: scopes = [] } = useGetScopesQuery(
    { tenantId: tenantId! },
    { skip: !tenantId },
  );
  const { data: roleAssignments = [] } = useGetUserRoleAssignmentsQuery(
    { tenantId: tenantId!, userId: userId! },
    { skip: !tenantId || !userId || userId === 'new' },
  );
  const [updateUser, { isLoading: isUpdating }] = useUpdateUserMutation();

  const handleToggleActive = async () => {
    if (!user || !tenantId || !userId) return;
    try {
      await updateUser({ tenantId, userId, body: { isActive: !user.isActive } }).unwrap();
      toast.success(user.isActive ? 'User deactivated' : 'User activated');
    } catch {
      toast.error('Update failed', 'Could not update user status.');
    }
  };

  // Deduplicated list of roles assigned to the user (active only)
  const assignedRoles = useMemo(() => {
    const seen = new Set<string>();
    const result: { roleId: string; roleName: string; scopeCount: number }[] = [];
    for (const a of roleAssignments.filter((x) => x.isActive)) {
      if (!seen.has(a.roleId)) {
        seen.add(a.roleId);
        const count = roleAssignments.filter((x) => x.roleId === a.roleId && x.isActive).length;
        result.push({ roleId: a.roleId, roleName: a.roleName || a.roleId, scopeCount: count });
      }
    }
    return result;
  }, [roleAssignments]);

  const selectedRole = assignedRoles.find((r) => r.roleId === selectedRoleId);

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
    <div className="p-6 space-y-6 max-w-5xl">
      {/* Header */}
      <div className="flex items-center gap-3">
        <button
          onClick={() => navigate(`/${tenantId}/users`)}
          className="text-muted-foreground hover:text-foreground transition-colors text-sm"
        >
          ← Users
        </button>
        <span className="text-muted-foreground">/</span>
        <h1 className="text-xl font-semibold">{user.displayName || user.email}</h1>
        {user.isActive
          ? <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded-full">Active</span>
          : <span className="text-xs bg-red-100 text-red-600 px-2 py-0.5 rounded-full">Inactive</span>}
      </div>

      {/* ── Profile card (untouched) ─────────────────────────────────────── */}
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
        <Authorized action="user:update" resource="users" fallback={null}>
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

      {/* ── Two-panel: roles + scopes ────────────────────────────────────── */}
      <div className="border rounded-lg overflow-hidden flex h-[520px]">
        {/* Left panel: assigned roles */}
        <aside className="w-56 flex-shrink-0 border-r flex flex-col">
          <div className="px-4 py-3 border-b bg-muted/50">
            <p className="text-sm font-medium">Assigned Roles</p>
            <p className="text-xs text-muted-foreground mt-0.5">
              {assignedRoles.length} role{assignedRoles.length !== 1 ? 's' : ''}
            </p>
          </div>

          <ul className="flex-1 overflow-y-auto py-1">
            {assignedRoles.length === 0 ? (
              <li className="px-4 py-8 text-xs text-muted-foreground text-center">
                No roles assigned.
              </li>
            ) : (
              assignedRoles.map((r) => (
                <li key={r.roleId}>
                  <button
                    type="button"
                    onClick={() => setSelectedRoleId(r.roleId)}
                    className={`w-full text-left px-4 py-2.5 text-sm transition-colors ${
                      selectedRoleId === r.roleId
                        ? 'bg-primary text-primary-foreground font-medium'
                        : 'hover:bg-muted/50 text-foreground'
                    }`}
                  >
                    <span className="truncate block">{r.roleName}</span>
                    <span
                      className={`text-xs ${
                        selectedRoleId === r.roleId
                          ? 'text-primary-foreground/70'
                          : 'text-muted-foreground'
                      }`}
                    >
                      {r.scopeCount} scope{r.scopeCount !== 1 ? 's' : ''}
                    </span>
                  </button>
                </li>
              ))
            )}
          </ul>
        </aside>

        {/* Right panel */}
        <div className="flex-1 overflow-y-auto">
          {!selectedRoleId ? (
            <div className="flex items-center justify-center h-full text-muted-foreground text-sm">
              Select a role on the left to manage its scope assignments.
            </div>
          ) : (
            <ScopeAssignmentPanel
              key={selectedRoleId}
              tenantId={tenantId!}
              userId={userId!}
              roleAssignments={roleAssignments}
              selectedRoleId={selectedRoleId}
              roleName={selectedRole?.roleName ?? selectedRoleId}
              scopes={scopes}
            />
          )}
        </div>
      </div>
    </div>
  );
}
