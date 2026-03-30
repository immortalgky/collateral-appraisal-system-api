using FluentValidation;

namespace Auth.Application.Features.Permissions.UpdatePermission;

public class UpdatePermissionCommandValidator : AbstractValidator<UpdatePermissionCommand>
{
    public UpdatePermissionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Module).NotEmpty().MaximumLength(50)
            .Must(m => new[] { "Auth", "Workflow", "Appraisal", "Request", "Common" }.Contains(m))
            .WithMessage("Module must be one of: Auth, Workflow, Appraisal, Request, Common");
    }
}
