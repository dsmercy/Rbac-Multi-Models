namespace PermissionEngine.Domain.Models;

public enum DenialReason
{
    ExplicitGlobalDeny,
    ResourceLevelDeny,
    NoPermissionFound,
    DelegationExpired,
    DelegationRevoked,
    DelegatorLostPermission,
    DelegationChainTooDeep,
    PolicyConditionFailed,
    CrossTenantRejection,
    ScopeNotInherited,
    AbacConditionFailed,
    TenantSuspended
}
