export type AccessDecision = 'Granted' | 'Denied';
export interface AuditLog { id: string; tenantId: string; actorId: string; action: string; resourceType: string | null; resourceId: string | null; scopeId: string | null; result: AccessDecision; deniedReason: string | null; metadata: Record<string, unknown> | null; correlationId: string; createdAt: string; }
export interface AuditLogFilters { from?: string; to?: string; userId?: string; action?: string; resourceId?: string; result?: AccessDecision; page: number; pageSize: number; }
