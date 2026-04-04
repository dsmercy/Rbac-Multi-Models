import { useState, useRef, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { useGetRolesQuery, useGetRoleMembersQuery } from '../roleEndpoints';
import { useGetUsersQuery, useAssignRoleToUserMutation, useRevokeRoleFromUserMutation } from '@/features/users/userEndpoints';
import { useToastStore } from '@/shared/stores/toastStore';
import { SkeletonBlock } from '@/shared/components/Skeleton';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import type { RoleMember } from '../types';

export default function RoleMembersPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const toast = useToastStore();

  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);
  const [emailInput, setEmailInput] = useState('');
  // pendingEmail triggers the RTK Query search; cleared once processed
  const [pendingEmail, setPendingEmail] = useState('');
  const [emailError, setEmailError] = useState<string | null>(null);
  const [checked, setChecked] = useState<Set<string>>(new Set());
  const [confirmRemove, setConfirmRemove] = useState(false);
  const emailRef = useRef<HTMLInputElement>(null);

  // ── Roles list ──────────────────────────────────────────────────────────────
  const { data: roles, isLoading: rolesLoading } = useGetRolesQuery(
    { tenantId: tenantId! },
    { skip: !tenantId },
  );

  const selectedRole = roles?.find((r) => r.id === selectedRoleId);

  // ── Members of selected role ──────────────────────────────────────────────
  const { data: members, isLoading: membersLoading } = useGetRoleMembersQuery(
    { tenantId: tenantId!, roleId: selectedRoleId! },
    { skip: !tenantId || !selectedRoleId },
  );

  // ── User lookup by email (fires only when pendingEmail is set) ────────────
  const { data: userSearchResult, isFetching: searchFetching } = useGetUsersQuery(
    { tenantId: tenantId!, search: pendingEmail, pageSize: 10 },
    { skip: !tenantId || !pendingEmail },
  );

  const [assignRole, { isLoading: isAssigning }] = useAssignRoleToUserMutation();
  const [revokeRole, { isLoading: isRevoking }] = useRevokeRoleFromUserMutation();

  // ── Process search result when it arrives ────────────────────────────────
  useEffect(() => {
    if (!pendingEmail || searchFetching || !userSearchResult || !tenantId || !selectedRoleId) return;

    const email = pendingEmail.toLowerCase();
    const found = userSearchResult.items?.find((u) => u.email.toLowerCase() === email);
    setPendingEmail(''); // clear so this effect doesn't re-run

    if (!found) {
      setEmailError(`No user found with email "${emailInput.trim()}".`);
      return;
    }

    const alreadyMember = members?.some((m) => m.userId === found.id);
    if (alreadyMember) {
      setEmailError('This user is already a member of this role.');
      return;
    }

    void (async () => {
      try {
        await assignRole({
          tenantId,
          userId: found.id,
          body: { roleId: selectedRoleId, scopeId: null },
        }).unwrap();
        toast.success('Member added', `${found.displayName || found.email} was assigned to the role.`);
        setEmailInput('');
        setEmailError(null);
      } catch {
        toast.error('Failed to assign', 'Could not assign the role. Please try again.');
      }
    })();
  }, [pendingEmail, searchFetching, userSearchResult, tenantId, selectedRoleId, members, emailInput, assignRole, toast]);

  // ── Handlers ──────────────────────────────────────────────────────────────
  const handleRoleSelect = (roleId: string) => {
    setSelectedRoleId(roleId);
    setChecked(new Set());
    setEmailInput('');
    setPendingEmail('');
    setEmailError(null);
  };

  const handleAssociate = () => {
    const email = emailInput.trim().toLowerCase();
    if (!email) {
      setEmailError('Enter an email address.');
      emailRef.current?.focus();
      return;
    }
    setEmailError(null);
    setPendingEmail(email);
  };

  const handleToggleCheck = (assignmentId: string) => {
    setChecked((prev) => {
      const next = new Set(prev);
      if (next.has(assignmentId)) next.delete(assignmentId);
      else next.add(assignmentId);
      return next;
    });
  };

  const handleToggleAll = () => {
    if (!members) return;
    setChecked(
      checked.size === members.length ? new Set() : new Set(members.map((m) => m.assignmentId)),
    );
  };

  const handleBulkRemove = async () => {
    if (!tenantId || !selectedRoleId || checked.size === 0) return;
    const toRemove = (members ?? []).filter((m) => checked.has(m.assignmentId));
    let failCount = 0;
    for (const member of toRemove) {
      try {
        await revokeRole({
          tenantId,
          userId: member.userId,
          roleId: selectedRoleId,
          scopeId: member.scopeId ?? undefined,
        }).unwrap();
      } catch {
        failCount++;
      }
    }
    if (failCount > 0) {
      toast.error('Partial failure', `${failCount} member(s) could not be removed.`);
    } else {
      toast.success('Members removed', `${toRemove.length} member(s) removed from the role.`);
    }
    setChecked(new Set());
    setConfirmRemove(false);
  };

  const isBusy = isAssigning || searchFetching;

  return (
    <div className="flex" style={{ height: 'calc(100vh - 64px)' }}>
      {/* ── Left panel: role list ─────────────────────────────────────────── */}
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
                  onClick={() => handleRoleSelect(role.id)}
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
                        selectedRoleId === role.id ? 'text-primary-foreground/70' : 'text-muted-foreground'
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
            Select a role on the left to manage its members.
          </div>
        ) : (
          <div className="p-6 space-y-5 max-w-2xl">
            {/* Header */}
            <div>
              <h1 className="text-xl font-semibold">Role Members</h1>
              <p className="text-sm text-muted-foreground mt-0.5">
                {selectedRole?.name}
              </p>
            </div>

            {/* Associate by email */}
            <div className="border rounded-lg p-4 space-y-3">
              <h2 className="text-sm font-medium">Add member by email</h2>
              <div className="flex gap-2">
                <input
                  ref={emailRef}
                  type="email"
                  value={emailInput}
                  onChange={(e) => { setEmailInput(e.target.value); setEmailError(null); }}
                  onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); handleAssociate(); } }}
                  placeholder="user@example.com"
                  className="flex-1 border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                />
                <button
                  type="button"
                  onClick={handleAssociate}
                  disabled={isBusy}
                  className="px-4 py-2 text-sm bg-primary text-primary-foreground rounded-md hover:bg-primary/90 disabled:opacity-50 whitespace-nowrap"
                >
                  {isBusy ? 'Searching…' : 'Associate'}
                </button>
              </div>
              {emailError && <p className="text-xs text-red-500">{emailError}</p>}
            </div>

            {/* Members list */}
            <div className="border rounded-lg overflow-hidden">
              <div className="px-4 py-3 bg-muted/50 border-b flex items-center justify-between">
                <span className="text-sm font-medium">
                  {membersLoading
                    ? 'Loading…'
                    : `${members?.length ?? 0} member${members?.length !== 1 ? 's' : ''}`}
                </span>
                {checked.size > 0 && (
                  <button
                    type="button"
                    onClick={() => setConfirmRemove(true)}
                    disabled={isRevoking}
                    className="text-xs px-3 py-1 border border-red-200 text-red-600 rounded hover:bg-red-50 transition-colors disabled:opacity-50"
                  >
                    Remove {checked.size} selected
                  </button>
                )}
              </div>

              {membersLoading ? (
                <div className="p-4 space-y-2">
                  {Array.from({ length: 3 }).map((_, i) => (
                    <SkeletonBlock key={i} className="h-10 w-full" />
                  ))}
                </div>
              ) : !members || members.length === 0 ? (
                <p className="text-sm text-muted-foreground text-center py-8">
                  No members assigned to this role yet.
                </p>
              ) : (
                <table className="w-full text-sm">
                  <thead className="bg-muted/30 text-muted-foreground">
                    <tr>
                      <th className="px-4 py-2 w-10">
                        <input
                          type="checkbox"
                          checked={checked.size === members.length}
                          ref={(el) => {
                            if (el) el.indeterminate = checked.size > 0 && checked.size < members.length;
                          }}
                          onChange={handleToggleAll}
                          aria-label="Select all members"
                          className="rounded"
                        />
                      </th>
                      <th className="text-left px-4 py-2 font-medium">User</th>
                      <th className="text-left px-4 py-2 font-medium">Assigned</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {members.map((member) => (
                      <MemberRow
                        key={member.assignmentId}
                        member={member}
                        checked={checked.has(member.assignmentId)}
                        onToggle={handleToggleCheck}
                      />
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        )}
      </div>

      {/* Confirm bulk remove dialog */}
      <ConfirmDialog
        open={confirmRemove}
        title="Remove members"
        description={`Remove ${checked.size} member${checked.size !== 1 ? 's' : ''} from "${selectedRole?.name}"? Their access will be revoked immediately.`}
        confirmLabel="Remove"
        destructive
        isLoading={isRevoking}
        onConfirm={() => void handleBulkRemove()}
        onCancel={() => setConfirmRemove(false)}
      />
    </div>
  );
}

function MemberRow({
  member,
  checked,
  onToggle,
}: {
  member: RoleMember;
  checked: boolean;
  onToggle: (assignmentId: string) => void;
}) {
  return (
    <tr className={`transition-colors ${checked ? 'bg-muted/30' : 'hover:bg-muted/20'}`}>
      <td className="px-4 py-2.5">
        <input
          type="checkbox"
          checked={checked}
          onChange={() => onToggle(member.assignmentId)}
          aria-label={`Select ${member.displayName || member.email}`}
          className="rounded"
        />
      </td>
      <td className="px-4 py-2.5">
        <p className="font-medium">{member.displayName || member.email}</p>
        {member.displayName && (
          <p className="text-xs text-muted-foreground">{member.email}</p>
        )}
      </td>
      <td className="px-4 py-2.5 text-muted-foreground text-xs">
        {new Date(member.assignedAt).toLocaleDateString()}
        {member.expiresAt && (
          <span className="ml-1 text-amber-600">
            · expires {new Date(member.expiresAt).toLocaleDateString()}
          </span>
        )}
      </td>
    </tr>
  );
}
