import { create } from 'zustand';

interface DelegationStore {
  // Create delegation panel
  isCreateOpen: boolean;

  // Revoke confirmation
  revokingDelegationId: string | null;

  openCreate: () => void;
  closeCreate: () => void;
  openRevokeConfirm: (delegationId: string) => void;
  closeRevokeConfirm: () => void;
}

export const useDelegationStore = create<DelegationStore>((set) => ({
  isCreateOpen: false,
  revokingDelegationId: null,

  openCreate: () => set({ isCreateOpen: true }),
  closeCreate: () => set({ isCreateOpen: false }),
  openRevokeConfirm: (delegationId) => set({ revokingDelegationId: delegationId }),
  closeRevokeConfirm: () => set({ revokingDelegationId: null }),
}));
