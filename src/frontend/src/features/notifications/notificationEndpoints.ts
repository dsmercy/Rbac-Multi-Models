import { apiSlice } from '@/shared/api/apiSlice';
import type { RbacNotification } from './types';

export const notificationEndpoints = apiSlice.injectEndpoints({
  endpoints: (builder) => ({

    getNotifications: builder.query<RbacNotification[], { tenantId: string }>({
      query: ({ tenantId }) => `/tenants/${tenantId}/notifications`,
      providesTags: (result) =>
        result
          ? [...result.map(({ id }) => ({ type: 'Notification' as const, id })), { type: 'Notification' as const, id: 'LIST' }]
          : [{ type: 'Notification' as const, id: 'LIST' }],
      keepUnusedDataFor: 10,
    }),

    markNotificationRead: builder.mutation<void, { tenantId: string; notificationId: string }>({
      query: ({ tenantId, notificationId }) => ({
        url: `/tenants/${tenantId}/notifications/${notificationId}/read`,
        method: 'PATCH',
      }),
      invalidatesTags: (_r, _e, { notificationId }) => [
        { type: 'Notification', id: notificationId },
        { type: 'Notification', id: 'LIST' },
      ],
    }),

    markAllNotificationsRead: builder.mutation<void, { tenantId: string }>({
      query: ({ tenantId }) => ({
        url: `/tenants/${tenantId}/notifications/read-all`,
        method: 'PATCH',
      }),
      invalidatesTags: [{ type: 'Notification', id: 'LIST' }],
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetNotificationsQuery,
  useMarkNotificationReadMutation,
  useMarkAllNotificationsReadMutation,
} = notificationEndpoints;
