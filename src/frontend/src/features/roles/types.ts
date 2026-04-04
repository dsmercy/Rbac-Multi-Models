export interface Role { id: string; tenantId: string; name: string; description: string | null; isSystem: boolean; createdAt: string; }
export interface CreateRoleInput { name: string; description?: string; }
export interface UpdateRoleInput { name?: string; description?: string; }
export interface RoleEditorState { isOpen: boolean; roleId: string | null; }
export interface RoleMember { assignmentId: string; userId: string; email: string; displayName: string; scopeId: string | null; assignedAt: string; expiresAt: string | null; }
