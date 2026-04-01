import type { LazyExoticComponent, ComponentType } from 'react';

/**
 * Context passed to every route guard function before the route renders.
 * Built from the Redux auth state + AbilityContext.
 */
export interface RouteGuardContext {
  isAuthenticated: boolean;
  tenantId: string | null;
  /** Returns true if the current user has the given action on the given resource. */
  can: (action: string, resource: string) => boolean;
}

/**
 * A single route definition. Features export arrays of these — no JSX, no <Route>.
 * The router root consumes them and maps them to React Router route objects.
 */
export interface AppRoute {
  /** Relative path segment — rendered under /:tenantId/ */
  path: string;
  /** Lazy-loaded page component. Always use React.lazy() here — never eager import. */
  component: LazyExoticComponent<ComponentType>;
  /**
   * Optional guard evaluated before the component renders.
   * Return true to allow, false to redirect to the parent segment.
   * Omit for routes that only require authentication (no specific permission).
   */
  guard?: (context: RouteGuardContext) => boolean;
}
