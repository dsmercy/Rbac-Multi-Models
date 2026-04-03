import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useGetPolicyByIdQuery, useCreatePolicyMutation, useUpdatePolicyMutation } from '../policyEndpoints';
import { useToastStore } from '@/shared/stores/toastStore';
import { SkeletonBlock } from '@/shared/components/Skeleton';
import type { ConditionNode, ConditionLeaf, ConditionOperator } from '../types';

// Form schema excludes conditionTree — managed in component state and merged on submit
const policyFormSchema = z.object({
  name: z.string().min(2, 'Name must be at least 2 characters').max(255),
  description: z.string().max(1000).optional(),
  effect: z.enum(['Allow', 'Deny']),
});
type PolicyFormSchema = z.infer<typeof policyFormSchema>;

const DEFAULT_CONDITION_TREE: ConditionNode = {
  operator: 'And',
  conditions: [],
};

function ConditionLeafEditor({
  leaf,
  index,
  onChange,
  onRemove,
}: {
  leaf: ConditionLeaf;
  index: number;
  onChange: (index: number, field: keyof ConditionLeaf, value: string) => void;
  onRemove: (index: number) => void;
}) {
  return (
    <div className="flex items-center gap-2 p-3 bg-muted/30 rounded-md">
      <input
        value={leaf.attribute}
        onChange={(e) => onChange(index, 'attribute', e.target.value)}
        placeholder="attribute (e.g. user.department)"
        aria-label={`Condition ${index + 1} attribute`}
        className="flex-1 border rounded px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
      />
      <select
        value={leaf.operator}
        onChange={(e) => onChange(index, 'operator', e.target.value)}
        aria-label={`Condition ${index + 1} operator`}
        className="border rounded px-2 py-1 text-xs bg-background focus:outline-none focus:ring-1 focus:ring-ring"
      >
        <option value="Equals">Equals</option>
        <option value="NotEquals">NotEquals</option>
        <option value="Contains">Contains</option>
        <option value="StartsWith">StartsWith</option>
        <option value="GreaterThan">GreaterThan</option>
        <option value="LessThan">LessThan</option>
        <option value="In">In</option>
      </select>
      <input
        value={String(leaf.value)}
        onChange={(e) => onChange(index, 'value', e.target.value)}
        placeholder="value"
        aria-label={`Condition ${index + 1} value`}
        className="flex-1 border rounded px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
      />
      <button
        type="button"
        onClick={() => onRemove(index)}
        className="text-red-500 hover:text-red-700 text-sm px-1"
        aria-label="Remove condition"
      >
        <span aria-hidden>×</span>
      </button>
    </div>
  );
}

export default function PolicyBuilderPage() {
  const { tenantId, policyId } = useParams<{ tenantId: string; policyId: string }>();
  const navigate = useNavigate();
  const toast = useToastStore();
  // policyId is undefined on /policies/new (static route has no :policyId param)
  const isEdit = !!policyId;
  const [conditionTree, setConditionTree] = useState<ConditionNode>(DEFAULT_CONDITION_TREE);
  const [jsonError, setJsonError] = useState<string | null>(null);
  const [showJson, setShowJson] = useState(false);

  const { data: policy, isLoading, isError, refetch } = useGetPolicyByIdQuery(
    { tenantId: tenantId!, policyId: policyId! },
    { skip: !isEdit || !tenantId || !policyId }
  );

  const [createPolicy, { isLoading: isCreating }] = useCreatePolicyMutation();
  const [updatePolicy, { isLoading: isUpdating }] = useUpdatePolicyMutation();
  const isSaving = isCreating || isUpdating;

  const { register, handleSubmit, reset, formState: { errors } } = useForm<PolicyFormSchema>({
    resolver: zodResolver(policyFormSchema),
    defaultValues: { effect: 'Allow' },
  });

  useEffect(() => {
    if (policy) {
      reset({ name: policy.name, description: policy.description ?? '', effect: policy.effect });
      setConditionTree(policy.conditionTree);
    }
  }, [policy, reset]);

  const handleConditionChange = (index: number, field: keyof ConditionLeaf, value: string) => {
    setConditionTree((prev) => {
      const updated = [...prev.conditions];
      updated[index] = { ...updated[index], [field]: value };
      return { ...prev, conditions: updated };
    });
  };

  const handleAddCondition = () => {
    setConditionTree((prev) => ({
      ...prev,
      conditions: [...prev.conditions, { attribute: '', operator: 'Equals', value: '' }],
    }));
  };

  const handleRemoveCondition = (index: number) => {
    setConditionTree((prev) => ({
      ...prev,
      conditions: prev.conditions.filter((_, i) => i !== index),
    }));
  };

  const handleOperatorChange = (op: ConditionOperator) => {
    setConditionTree((prev) => ({ ...prev, operator: op }));
  };

  const onSubmit = async (data: PolicyFormSchema) => {
    setJsonError(null);
    const payload = { ...data, conditionTree };

    try {
      if (isEdit) {
        await updatePolicy({ tenantId: tenantId!, policyId: policyId!, body: payload }).unwrap();
        toast.success('Policy updated', 'Changes saved successfully.');
      } else {
        const created = await createPolicy({ tenantId: tenantId!, body: payload }).unwrap();
        toast.success('Policy created', `"${created.name}" is now active.`);
        navigate(`/${tenantId}/policies/${created.id}/edit`);
      }
    } catch {
      toast.error('Save failed', 'Could not save the policy. Please try again.');
    }
  };

  if (isLoading) {
    return (
      <div className="p-6 space-y-3">
        <SkeletonBlock className="h-6 w-48" />
        <SkeletonBlock className="h-32 w-full" />
      </div>
    );
  }

  if (isEdit && isError) {
    return (
      <div className="p-6">
        <div className="border border-red-200 bg-red-50 text-red-700 rounded-md px-4 py-3 text-sm flex justify-between">
          <span>Failed to load policy.</span>
          <button onClick={() => void refetch()} className="underline">Retry</button>
        </div>
      </div>
    );
  }

  return (
    <div className="p-6 max-w-3xl space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(`/${tenantId}/policies`)} className="text-muted-foreground hover:text-foreground transition-colors text-sm">
          ← Policies
        </button>
        <span className="text-muted-foreground">/</span>
        <h1 className="text-xl font-semibold">{isEdit ? 'Edit policy' : 'New policy'}</h1>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
        {/* Basic info */}
        <div className="border rounded-lg p-5 space-y-4">
          <h2 className="text-sm font-medium text-muted-foreground uppercase tracking-wide">Policy details</h2>

          <div className="space-y-1">
            <label className="text-sm font-medium">Name</label>
            <input {...register('name')} placeholder="e.g. Deny access outside business hours" className="w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
            {errors.name && <p className="text-xs text-red-500">{errors.name.message}</p>}
          </div>

          <div className="space-y-1">
            <label className="text-sm font-medium">Description</label>
            <textarea {...register('description')} rows={2} placeholder="What this policy enforces…" className="w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring resize-none" />
          </div>

          <div className="space-y-1">
            <label className="text-sm font-medium">Effect</label>
            <select {...register('effect')} className="w-full border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring">
              <option value="Allow">Allow</option>
              <option value="Deny">Deny</option>
            </select>
          </div>
        </div>

        {/* Condition tree builder */}
        <div className="border rounded-lg overflow-hidden">
          <div className="px-5 py-3 bg-muted/50 border-b flex items-center justify-between">
            <div>
              <h2 className="text-sm font-medium">Condition tree</h2>
              <p className="text-xs text-muted-foreground">All conditions must match (AND) or any one (OR).</p>
            </div>
            <button type="button" onClick={() => setShowJson((s) => !s)} className="text-xs text-muted-foreground underline">
              {showJson ? 'Visual editor' : 'View JSON'}
            </button>
          </div>

          {showJson ? (
            <div className="p-4">
              <pre className="text-xs bg-muted rounded-md p-3 overflow-auto max-h-64 whitespace-pre-wrap">
                {JSON.stringify(conditionTree, null, 2)}
              </pre>
            </div>
          ) : (
            <div className="p-4 space-y-3">
              <div className="flex items-center gap-2">
                <span className="text-xs text-muted-foreground">Match</span>
                {(['And', 'Or', 'Not'] as ConditionOperator[]).map((op) => (
                  <button
                    key={op}
                    type="button"
                    onClick={() => handleOperatorChange(op)}
                    aria-pressed={conditionTree.operator === op}
                    className={`text-xs px-2 py-1 rounded border transition-colors ${
                      conditionTree.operator === op
                        ? 'bg-primary text-primary-foreground border-primary'
                        : 'hover:bg-accent border-input'
                    }`}
                  >
                    {op}
                  </button>
                ))}
                <span className="text-xs text-muted-foreground">conditions</span>
              </div>

              {conditionTree.conditions.length === 0 ? (
                <p className="text-sm text-muted-foreground text-center py-4">No conditions. Add one below.</p>
              ) : (
                <div className="space-y-2">
                  {conditionTree.conditions.map((leaf, i) => (
                    <ConditionLeafEditor
                      key={i}
                      leaf={leaf}
                      index={i}
                      onChange={handleConditionChange}
                      onRemove={handleRemoveCondition}
                    />
                  ))}
                </div>
              )}

              <button
                type="button"
                onClick={handleAddCondition}
                className="text-xs px-3 py-1.5 border border-dashed rounded-md hover:bg-accent transition-colors w-full"
              >
                + Add condition
              </button>
            </div>
          )}
        </div>

        {jsonError && <p className="text-xs text-red-500">{jsonError}</p>}

        <div className="flex justify-end gap-2">
          <button type="button" onClick={() => navigate(`/${tenantId}/policies`)} className="px-4 py-2 text-sm border rounded-md hover:bg-accent">Cancel</button>
          <button type="submit" disabled={isSaving} className="px-4 py-2 text-sm bg-primary text-primary-foreground rounded-md hover:bg-primary/90 disabled:opacity-50">
            {isSaving ? 'Saving…' : isEdit ? 'Save changes' : 'Create policy'}
          </button>
        </div>
      </form>
    </div>
  );
}
