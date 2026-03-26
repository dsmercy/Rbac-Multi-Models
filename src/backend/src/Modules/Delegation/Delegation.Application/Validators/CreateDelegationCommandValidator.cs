using FluentValidation;

namespace Delegation.Application.Validators;

public sealed class CreateDelegationCommandValidator
    : AbstractValidator<Commands.CreateDelegationCommand>
{
    public CreateDelegationCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("TenantId is required.");
        RuleFor(x => x.DelegatorId).NotEmpty().WithMessage("DelegatorId is required.");
        RuleFor(x => x.DelegateeId).NotEmpty().WithMessage("DelegateeId is required.");

        RuleFor(x => x.DelegateeId)
            .NotEqual(x => x.DelegatorId)
            .WithMessage("Delegatee cannot be the same as the delegator.");

        RuleFor(x => x.PermissionCodes)
            .NotEmpty().WithMessage("At least one permission code is required.")
            .Must(codes => codes.All(c => !string.IsNullOrWhiteSpace(c)))
            .WithMessage("Permission codes cannot be empty strings.");

        RuleFor(x => x.ScopeId).NotEmpty().WithMessage("ScopeId is required.");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("ExpiresAt must be in the future.")
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.AddYears(1))
            .WithMessage("Delegation cannot exceed 1 year.");
    }
}
