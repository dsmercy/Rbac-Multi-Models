import { useContext } from 'react';
import { AbilityContext, type AbilityContextValue } from '@/shared/contexts/AbilityContext';

/**
 * Returns the current tenant's permission-check functions.
 *
 * Must be used inside a component tree that is wrapped by AbilityProvider
 * (i.e. any route under TenantLayout).
 *
 * @example
 * const { can } = useAbility();
 * if (!can('role:create', 'roles')) return null;
 */
export function useAbility(): AbilityContextValue {
  return useContext(AbilityContext);
}
