import { create } from 'zustand';

export type SignalRConnectionState =
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected';

interface SignalRStore {
  state: SignalRConnectionState;
  setState: (state: SignalRConnectionState) => void;
}

/**
 * Holds SignalR connection state for the current session.
 * Read by ConnectionBanner to show/hide the "connection lost" UI.
 * Written exclusively by useSignalR.
 */
export const useSignalRStore = create<SignalRStore>((set) => ({
  state: 'disconnected',
  setState: (state) => set({ state }),
}));
