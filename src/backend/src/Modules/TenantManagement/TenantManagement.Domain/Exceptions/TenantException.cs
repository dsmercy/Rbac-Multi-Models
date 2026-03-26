using BuildingBlocks.Domain;

namespace TenantManagement.Domain.Exceptions;

public sealed class TenantException : DomainException
{
    public TenantException(string code, string message)
        : base(code, message) { }
}
