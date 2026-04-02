import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';

export const notificationRoutes: AppRoute[] = [
  { path: 'notifications', component: lazy(() => import('./components/NotificationCentrePage')) },
];

export {
  useGetNotificationsQuery,
  useMarkNotificationReadMutation,
  useMarkAllNotificationsReadMutation,
} from './notificationEndpoints';

export { useNotificationStore } from './notificationStore';
export type { RbacNotification, NotificationType } from './types';
