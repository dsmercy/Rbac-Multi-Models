using FluentValidation;

namespace TenantManagement.Application.Validators;

public sealed class CreateTenantCommandValidator
    : AbstractValidator<Commands.CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant name is required.")
            .MaximumLength(200).WithMessage("Tenant name must not exceed 200 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MinimumLength(3).WithMessage("Slug must be at least 3 characters.")
            .MaximumLength(63).WithMessage("Slug must not exceed 63 characters.")
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");

        RuleFor(x => x.AdminEmail)
            .NotEmpty().WithMessage("Admin email is required.")
            .EmailAddress().WithMessage("Admin email must be valid.");

        RuleFor(x => x.AdminPassword)
            .NotEmpty().WithMessage("Admin password is required.")
            .MinimumLength(12).WithMessage("Admin password must be at least 12 characters.");

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty().WithMessage("CreatedByUserId is required.");
    }
}
