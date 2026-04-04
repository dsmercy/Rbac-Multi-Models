namespace RbacSystem.Api.Hubs;

/// <summary>
/// Payload pushed to all connected admin-panel clients for a tenant when
/// any RBAC entity changes. The React client uses <c>Type</c> to determine
/// which data to refetch and which cached permission checks to re-evaluate.
/// </summary>
/// <param name="Type">
/// Discriminator for the change category. One of:
/// <c>role</c>, <c>permission</c>, <c>assignment</c>, <c>policy</c>, <c>delegation</c>.
/// </param>
/// <param name="TenantId">Tenant the change belongs to. Matches the group the message is pushed to.</param>
/// <param name="ResourceId">
/// Optional UUID of the specific entity that changed (role ID, policy ID, delegation ID, etc.).
/// Null for events that invalidate all entities of a type (e.g. RoleDeleted without a specific resource context).
/// </param>
/// <param name="OccurredAt">UTC timestamp when the domain event was raised on the server.</param>
public sealed record RbacInvalidatedMessage(
    string          Type,
    Guid            TenantId,
    Guid?           ResourceId,
    DateTimeOffset  OccurredAt,
    Guid?           SecondaryId = null);
