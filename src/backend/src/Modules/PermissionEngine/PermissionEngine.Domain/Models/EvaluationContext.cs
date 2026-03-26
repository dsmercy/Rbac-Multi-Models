namespace PermissionEngine.Domain.Models;

public sealed class EvaluationContext
{
    public Guid TenantId { get; init; }
    public Guid CorrelationId { get; init; }
    public DelegationChainInfo? DelegationChain { get; init; }
    public IReadOnlyDictionary<string, object> UserAttributes { get; init; }
        = new Dictionary<string, object>();
    public IReadOnlyDictionary<string, object> ResourceAttributes { get; init; }
        = new Dictionary<string, object>();
    public IReadOnlyDictionary<string, object> EnvironmentAttributes { get; init; }
        = new Dictionary<string, object>();

    public EvaluationContext() { }

    public EvaluationContext(
        Guid tenantId,
        Guid correlationId,
        DelegationChainInfo? delegationChain = null,
        IDictionary<string, object>? userAttributes = null,
        IDictionary<string, object>? resourceAttributes = null,
        IDictionary<string, object>? environmentAttributes = null)
    {
        TenantId = tenantId;
        CorrelationId = correlationId;
        DelegationChain = delegationChain;
        UserAttributes = userAttributes is not null
            ? new Dictionary<string, object>(userAttributes)
            : new Dictionary<string, object>();
        ResourceAttributes = resourceAttributes is not null
            ? new Dictionary<string, object>(resourceAttributes)
            : new Dictionary<string, object>();
        EnvironmentAttributes = environmentAttributes is not null
            ? new Dictionary<string, object>(environmentAttributes)
            : new Dictionary<string, object>();
    }
}
