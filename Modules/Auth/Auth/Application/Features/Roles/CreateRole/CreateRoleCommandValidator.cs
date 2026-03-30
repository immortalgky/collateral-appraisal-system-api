using FluentValidation;

namespace Auth.Application.Features.Roles.CreateRole;

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Scope).MaximumLength(50)
            .Must(s => s == null || new[] { "Bank", "Company" }.Contains(s))
            .WithMessage("Scope must be 'Bank', 'Company', or null.");
        RuleFor(x => x.PermissionIds).NotNull();
        RuleForEach(x => x.PermissionIds).NotEmpty();
    }
}
