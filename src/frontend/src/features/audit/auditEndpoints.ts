import { apiSlice } from '@/shared/api/apiSlice';
import type { AuditLogPagedResponse, AuditLogFilters } from './types';

export const auditEndpoints = apiSlice.injectEndpoints({
  endpoints: (builder) => ({

    getAuditLogs: builder.query<AuditLogPagedResponse, { tenantId: string } & AuditLogFilters>({
      query: ({ tenantId, ...filters }) => ({
        url: `/tenants/${tenantId}/audit-logs`,
        params: filters,
      }),
      providesTags: [{ type: 'AuditLog', id: 'LIST' }],
      keepUnusedDataFor: 30,
    }),

    exportAuditLogs: builder.mutation<Blob, { tenantId: string } & Omit<AuditLogFilters, 'page' | 'pageSize'>>({
      query: ({ tenantId, ...filters }) => ({
        url: `/tenants/${tenantId}/audit-logs`,
        params: filters,
        headers: { Accept: 'text/csv' },
        responseHandler: (response) => response.blob(),
      }),
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetAuditLogsQuery,
  useExportAuditLogsMutation,
} = auditEndpoints;
