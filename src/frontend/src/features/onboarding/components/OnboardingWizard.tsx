import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useAppSelector } from '@/app/hooks';
import { useOnboardingStore } from '../onboardingStore';
import { useCompleteOnboardingMutation } from '../onboardingEndpoints';
import { useCreateRoleMutation } from '@/features/roles/roleEndpoints';
import { useGetPermissionsQuery } from '@/features/permissions/permissionEndpoints';
import { useAssignPermissionToRoleMutation } from '@/features/roles/roleEndpoints';

// ── Step schemas ──────────────────────────────────────────────────────────────

const roleSchema = z.object({
  name: z.string().min(2, 'Name must be at least 2 characters').max(255),
  description: z.string().max(500).optional(),
});
type RoleForm = z.infer<typeof roleSchema>;

// ── Step components ───────────────────────────────────────────────────────────

function StepIndicator({ current, total }: { current: number; total: number }) {
  return (
    <div className="flex items-center gap-2" role="progressbar" aria-valuenow={current} aria-valuemin={1} aria-valuemax={total} aria-label={`Step ${current} of ${total}`}>
      {Array.from({ length: total }, (_, i) => (
        <div
          key={i}
          className={`h-1.5 rounded-full transition-all ${
            i < current - 1
              ? 'w-6 bg-primary'
              : i === current - 1
              ? 'w-8 bg-primary'
              : 'w-6 bg-muted'
          }`}
        />
      ))}
      <span className="text-xs text-muted-foreground ml-1">{current}/{total}</span>
    </div>
  );
}

// Step 1 — Create your first role
function Step1CreateRole({
  tenantId,
  onCreated,
}: {
  tenantId: string;
  onCreated: (roleId: string) => void;
}) {
  const [createRole, { isLoading }] = useCreateRoleMutation();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RoleForm>({
    resolver: zodResolver(roleSchema),
    defaultValues: { name: 'Tenant Admin', description: 'Full administrative access for this tenant.' },
  });

  const onSubmit = async (data: RoleForm) => {
    try {
      const role = await createRole({ tenantId, body: data }).unwrap();
      onCreated(role.id);
    } catch {
      // error visible in form submit state
    }
  };

  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <h2 className="text-base font-semibold">Create your first role</h2>
        <p className="text-sm text-muted-foreground">
          Roles group permissions. Start with a pre-filled <strong>Tenant Admin</strong> template or
          customise the name and description.
        </p>
      </div>

      <form id="wizard-step1" onSubmit={handleSubmit(onSubmit)} className="space-y-3">
        <div className="space-y-1">
          <label htmlFor="role-name" className="text-xs font-medium">Role name</label>
          <input
            id="role-name"
            {...register('name')}
            className="w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            aria-describedby={errors.name ? 'role-name-error' : undefined}
          />
          {errors.name && (
            <p id="role-name-error" className="text-xs text-destructive" role="alert">{errors.name.message}</p>
          )}
        </div>
        <div className="space-y-1">
          <label htmlFor="role-desc" className="text-xs font-medium">Description <span className="text-muted-foreground font-normal">(optional)</span></label>
          <textarea
            id="role-desc"
            {...register('description')}
            rows={2}
            className="w-full border rounded-md px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
      </form>

      <div className="bg-muted/40 rounded-md p-3 text-xs text-muted-foreground space-y-1">
        <p className="font-medium text-foreground">What happens next?</p>
        <p>After creating the role, step 2 will let you assign permissions to it from your permission catalogue.</p>
      </div>

      <div className="flex justify-end pt-1">
        <button
          type="submit"
          form="wizard-step1"
          disabled={isLoading}
          className="px-5 py-2 bg-primary text-primary-foreground text-sm font-medium rounded-md hover:bg-primary/90 disabled:opacity-50 transition-colors"
        >
          {isLoading ? 'Creating…' : 'Create role & continue'}
        </button>
      </div>
    </div>
  );
}

// Step 2 — Assign permissions
function Step2AssignPermissions({
  tenantId,
  roleId,
  onDone,
}: {
  tenantId: string;
  roleId: string;
  onDone: () => void;
}) {
  const { data: permissions = [], isLoading } = useGetPermissionsQuery({ tenantId });
  const [assignPermission] = useAssignPermissionToRoleMutation();
  const [assigned, setAssigned] = useState<Set<string>>(new Set());
  const [saving, setSaving] = useState(false);

  const toggle = (permCode: string) => {
    const next = new Set(assigned);
    if (next.has(permCode)) {
      next.delete(permCode);
    } else {
      next.add(permCode);
    }
    setAssigned(next);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await Promise.all(
        [...assigned].map((permissionCode) =>
          assignPermission({ tenantId, roleId, permissionCode }).unwrap()
        )
      );
    } finally {
      setSaving(false);
    }
    onDone();
  };

  // Group by resourceType for readability
  const grouped = permissions.reduce<Record<string, typeof permissions>>(
    (acc, p) => {
      const key = p.resourceType ?? 'General';
      (acc[key] ??= []).push(p);
      return acc;
    },
    {}
  );

  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <h2 className="text-base font-semibold">Assign permissions</h2>
        <p className="text-sm text-muted-foreground">
          Select which permissions to grant to this role. You can change these at any time from the
          Permissions page.
        </p>
      </div>

      {isLoading && (
        <div className="space-y-2">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-8 bg-muted animate-pulse rounded" />
          ))}
        </div>
      )}

      {!isLoading && permissions.length === 0 && (
        <p className="text-sm text-muted-foreground py-4 text-center">
          No permissions in the catalogue yet. You can add them from the Permissions page later.
        </p>
      )}

      {!isLoading && permissions.length > 0 && (
        <div
          className="border rounded-md divide-y max-h-56 overflow-y-auto"
          role="group"
          aria-label="Permission list"
        >
          {Object.entries(grouped).map(([resourceType, perms]) => (
            <div key={resourceType} className="px-3 py-2">
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1.5">
                {resourceType}
              </p>
              <div className="space-y-1">
                {perms.map((p) => (
                  <label key={p.id} className="flex items-center gap-2 text-xs cursor-pointer select-none">
                    <input
                      type="checkbox"
                      checked={assigned.has(p.code)}
                      onChange={() => toggle(p.code)}
                      className="rounded"
                      aria-label={`${p.action} on ${p.resourceType}`}
                    />
                    <span className="font-medium">{p.action}</span>
                    {p.description && (
                      <span className="text-muted-foreground truncate">{p.description}</span>
                    )}
                  </label>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      <div className="flex justify-between pt-1">
        <button
          onClick={onDone}
          className="text-sm text-muted-foreground underline-offset-2 hover:underline"
        >
          Skip for now
        </button>
        <button
          onClick={handleSave}
          disabled={saving}
          className="px-5 py-2 bg-primary text-primary-foreground text-sm font-medium rounded-md hover:bg-primary/90 disabled:opacity-50 transition-colors"
        >
          {saving ? 'Saving…' : `Assign ${assigned.size > 0 ? assigned.size : ''} permission${assigned.size !== 1 ? 's' : ''} & continue`}
        </button>
      </div>
    </div>
  );
}

// Step 3 — Add a user
function Step3AddUser({
  tenantId,
  onDone,
}: {
  tenantId: string;
  onDone: () => void;
}) {
  const navigate = useNavigate();

  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <h2 className="text-base font-semibold">Add a user</h2>
        <p className="text-sm text-muted-foreground">
          Invite users to your tenant and assign them the role you just created. Users can be managed
          at any time from the Users page.
        </p>
      </div>

      <div className="bg-muted/40 rounded-md p-4 space-y-3">
        <div className="flex items-start gap-3">
          <span className="text-2xl" aria-hidden>👤</span>
          <div>
            <p className="text-sm font-medium">Go to Users page</p>
            <p className="text-xs text-muted-foreground mt-0.5">
              Create users, assign roles with optional scope and expiry, and manage access from one
              place.
            </p>
          </div>
        </div>
        <button
          onClick={() => {
            onDone();
            navigate(`/${tenantId}/users`);
          }}
          className="w-full py-2 text-sm border rounded-md hover:bg-accent transition-colors font-medium"
        >
          Open Users page
        </button>
      </div>

      <div className="flex justify-end pt-1">
        <button
          onClick={onDone}
          className="px-5 py-2 bg-primary text-primary-foreground text-sm font-medium rounded-md hover:bg-primary/90 transition-colors"
        >
          Complete setup
        </button>
      </div>
    </div>
  );
}

// ── Main wizard component ─────────────────────────────────────────────────────

export default function OnboardingWizard() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const user = useAppSelector((s) => s.auth.user);
  const { isOpen, markCompleted } = useOnboardingStore();
  const [completeOnboarding] = useCompleteOnboardingMutation();
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [createdRoleId, setCreatedRoleId] = useState<string | null>(null);

  if (!isOpen || !tenantId || !user) return null;

  const handleRoleCreated = (roleId: string) => {
    setCreatedRoleId(roleId);
    setStep(2);
  };

  const handlePermissionsDone = () => setStep(3);

  const handleComplete = () => {
    markCompleted(user.id);
    void completeOnboarding({ tenantId, userId: user.id }).catch(() => {
      // Best-effort — local state already saved
    });
  };

  const handleDismiss = () => {
    markCompleted(user.id);
  };

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black/50 z-40"
        aria-hidden="true"
        onClick={handleDismiss}
      />

      {/* Wizard panel */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Setup wizard"
        className="fixed left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 z-50 w-full max-w-lg bg-card border rounded-xl shadow-2xl"
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 pt-5 pb-4 border-b">
          <div>
            <p className="text-xs font-semibold text-primary uppercase tracking-widest">Get started</p>
            <h1 className="text-lg font-semibold mt-0.5">Set up your tenant</h1>
          </div>
          <div className="flex items-center gap-3">
            <StepIndicator current={step} total={3} />
            <button
              onClick={handleDismiss}
              className="p-1.5 rounded-md text-muted-foreground hover:bg-accent transition-colors"
              aria-label="Dismiss setup wizard"
            >
              ✕
            </button>
          </div>
        </div>

        {/* Step content */}
        <div className="px-6 py-5">
          {step === 1 && (
            <Step1CreateRole
              tenantId={tenantId}
              onCreated={handleRoleCreated}
            />
          )}
          {step === 2 && (
            <Step2AssignPermissions
              tenantId={tenantId}
              roleId={createdRoleId ?? ''}
              onDone={handlePermissionsDone}
            />
          )}
          {step === 3 && (
            <Step3AddUser
              tenantId={tenantId}
              onDone={handleComplete}
            />
          )}
        </div>

        {/* Footer hint */}
        <div className="px-6 pb-4 text-xs text-muted-foreground">
          You can relaunch this wizard anytime from the Dashboard.
        </div>
      </div>
    </>
  );
}
