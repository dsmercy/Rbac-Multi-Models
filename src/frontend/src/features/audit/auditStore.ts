import { create } from 'zustand';
import type { AuditLogFilters, AccessDecision } from './types';

interface AuditLogFilterStore {
  filters: AuditLogFilters;
  expandedRowId: string | null; // row with full AccessResult JSON open

  setFilter: <K extends keyof AuditLogFilters>(key: K, value: AuditLogFilters[K]) => void;
  setDateRange: (from: string, to: string) => void;
  setResult: (result: AccessDecision | undefined) => void;
  resetFilters: () => void;
  setPage: (page: number) => void;
  expandRow: (id: string | null) => void;
}

const defaultFilters: AuditLogFilters = {
  page: 1,
  pageSize: 50,
};

export const useAuditLogFilterStore = create<AuditLogFilterStore>((set) => ({
  filters: defaultFilters,
  expandedRowId: null,

  setFilter: (key, value) =>
    set((s) => ({ filters: { ...s.filters, [key]: value, page: 1 } })),

  setDateRange: (from, to) =>
    set((s) => ({ filters: { ...s.filters, from, to, page: 1 } })),

  setResult: (result) =>
    set((s) => ({ filters: { ...s.filters, result, page: 1 } })),

  resetFilters: () => set({ filters: defaultFilters }),

  setPage: (page) =>
    set((s) => ({ filters: { ...s.filters, page } })),

  expandRow: (id) => set({ expandedRowId: id }),
}));
