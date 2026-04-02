// ── Pagination ────────────────────────────────────────────────────────────────

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  hasNextPage: boolean;
}

// ── Scope hierarchy ───────────────────────────────────────────────────────────

export type ScopeType = 'Organization' | 'Department' | 'Project';

export interface Scope {
  id: string;
  tenantId: string;
  name: string;
  type: ScopeType;
  parentId: string | null;
  createdAt: string;
}

export interface ScopeTreeNode extends Scope {
  children: ScopeTreeNode[];
}

// ── Permission check / evaluation ─────────────────────────────────────────────

export type AccessDecision = 'Granted' | 'Denied';

export type DeniedReason =
  | 'ExplicitGlobalDeny'
  | 'ResourceLevelDeny'
  | 'AbacPolicyDeny'
  | 'DefaultDeny'
  | 'DelegationChainDepthExceeded'
  | 'DelegationExpired'
  | 'CrossTenantRejection';

export interface CheckPermissionInput {
  userId: string;
  action: string;
  resourceId: string;
  resourceType: string;
  scopeId: string;
}

export interface EvaluatedPolicy {
  policyId: string;
  policyName: string;
  decision: AccessDecision;
}

export interface EffectiveRole {
  roleId: string;
  roleName: string;
  scopeId: string;
  inherited: boolean;
}

export interface DelegationChainEntry {
  delegationId: string;
  delegatorId: string;
  chainDepth: number;
}

export interface AccessResult {
  decision: AccessDecision;
  deniedReason: DeniedReason | null;
  evaluatedPolicies: EvaluatedPolicy[];
  effectiveRoles: EffectiveRole[];
  delegationChainUsed: DelegationChainEntry | null;
  cacheHit: boolean;
  evaluationLatencyMs: number;
}

// ── API error shape ───────────────────────────────────────────────────────────

export interface ApiError {
  status: number;
  data: {
    error: string;
    message: string;
    traceId?: string;
  };
}
