export interface Role { id: string; tenantId: string; name: string; description: string | null; isSystem: boolean; createdAt: string; }
export interface CreateRoleInput { name: string; description?: string; }
export interface UpdateRoleInput { name?: string; description?: string; }
export interface RoleEditorState { isOpen: boolean; roleId: string | null; }
