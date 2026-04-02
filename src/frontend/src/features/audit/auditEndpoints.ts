import { apiSlice } from '@/shared/api/apiSlice';
import type { AuditLog, AuditLogFilters } from './types';
import type { PagedResult } from '@/shared/types';

export const auditEndpoints = apiSlice.injectEndpoints({
  endpoints: (builder) => ({

    getAuditLogs: builder.query<PagedResult<AuditLog>, { tenantId: string } & AuditLogFilters>({
      query: ({ tenantId, ...filters }) => ({
        url: `/tenants/${tenantId}/audit-logs`,
        params: filters,
      }),
      providesTags: [{ type: 'AuditLog', id: 'LIST' }],
      // Audit logs are append-only — short TTL prevents stale reads
      keepUnusedDataFor: 30,
    }),

    /**
     * Export audit logs as CSV.
     * The backend serves `text/csv` when the Accept header is set.
     * The component handles the download via a Blob URL.
     */
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
