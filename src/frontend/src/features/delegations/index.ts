import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';

export const delegationRoutes: AppRoute[] = [
  { path: 'delegations', component: lazy(() => import('./components/DelegationManagerPage')) },
];

export {
  useGetDelegationsQuery,
  useGetDelegationByIdQuery,
  useCreateDelegationMutation,
  useRevokeDelegationMutation,
} from './delegationEndpoints';

export { useDelegationStore } from './delegationStore';
export type { Delegation, CreateDelegationInput, DelegationStatus } from './types';
