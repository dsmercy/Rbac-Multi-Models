export type DelegationStatus = 'Active' | 'Revoked' | 'Expired';
export interface Delegation { id: string; tenantId: string; delegatorId: string; delegateeId: string; permissionIds: string[]; scopeId: string; createdAt: string; expiresAt: string; revokedAt: string | null; chainDepth: number; status: DelegationStatus; }
export interface CreateDelegationInput { delegateeUserId: string; permissionIds: string[]; scopeId: string; expiresAt: string; }
