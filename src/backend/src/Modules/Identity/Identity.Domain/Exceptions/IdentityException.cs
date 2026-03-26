using BuildingBlocks.Domain;

namespace Identity.Domain.Exceptions;

public sealed class IdentityException : DomainException
{
    public IdentityException(string code, string message)
        : base(code, message) { }
}
