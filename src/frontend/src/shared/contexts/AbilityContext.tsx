import { createContext, useCallback, useRef, useState } from 'react';
import { useAppSelector } from '@/app/hooks';
import { useCheckPermissionMutation } from '@/features/permissions/permissionEndpoints';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface AbilityContextValue {
  /**
   * Returns whether the current user can perform `action` on `resource`.
   *
   * Behaviour:
   * - Returns `true` optimistically while the check is in-flight (first call).
   * - Subsequent calls for the same key return the cached server decision.
   * - UI-level RBAC is a UX convenience only — the backend enforces all rules
   *   authoritatively. Never skip an API call because can() returns false.
   */
  can: (action: string, resource: string) => boolean;

  /**
   * Imperatively pre-fetch a permission and populate the cache.
   * Use this in page-level `useEffect` to warm the cache before rendering
   * mutation controls, so `can()` is accurate on first render.
   */
  prefetch: (action: string, resource: string) => Promise<boolean>;

  /** True while any permission check is in-flight for the first time. */
  isLoading: boolean;

  /** Clears all cached decisions — call on SignalR `rbac:invalidated` event. */
  invalidate: () => void;
}

export const AbilityContext = createContext<AbilityContextValue>({
  can: () => true,
  prefetch: async () => true,
  isLoading: false,
  invalidate: () => undefined,
});

// ── Provider ──────────────────────────────────────────────────────────────────

interface AbilityProviderProps {
  children: React.ReactNode;
}

/**
 * AbilityProvider must be mounted inside TenantLayout so it has access to the
 * authenticated user and tenant context from Redux state.
 *
 * Permission decisions are cached in a Ref (Map) for the session lifetime.
 * The cache is busted when `invalidate()` is called, which happens on
 * `rbac:invalidated` SignalR events (wired in Phase 6 — useSignalR hook).
 *
 * Key format: `"action::resource"` — double colon avoids collision with
 * action strings that may contain a single colon (e.g. `role:create`).
 */
export function AbilityProvider({ children }: AbilityProviderProps) {
  const user = useAppSelector((s) => s.auth.user);
  const tenantId = useAppSelector((s) => s.auth.tenantId);
  const [isLoading, setIsLoading] = useState(false);

  // Ref-based cache: Map<"action::resource", boolean>
  // Using Ref (not state) so cache reads don't trigger re-renders.
  const cache = useRef<Map<string, boolean>>(new Map());
  // Track in-flight checks to prevent duplicate concurrent requests.
  const inFlight = useRef<Set<string>>(new Set());

  const [checkPermission] = useCheckPermissionMutation();

  const cacheKey = (action: string, resource: string) => `${action}::${resource}`;

  const check = useCallback(
    async (action: string, resource: string): Promise<boolean> => {
      if (!user || !tenantId) return false;

      const key = cacheKey(action, resource);
      if (cache.current.has(key)) return cache.current.get(key)!;
      if (inFlight.current.has(key)) return true; // optimistic while in-flight

      inFlight.current.add(key);
      setIsLoading(true);

      try {
        const result = await checkPermission({
          tenantId,
          body: {
            userId: user.id,
            action,
            resourceId: '*',     // wildcard — UI checks type-level, not instance-level
            resourceType: resource,
            scopeId: tenantId,   // default to tenant root scope
          },
        }).unwrap();

        const granted = result.decision === 'Granted';
        cache.current.set(key, granted);
        return granted;
      } catch {
        // On check failure, fail open (true) — backend will reject unauthorized calls.
        cache.current.set(key, true);
        return true;
      } finally {
        inFlight.current.delete(key);
        if (inFlight.current.size === 0) setIsLoading(false);
      }
    },
    [user, tenantId, checkPermission]
  );

  /**
   * Synchronous cache read — returns cached result or `true` optimistically.
   * Triggers a background async check if no cache entry exists yet.
   */
  const can = useCallback(
    (action: string, resource: string): boolean => {
      const key = cacheKey(action, resource);
      if (cache.current.has(key)) return cache.current.get(key)!;
      // Fire-and-forget background check to populate cache
      void check(action, resource);
      return true; // optimistic until the check resolves
    },
    [check]
  );

  const prefetch = useCallback(
    (action: string, resource: string) => check(action, resource),
    [check]
  );

  const invalidate = useCallback(() => {
    cache.current.clear();
    inFlight.current.clear();
  }, []);

  return (
    <AbilityContext.Provider value={{ can, prefetch, isLoading, invalidate }}>
      {children}
    </AbilityContext.Provider>
  );
}
