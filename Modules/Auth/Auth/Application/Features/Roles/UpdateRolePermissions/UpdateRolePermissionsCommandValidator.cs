using FluentValidation;

namespace Auth.Application.Features.Roles.UpdateRolePermissions;

public class UpdateRolePermissionsCommandValidator : AbstractValidator<UpdateRolePermissionsCommand>
{
    public UpdateRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.PermissionIds).NotNull();
        RuleForEach(x => x.PermissionIds).NotEmpty();
    }
}
