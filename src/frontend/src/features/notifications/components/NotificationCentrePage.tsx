import { useParams } from 'react-router-dom';
import {
  useGetNotificationsQuery,
  useMarkNotificationReadMutation,
  useMarkAllNotificationsReadMutation,
} from '../notificationEndpoints';
import { useToastStore } from '@/shared/stores/toastStore';
import { EmptyState } from '@/shared/components/EmptyState';
import { SkeletonBlock } from '@/shared/components/Skeleton';
import type { RbacNotification, NotificationType } from '../types';

const TYPE_ICON: Record<NotificationType, string> = {
  RoleChanged: '🔑',
  PolicyChanged: '📋',
  DelegationRequest: '🔁',
  PolicyAlert: '⚠️',
};

function NotificationItem({
  notification,
  onMarkRead,
}: {
  notification: RbacNotification;
  onMarkRead: (id: string) => void;
}) {
  return (
    <div
      className={`flex items-start gap-4 px-5 py-4 transition-colors ${
        notification.isRead ? 'opacity-60' : 'bg-blue-50/50 dark:bg-blue-950/20'
      }`}
    >
      <span className="text-xl mt-0.5" aria-hidden>
        {TYPE_ICON[notification.type] ?? '🔔'}
      </span>
      <div className="flex-1 min-w-0">
        <div className="flex items-start justify-between gap-2">
          <p className={`text-sm ${notification.isRead ? '' : 'font-medium'}`}>{notification.title}</p>
          <span className="text-xs text-muted-foreground whitespace-nowrap shrink-0">
            {new Date(notification.createdAt).toLocaleString()}
          </span>
        </div>
        <p className="text-xs text-muted-foreground mt-0.5">{notification.description}</p>
      </div>
      {!notification.isRead && (
        <button
          onClick={() => onMarkRead(notification.id)}
          aria-label={`Mark notification "${notification.title}" as read`}
          className="text-xs text-primary underline shrink-0"
        >
          Mark read
        </button>
      )}
    </div>
  );
}

export default function NotificationCentrePage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const toast = useToastStore();

  const { data: notifications, isLoading, isError, refetch } = useGetNotificationsQuery(
    { tenantId: tenantId! },
    { skip: !tenantId, pollingInterval: 30000 }
  );
  const [markRead] = useMarkNotificationReadMutation();
  const [markAllRead, { isLoading: isMarkingAll }] = useMarkAllNotificationsReadMutation();

  const unreadCount = notifications?.filter((n) => !n.isRead).length ?? 0;

  const handleMarkRead = async (notificationId: string) => {
    if (!tenantId) return;
    try {
      await markRead({ tenantId, notificationId }).unwrap();
    } catch {
      toast.error('Update failed', 'Could not mark notification as read.');
    }
  };

  const handleMarkAllRead = async () => {
    if (!tenantId) return;
    try {
      await markAllRead({ tenantId }).unwrap();
    } catch {
      toast.error('Update failed', 'Could not mark all notifications as read.');
    }
  };

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Notifications</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            {unreadCount > 0
              ? `${unreadCount} unread notification${unreadCount !== 1 ? 's' : ''}`
              : 'Real-time RBAC change events'}
          </p>
        </div>
        {unreadCount > 0 && (
          <button
            onClick={() => void handleMarkAllRead()}
            disabled={isMarkingAll}
            className="px-3 py-1.5 text-sm border rounded-md hover:bg-accent transition-colors disabled:opacity-50"
          >
            {isMarkingAll ? 'Marking…' : 'Mark all read'}
          </button>
        )}
      </div>

      {isLoading && (
        <div className="border rounded-lg p-4 space-y-3">
          {Array.from({ length: 5 }).map((_, i) => (
            <SkeletonBlock key={i} className="h-16 w-full" />
          ))}
        </div>
      )}

      {isError && (
        <div className="border border-red-200 bg-red-50 text-red-700 rounded-md px-4 py-3 text-sm flex justify-between">
          <span>Failed to load notifications.</span>
          <button onClick={() => void refetch()} className="underline">Retry</button>
        </div>
      )}

      {!isLoading && !isError && notifications?.length === 0 && (
        <EmptyState
          icon="🔔"
          title="No notifications"
          description="You'll be notified here when roles, policies, or delegations change."
        />
      )}

      {notifications && notifications.length > 0 && (
        <div className="border rounded-lg overflow-hidden divide-y">
          {notifications.map((n) => (
            <NotificationItem key={n.id} notification={n} onMarkRead={(id) => void handleMarkRead(id)} />
          ))}
        </div>
      )}
    </div>
  );
}
