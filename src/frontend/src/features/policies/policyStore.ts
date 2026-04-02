import { create } from 'zustand';
import type { ConditionNode, PolicyEffect } from './types';

interface PolicyBuilderStore {
  // Draft state for the condition tree composer
  draftEffect: PolicyEffect;
  draftConditionTree: ConditionNode;

  // Test panel
  isTestPanelOpen: boolean;
  testContextJson: string; // raw JSON string typed by the user

  setDraftEffect: (effect: PolicyEffect) => void;
  setDraftConditionTree: (tree: ConditionNode) => void;
  setTestContextJson: (json: string) => void;
  toggleTestPanel: () => void;
  resetDraft: () => void;
}

const defaultTree: ConditionNode = { operator: 'And', conditions: [] };

export const usePolicyBuilderStore = create<PolicyBuilderStore>((set) => ({
  draftEffect: 'Allow',
  draftConditionTree: defaultTree,
  isTestPanelOpen: false,
  testContextJson: '{}',

  setDraftEffect: (effect) => set({ draftEffect: effect }),
  setDraftConditionTree: (tree) => set({ draftConditionTree: tree }),
  setTestContextJson: (json) => set({ testContextJson: json }),
  toggleTestPanel: () => set((s) => ({ isTestPanelOpen: !s.isTestPanelOpen })),
  resetDraft: () => set({ draftEffect: 'Allow', draftConditionTree: defaultTree, testContextJson: '{}' }),
}));
