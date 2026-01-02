using FluentValidation;

namespace Auth.Domain.Roles.Features.CreateRole;

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required.");
        RuleFor(x => x.Permissions).NotNull().WithMessage("Permissions are required.");
        RuleForEach(x => x.Permissions).NotEmpty().WithMessage("Permission is required.");
    }
}
