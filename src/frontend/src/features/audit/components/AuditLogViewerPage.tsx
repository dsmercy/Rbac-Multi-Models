import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useGetAuditLogsQuery, useExportAuditLogsMutation } from '../auditEndpoints';
import { useToastStore } from '@/shared/stores/toastStore';
import { EmptyState } from '@/shared/components/EmptyState';
import { SkeletonTable } from '@/shared/components/Skeleton';
import type { AuditLog } from '../types';

function ResultBadge({ isGranted }: { isGranted: boolean | null }) {
  if (isGranted === null) return <span className="text-xs bg-muted text-muted-foreground px-2 py-0.5 rounded-full">N/A</span>;
  return isGranted
    ? <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded-full">Granted</span>
    : <span className="text-xs bg-red-100 text-red-600 px-2 py-0.5 rounded-full">Denied</span>;
}

function AuditLogRow({ log }: { log: AuditLog }) {
  const [expanded, setExpanded] = useState(false);

  return (
    <>
      <tr className="hover:bg-muted/30 transition-colors cursor-pointer" onClick={() => setExpanded((s) => !s)}>
        <td className="px-4 py-3 text-muted-foreground text-xs whitespace-nowrap">
          {new Date(log.timestamp).toLocaleString()}
        </td>
        <td className="px-4 py-3 font-mono text-xs">{log.actorUserId.slice(0, 8)}…</td>
        <td className="px-4 py-3 text-xs">{log.action}</td>
        <td className="px-4 py-3 text-xs text-muted-foreground">{log.targetEntityType ?? log.resourceId?.slice(0, 8) ?? '—'}</td>
        <td className="px-4 py-3"><ResultBadge isGranted={log.isGranted} /></td>
        <td className="px-4 py-3 text-xs text-muted-foreground max-w-xs truncate">{log.denialReason ?? '—'}</td>
        <td className="px-4 py-3 text-xs text-muted-foreground text-right">{expanded ? '▲' : '▼'}</td>
      </tr>
      {expanded && (
        <tr className="bg-muted/10">
          <td colSpan={7} className="px-4 py-3">
            <div className="text-xs space-y-1.5">
              <div className="flex gap-4">
                <span className="text-muted-foreground w-32 shrink-0">Correlation ID</span>
                <span className="font-mono">{log.correlationId}</span>
              </div>
              {log.resourceId && (
                <div className="flex gap-4">
                  <span className="text-muted-foreground w-32 shrink-0">Resource ID</span>
                  <span className="font-mono">{log.resourceId}</span>
                </div>
              )}
              {log.scopeId && (
                <div className="flex gap-4">
                  <span className="text-muted-foreground w-32 shrink-0">Scope ID</span>
                  <span className="font-mono">{log.scopeId}</span>
                </div>
              )}
              {log.targetEntityId && (
                <div className="flex gap-4">
                  <span className="text-muted-foreground w-32 shrink-0">Target entity</span>
                  <span className="font-mono">{log.targetEntityType}: {log.targetEntityId}</span>
                </div>
              )}
              {log.evaluationLatencyMs !== null && (
                <div className="flex gap-4">
                  <span className="text-muted-foreground w-32 shrink-0">Latency</span>
                  <span>{log.evaluationLatencyMs}ms {log.cacheHit ? '(cache hit)' : ''}</span>
                </div>
              )}
              {log.newValue && (
                <div>
                  <p className="text-muted-foreground mb-1">New value</p>
                  <pre className="bg-muted rounded p-2 overflow-auto max-h-32 text-xs whitespace-pre-wrap">{log.newValue}</pre>
                </div>
              )}
            </div>
          </td>
        </tr>
      )}
    </>
  );
}

export default function AuditLogViewerPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const toast = useToastStore();

  const [filters, setFilters] = useState({
    from: '',
    to: '',
    userId: '',
    action: '',
    resourceId: '',
  });
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const queryFilters = {
    ...(filters.from ? { from: filters.from } : {}),
    ...(filters.to ? { to: filters.to } : {}),
    ...(filters.userId ? { userId: filters.userId } : {}),
    ...(filters.action ? { action: filters.action } : {}),
    ...(filters.resourceId ? { resourceId: filters.resourceId } : {}),
    page,
    pageSize,
  };

  const { data, isLoading, isError, refetch } = useGetAuditLogsQuery(
    { tenantId: tenantId!, ...queryFilters },
    { skip: !tenantId }
  );
  const [exportLogs, { isLoading: isExporting }] = useExportAuditLogsMutation();

  const logs = data?.data ?? [];
  const totalPages = data?.totalPages ?? 1;

  const handleExport = async () => {
    if (!tenantId) return;
    try {
      const blob = await exportLogs({ tenantId, ...queryFilters }).unwrap();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `audit-logs-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      toast.error('Export failed', 'Could not export audit logs.');
    }
  };

  const handleFilterChange = (key: keyof typeof filters, value: string) => {
    setFilters((f) => ({ ...f, [key]: value }));
    setPage(1);
  };

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Audit logs</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            {data ? `${data.total.toLocaleString()} immutable records` : 'Immutable access decision trail'}
          </p>
        </div>
        <button
          onClick={() => void handleExport()}
          disabled={isExporting || logs.length === 0}
          className="px-4 py-2 border text-sm font-medium rounded-md hover:bg-accent transition-colors disabled:opacity-50"
        >
          {isExporting ? 'Exporting…' : 'Export CSV'}
        </button>
      </div>

      {/* Filters */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
        <input
          type="datetime-local"
          value={filters.from}
          onChange={(e) => handleFilterChange('from', e.target.value)}
          placeholder="From"
          className="border rounded-md px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-ring"
        />
        <input
          type="datetime-local"
          value={filters.to}
          onChange={(e) => handleFilterChange('to', e.target.value)}
          placeholder="To"
          className="border rounded-md px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-ring"
        />
        <input
          value={filters.userId}
          onChange={(e) => handleFilterChange('userId', e.target.value)}
          placeholder="Actor user ID"
          className="border rounded-md px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-ring"
        />
        <input
          value={filters.action}
          onChange={(e) => handleFilterChange('action', e.target.value)}
          placeholder="Action (e.g. roles:create)"
          className="border rounded-md px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-ring"
        />
        <input
          value={filters.resourceId}
          onChange={(e) => handleFilterChange('resourceId', e.target.value)}
          placeholder="Resource ID"
          className="border rounded-md px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-ring"
        />
      </div>

      {isLoading && <SkeletonTable rows={8} cols={7} />}

      {isError && (
        <div className="border border-red-200 bg-red-50 text-red-700 rounded-md px-4 py-3 text-sm flex justify-between">
          <span>Failed to load audit logs.</span>
          <button onClick={() => void refetch()} className="underline">Retry</button>
        </div>
      )}

      {!isLoading && !isError && logs.length === 0 && (
        <EmptyState
          icon="📝"
          title="No audit logs"
          description="Audit records will appear here as permissions are evaluated."
        />
      )}

      {logs.length > 0 && (
        <>
          <div className="border rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50 text-muted-foreground">
                <tr>
                  <th className="text-left px-4 py-3 font-medium text-xs">Timestamp</th>
                  <th className="text-left px-4 py-3 font-medium text-xs">Actor</th>
                  <th className="text-left px-4 py-3 font-medium text-xs">Action</th>
                  <th className="text-left px-4 py-3 font-medium text-xs">Target</th>
                  <th className="text-left px-4 py-3 font-medium text-xs">Result</th>
                  <th className="text-left px-4 py-3 font-medium text-xs">Denied reason</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y">
                {logs.map((log) => <AuditLogRow key={log.id} log={log} />)}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">Page {page} of {totalPages} ({data?.total.toLocaleString()} total)</span>
              <div className="flex gap-2">
                <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page === 1} className="px-3 py-1.5 border rounded-md hover:bg-accent disabled:opacity-40">Previous</button>
                <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page === totalPages} className="px-3 py-1.5 border rounded-md hover:bg-accent disabled:opacity-40">Next</button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
