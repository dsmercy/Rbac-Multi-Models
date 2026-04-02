import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';

export const userRoutes: AppRoute[] = [
  { path: 'users', component: lazy(() => import('./components/UserListPage')) },
  { path: 'users/:userId', component: lazy(() => import('./components/UserDetailPage')) },
];

export {
  useGetUsersQuery,
  useGetUserByIdQuery,
  useCreateUserMutation,
  useUpdateUserMutation,
  useAssignRoleToUserMutation,
  useRevokeRoleFromUserMutation,
} from './userEndpoints';

export { useUserTableStore } from './userStore';
export type { User, CreateUserInput, UpdateUserInput, AssignRoleInput, UserRoleAssignment } from './types';
