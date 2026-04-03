import { useEffect, useRef, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAppDispatch, useAppSelector } from '@/app/hooks';
import { apiSlice, type TagType } from '@/shared/api/apiSlice';
import { useSignalRStore } from '@/shared/stores/signalRStore';

// ── Types ─────────────────────────────────────────────────────────────────────

/**
 * Payload shape emitted by the backend RbacInvalidatedMessage record.
 *
 * Type values (lowercase strings from C# handlers):
 *   "role" | "permission" | "assignment" | "policy" | "delegation"
 *
 * TenantId is serialised as a lowercase UUID string by System.Text.Json.
 */
interface RbacInvalidatedPayload {
  type:        string;
  tenantId:    string;   // UUID string, e.g. "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  resourceId:  string | null;
  occurredAt:  string;
}

// ── Constants ─────────────────────────────────────────────────────────────────

const MAX_RETRIES = 5;

/**
 * Maps the backend Type string → one or more RTK Query tag types to invalidate.
 *
 * "assignment" invalidates both User (assignment list per user) and Role
 * (role list shows assignment counts). "permission" also invalidates Role
 * because the RoleEditor shows a permission matrix.
 */
const TYPE_TO_TAGS: Record<string, TagType[]> = {
  role:        ['Role'],
  permission:  ['Permission', 'Role'],
  assignment:  ['User', 'Role'],
  policy:      ['Policy'],
  delegation:  ['Delegation', 'User'],
};

// ── Hook ──────────────────────────────────────────────────────────────────────

/**
 * Manages a SignalR connection to /api/v1/hubs/rbac for the current tenant.
 *
 * Auth: the backend expects the JWT as ?access_token=<token> on the initial
 * HTTP upgrade (browsers cannot set Authorization headers on WebSocket connections).
 * The access token is read from Redux memory and passed via accessTokenFactory,
 * which is also called on each reconnect so a refreshed token is always used.
 *
 * On `rbac:invalidated`:
 *   Invalidates RTK Query LIST tags for the affected entity types, triggering
 *   automatic refetch of any subscribed queries.
 *
 * Reconnect: exponential backoff up to MAX_RETRIES (1 s → 2 s → 4 s → 8 s → 16 s),
 * then transitions to 'disconnected' and ConnectionBanner appears.
 *
 * Usage: call once inside TenantLayout — not per page.
 */
export function useSignalR(tenantId: string | undefined): void {
  const dispatch    = useAppDispatch();
  const setState    = useSignalRStore((s) => s.setState);
  const accessToken = useAppSelector((s) => s.auth.accessToken);

  // Keep a ref to the latest token so accessTokenFactory always uses it
  // even after a token refresh while the connection is open.
  const tokenRef = useRef<string | null>(accessToken);
  useEffect(() => {
    tokenRef.current = accessToken;
  }, [accessToken]);

  const handleInvalidated = useCallback(
    (payload: RbacInvalidatedPayload) => {
      if (!payload) return;
      // TenantId from C# is UUID — compare case-insensitively
      if (payload.tenantId?.toLowerCase() !== tenantId?.toLowerCase()) return;

      const affectedTypes = TYPE_TO_TAGS[payload.type] ?? [];

      // Always invalidate the LIST tag for each affected type so list queries refetch.
      const tags: { type: TagType; id: string }[] = affectedTypes.map((tag) => ({
        type: tag,
        id:   'LIST',
      }));

      // Also invalidate the specific resource tag when the backend provides a resourceId.
      // This is needed for queries that provide { type, id: specificId } (e.g. getRolePermissions
      // provides { type: 'Role', id: roleId } — without this, the permission matrix won't refetch).
      if (payload.resourceId) {
        for (const tag of affectedTypes) {
          tags.push({ type: tag, id: payload.resourceId });
        }
      }

      if (tags.length > 0) {
        dispatch(apiSlice.util.invalidateTags(tags));
      }
    },
    [dispatch, tenantId],
  );

  useEffect(() => {
    if (!tenantId) return;

    const hubUrl = `${import.meta.env.VITE_API_BASE_URL ?? '/api/v1'}/hubs/rbac`;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        // Browsers cannot set Authorization headers on WebSocket upgrades.
        // Program.cs reads the token from the ?access_token query param via
        // JwtBearerEvents.OnMessageReceived.
        accessTokenFactory: () => tokenRef.current ?? '',
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) => {
          if (ctx.previousRetryCount >= MAX_RETRIES) return null; // give up → 'disconnected'
          // 1 s, 2 s, 4 s, 8 s, 16 s
          return Math.min(1000 * Math.pow(2, ctx.previousRetryCount), 30_000);
        },
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('rbac:invalidated', handleInvalidated);
    connection.onreconnecting(() => setState('reconnecting'));
    connection.onreconnected(() => setState('connected'));
    connection.onclose(() => setState('disconnected'));

    setState('connecting');
    connection
      .start()
      .then(() => setState('connected'))
      .catch(() => setState('disconnected'));

    return () => {
      connection.off('rbac:invalidated', handleInvalidated);
      void connection.stop();
    };
  }, [tenantId, handleInvalidated, setState]);
}
