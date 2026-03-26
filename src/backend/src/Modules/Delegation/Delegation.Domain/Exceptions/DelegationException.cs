using BuildingBlocks.Domain;

namespace Delegation.Domain.Exceptions;

public sealed class DelegationException : DomainException
{
    public DelegationException(string code, string message)
        : base(code, message) { }
}
