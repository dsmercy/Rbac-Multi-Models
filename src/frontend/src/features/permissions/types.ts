export interface Permission { id: string; tenantId: string; action: string; resourceType: string; description: string | null; createdAt: string; }
export interface CreatePermissionInput { action: string; resourceType: string; description?: string; }
