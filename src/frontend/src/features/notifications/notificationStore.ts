import { create } from 'zustand';

interface NotificationStore {
  isDropdownOpen: boolean;
  // Unread count — driven by RTK Query data but cached here to avoid
  // re-renders on every query refetch when the bell icon is not visible
  unreadCount: number;

  openDropdown: () => void;
  closeDropdown: () => void;
  setUnreadCount: (count: number) => void;
}

export const useNotificationStore = create<NotificationStore>((set) => ({
  isDropdownOpen: false,
  unreadCount: 0,

  openDropdown: () => set({ isDropdownOpen: true }),
  closeDropdown: () => set({ isDropdownOpen: false }),
  setUnreadCount: (count) => set({ unreadCount: count }),
}));
