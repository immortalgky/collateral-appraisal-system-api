using FluentValidation;

namespace Auth.Application.Features.Permissions.CreatePermission;

public class CreatePermissionCommandValidator : AbstractValidator<CreatePermissionCommand>
{
    public CreatePermissionCommandValidator()
    {
        RuleFor(x => x.PermissionCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Module).NotEmpty().MaximumLength(50)
            .Must(m => new[] { "Auth", "Workflow", "Appraisal", "Request", "Common" }.Contains(m))
            .WithMessage("Module must be one of: Auth, Workflow, Appraisal, Request, Common");
    }
}
