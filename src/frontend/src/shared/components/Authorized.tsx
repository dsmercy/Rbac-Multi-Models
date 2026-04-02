import { cloneElement, isValidElement } from 'react';
import { useAbility } from '@/shared/hooks/useAbility';

interface AuthorizedProps {
  /** The permission action to check, e.g. `"role:create"`. */
  action: string;
  /** The resource type to check against, e.g. `"roles"`. */
  resource: string;
  /**
   * What to render when the user lacks permission.
   *
   * - `undefined` (default): render the child as disabled
   *   (sets `disabled` + `aria-disabled` on the child element).
   * - `null`: hide the child entirely (display: none equivalent).
   * - Any ReactNode: render the fallback instead.
   *
   * Use `fallback={null}` for actions the user should not know exist.
   * Use the default for actions the user should see but not be able to trigger.
   *
   * CRITICAL: UI-level RBAC is a UX convenience only. The backend enforces
   * all rules authoritatively. Never skip a backend API call because this
   * component hides or disables an element.
   */
  fallback?: React.ReactNode;
  children: React.ReactElement;
}

/**
 * RBAC gate component. Wraps any mutation-capable element and disables or
 * hides it based on the current user's permission to perform `action` on `resource`.
 *
 * @example
 * // Disabled button when user lacks role:create
 * <Authorized action="role:create" resource="roles">
 *   <Button onClick={openCreate}>New role</Button>
 * </Authorized>
 *
 * // Completely hidden when user lacks tenant:create
 * <Authorized action="tenant:create" resource="tenants" fallback={null}>
 *   <TenantCreateButton />
 * </Authorized>
 */
export function Authorized({ action, resource, fallback, children }: AuthorizedProps) {
  const { can } = useAbility();
  const allowed = can(action, resource);

  if (allowed) return <>{children}</>;

  // fallback={null} — hide entirely
  if (fallback === null) return null;

  // fallback={<SomeNode>} — render the custom fallback
  if (fallback !== undefined) return <>{fallback}</>;

  // Default: render child as disabled
  if (isValidElement(children)) {
    return cloneElement(children as React.ReactElement<Record<string, unknown>>, {
      disabled: true,
      'aria-disabled': true,
      tabIndex: -1,
      // Prevent click events from firing when aria-disabled
      onClick: (e: React.MouseEvent) => e.preventDefault(),
    });
  }

  return <>{children}</>;
}
