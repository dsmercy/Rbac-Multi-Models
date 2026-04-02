export interface AuditLog {
  id: string;
  tenantId: string;
  actorUserId: string;
  action: string;
  resourceId: string | null;
  scopeId: string | null;
  isGranted: boolean | null;
  denialReason: string | null;
  correlationId: string;
  timestamp: string;
  logType: number;
  targetEntityType: string | null;
  targetEntityId: string | null;
  newValue: string | null;
  oldValue: string | null;
  cacheHit: boolean | null;
  evaluationLatencyMs: number | null;
}

export interface AuditLogPagedResponse {
  data: AuditLog[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AuditLogFilters { from?: string; to?: string; userId?: string; action?: string; resourceId?: string; page: number; pageSize: number; }
