import { create } from 'zustand';

interface RoleEditorStore {
  // Modal / drawer state
  isEditorOpen: boolean;
  editingRoleId: string | null; // null = create mode

  // Permission assignment state
  pendingPermissionIds: Set<string>; // permissions staged for assignment

  // Actions
  openCreate: () => void;
  openEdit: (roleId: string) => void;
  close: () => void;
  togglePermission: (permissionId: string) => void;
  setPermissions: (permissionIds: string[]) => void;
  resetPermissions: () => void;
}

export const useRoleEditorStore = create<RoleEditorStore>((set) => ({
  isEditorOpen: false,
  editingRoleId: null,
  pendingPermissionIds: new Set(),

  openCreate: () => set({ isEditorOpen: true, editingRoleId: null, pendingPermissionIds: new Set() }),
  openEdit: (roleId) => set({ isEditorOpen: true, editingRoleId: roleId }),
  close: () => set({ isEditorOpen: false, editingRoleId: null, pendingPermissionIds: new Set() }),

  togglePermission: (permissionId) =>
    set((s) => {
      const next = new Set(s.pendingPermissionIds);
      next.has(permissionId) ? next.delete(permissionId) : next.add(permissionId);
      return { pendingPermissionIds: next };
    }),

  setPermissions: (permissionIds) =>
    set({ pendingPermissionIds: new Set(permissionIds) }),

  resetPermissions: () => set({ pendingPermissionIds: new Set() }),
}));
