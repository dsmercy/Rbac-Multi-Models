import { create } from 'zustand';

interface PermissionMatrixStore {
  // Active scope filter for the matrix view
  activeScopeId: string | null;
  // Resource type filter
  activeResourceType: string | null;
  // Column (role) pinned for detail popover
  pinnedRoleId: string | null;

  setActiveScopeId: (scopeId: string | null) => void;
  setActiveResourceType: (resourceType: string | null) => void;
  setPinnedRoleId: (roleId: string | null) => void;
  resetFilters: () => void;
}

export const usePermissionMatrixStore = create<PermissionMatrixStore>((set) => ({
  activeScopeId: null,
  activeResourceType: null,
  pinnedRoleId: null,

  setActiveScopeId: (scopeId) => set({ activeScopeId: scopeId }),
  setActiveResourceType: (resourceType) => set({ activeResourceType: resourceType }),
  setPinnedRoleId: (roleId) => set({ pinnedRoleId: roleId }),
  resetFilters: () => set({ activeScopeId: null, activeResourceType: null, pinnedRoleId: null }),
}));
