using FluentValidation;

namespace Auth.Auth.Features.RegisterUser;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage("Username is required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Email is required.");
        RuleFor(x => x.Permissions).NotNull().WithMessage("Permissions are required.");
        RuleForEach(x => x.Permissions)
            .ChildRules(userPermissionDto =>
            {
                userPermissionDto.RuleFor(y => y.PermissionId).NotEmpty();
            })
            .WithMessage("PermissionId is required.");
        RuleFor(x => x.Roles).NotNull().WithMessage("Roles are required.");
        RuleForEach(x => x.Roles).NotEmpty().WithMessage("RoleId is required.");
    }
}
