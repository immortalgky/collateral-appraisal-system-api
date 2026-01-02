using FluentValidation;

namespace Auth.Domain.Permissions.Features.CreatePermission;

public class CreatePermissionCommandValidator : AbstractValidator<CreatePermissionCommand>
{
    public CreatePermissionCommandValidator()
    {
        RuleFor(x => x.PermissionCode).NotEmpty().WithMessage("PermissionCode is required.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required.");
    }
}
