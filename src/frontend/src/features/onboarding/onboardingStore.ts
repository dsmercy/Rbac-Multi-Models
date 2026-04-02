import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface OnboardingStore {
  /** Map of userId → whether wizard has been completed/dismissed. */
  completedByUser: Record<string, boolean>;
  /** Whether the wizard overlay is currently visible. */
  isOpen: boolean;

  /** Mark wizard done for a given user (persisted to localStorage). */
  markCompleted: (userId: string) => void;
  /** Open the wizard overlay (re-launch from dashboard). */
  open: () => void;
  /** Close the wizard overlay without marking complete. */
  close: () => void;
  /** Whether this user has already completed/dismissed the wizard. */
  isCompleted: (userId: string) => boolean;
}

export const useOnboardingStore = create<OnboardingStore>()(
  persist(
    (set, get) => ({
      completedByUser: {},
      isOpen: false,

      markCompleted: (userId) =>
        set((s) => ({
          completedByUser: { ...s.completedByUser, [userId]: true },
          isOpen: false,
        })),

      open: () => set({ isOpen: true }),
      close: () => set({ isOpen: false }),

      isCompleted: (userId) => get().completedByUser[userId] === true,
    }),
    {
      name: 'rbac-onboarding',
      partialize: (s) => ({ completedByUser: s.completedByUser }),
    }
  )
);
