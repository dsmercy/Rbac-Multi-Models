import { create } from 'zustand';

interface UserTableStore {
  selectedUserIds: Set<string>;
  searchQuery: string;
  page: number;

  // Role assignment panel
  isAssignRoleOpen: boolean;
  assigningUserId: string | null;

  setSearchQuery: (q: string) => void;
  setPage: (page: number) => void;
  toggleUserSelection: (userId: string) => void;
  clearSelection: () => void;
  openAssignRole: (userId: string) => void;
  closeAssignRole: () => void;
}

export const useUserTableStore = create<UserTableStore>((set) => ({
  selectedUserIds: new Set(),
  searchQuery: '',
  page: 1,
  isAssignRoleOpen: false,
  assigningUserId: null,

  setSearchQuery: (q) => set({ searchQuery: q, page: 1 }),
  setPage: (page) => set({ page }),

  toggleUserSelection: (userId) =>
    set((s) => {
      const next = new Set(s.selectedUserIds);
      next.has(userId) ? next.delete(userId) : next.add(userId);
      return { selectedUserIds: next };
    }),

  clearSelection: () => set({ selectedUserIds: new Set() }),

  openAssignRole: (userId) => set({ isAssignRoleOpen: true, assigningUserId: userId }),
  closeAssignRole: () => set({ isAssignRoleOpen: false, assigningUserId: null }),
}));
