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
  resourceId?: string | null;
  resourceType?: string;
  scopeId: string;
}

export interface AccessResult {
  isGranted: boolean;
  denialReason: DeniedReason | null;
  cacheHit: boolean;
  evaluationLatencyMs: number;
  delegationChain: string | null;
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
