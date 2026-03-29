namespace PermissionEngine.Domain.Models;

/// <summary>
/// Carries all ambient data required for a single CanUserAccess evaluation.
///
/// Phase-3 additions:
///   TokenVersion — the "tv" claim from the caller's JWT; validated against
///                  Redis before evaluation begins (Step 0). Null = skip check.
///   IsSuperAdmin — when true the pipeline skips tenant isolation checks;
///                  used exclusively for platform-level operations.
/// </summary>
public sealed class EvaluationContext
{
    public Guid TenantId { get; init; }
    public Guid CorrelationId { get; init; }
    public DelegationChainInfo? DelegationChain { get; init; }

    // ?? JWT token metadata ????????????????????????????????????????????????????

    /// <summary>
    /// Token version embedded in the JWT ("tv" claim). The pipeline compares
    /// this against the current Redis value for the user. Null = no check.
    /// </summary>
    public int? TokenVersion { get; init; }

    /// <summary>
    /// True for tokens issued to the platform super-admin account.
    /// Bypasses tenant isolation but is still fully audited.
    /// </summary>
    public bool IsSuperAdmin { get; init; }

    // ?? ABAC attribute bags ???????????????????????????????????????????????????

    public IReadOnlyDictionary<string, object> UserAttributes { get; init; }
        = new Dictionary<string, object>();

    public IReadOnlyDictionary<string, object> ResourceAttributes { get; init; }
        = new Dictionary<string, object>();

    public IReadOnlyDictionary<string, object> EnvironmentAttributes { get; init; }
        = new Dictionary<string, object>();

    // ?? Constructors ??????????????????????????????????????????????????????????

    /// <summary>Default constructor — required for object-initializer usage.</summary>
    public EvaluationContext() { }

    public EvaluationContext(
        Guid tenantId,
        Guid correlationId,
        DelegationChainInfo? delegationChain = null,
        int? tokenVersion = null,
        bool isSuperAdmin = false,
        IDictionary<string, object>? userAttributes = null,
        IDictionary<string, object>? resourceAttributes = null,
        IDictionary<string, object>? environmentAttributes = null)
    {
        TenantId = tenantId;
        CorrelationId = correlationId;
        DelegationChain = delegationChain;
        TokenVersion = tokenVersion;
        IsSuperAdmin = isSuperAdmin;

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