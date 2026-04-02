import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';

export const policyRoutes: AppRoute[] = [
  { path: 'policies', component: lazy(() => import('./components/PolicyListPage')) },
  { path: 'policies/new', component: lazy(() => import('./components/PolicyBuilderPage')), guard: ({ can }) => can('policy:create', 'policies') },
  { path: 'policies/:policyId', component: lazy(() => import('./components/PolicyBuilderPage')), guard: ({ can }) => can('policy:update', 'policies') },
];

export {
  useGetPoliciesQuery,
  useGetPolicyByIdQuery,
  useCreatePolicyMutation,
  useUpdatePolicyMutation,
  useDeletePolicyMutation,
} from './policyEndpoints';

export { usePolicyBuilderStore } from './policyStore';
export type { Policy, CreatePolicyInput, UpdatePolicyInput, PolicyEffect, ConditionNode, ConditionLeaf } from './types';
