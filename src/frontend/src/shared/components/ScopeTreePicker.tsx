import { useAppSelector } from '@/app/hooks';
import { useGetScopesQuery } from '@/shared/api/scopeEndpoints';
import { SkeletonBlock } from './Skeleton';
import type { Scope } from '@/shared/types';

interface ScopeTreePickerProps {
  value: string | null;
  onChange: (scopeId: string) => void;
  placeholder?: string;
  disabled?: boolean;
}

function buildTree(scopes: Scope[]): Scope[] {
  return scopes.filter((s) => !s.parentId);
}

function ScopeNode({
  scope,
  allScopes,
  value,
  onChange,
  depth,
}: {
  scope: Scope;
  allScopes: Scope[];
  value: string | null;
  onChange: (id: string) => void;
  depth: number;
}) {
  const children = allScopes.filter((s) => s.parentId === scope.id);
  const selected = value === scope.id;

  return (
    <div>
      <button
        type="button"
        onClick={() => onChange(scope.id)}
        style={{ paddingLeft: `${depth * 16 + 8}px` }}
        className={`w-full text-left py-1.5 pr-3 text-sm rounded transition-colors flex items-center gap-2 ${
          selected
            ? 'bg-primary text-primary-foreground'
            : 'hover:bg-accent'
        }`}
        aria-pressed={selected}
      >
        <span className="text-xs text-muted-foreground w-20 shrink-0">{scope.type}</span>
        <span className="truncate">{scope.name}</span>
      </button>
      {children.map((child) => (
        <ScopeNode
          key={child.id}
          scope={child}
          allScopes={allScopes}
          value={value}
          onChange={onChange}
          depth={depth + 1}
        />
      ))}
    </div>
  );
}

/**
 * Hierarchical scope picker — displays the scope tree (org → dept → project)
 * and lets the user select a single scope node.
 */
export function ScopeTreePicker({ value, onChange, placeholder = 'Select scope', disabled }: ScopeTreePickerProps) {
  const tenantId = useAppSelector((s) => s.auth.tenantId);
  const { data: scopes, isLoading } = useGetScopesQuery(
    { tenantId: tenantId! },
    { skip: !tenantId }
  );

  if (isLoading) return <SkeletonBlock className="h-32 w-full" />;

  const roots = buildTree(scopes ?? []);
  const selected = scopes?.find((s) => s.id === value);

  return (
    <div className={`border rounded-md overflow-hidden ${disabled ? 'opacity-50 pointer-events-none' : ''}`}>
      <div className="px-3 py-2 border-b bg-muted/40 text-xs text-muted-foreground font-medium">
        {selected ? `${selected.type} — ${selected.name}` : placeholder}
      </div>
      <div className="max-h-48 overflow-y-auto p-1 space-y-0.5">
        {roots.length === 0 ? (
          <p className="text-sm text-muted-foreground text-center py-4">No scopes found</p>
        ) : (
          roots.map((root) => (
            <ScopeNode
              key={root.id}
              scope={root}
              allScopes={scopes ?? []}
              value={value}
              onChange={onChange}
              depth={0}
            />
          ))
        )}
      </div>
    </div>
  );
}
