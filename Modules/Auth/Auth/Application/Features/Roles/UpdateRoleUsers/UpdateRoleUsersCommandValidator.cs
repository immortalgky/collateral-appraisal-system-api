using FluentValidation;

namespace Auth.Application.Features.Roles.UpdateRoleUsers;

public class UpdateRoleUsersCommandValidator : AbstractValidator<UpdateRoleUsersCommand>
{
    public UpdateRoleUsersCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.UserIds).NotNull();
        RuleForEach(x => x.UserIds).NotEmpty();
    }
}
