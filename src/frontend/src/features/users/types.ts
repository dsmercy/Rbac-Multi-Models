export interface User { id: string; tenantId: string; email: string; displayName: string; isActive: boolean; createdAt: string; }
export interface CreateUserInput { email: string; displayName: string; password: string; }
export interface UpdateUserInput { displayName?: string; isActive?: boolean; }
export interface AssignRoleInput { roleId: string; scopeId: string; expiresAt?: string; }
