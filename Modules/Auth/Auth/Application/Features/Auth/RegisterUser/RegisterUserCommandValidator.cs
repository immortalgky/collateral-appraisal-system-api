using FluentValidation;

namespace Auth.Domain.Auth.Features.RegisterUser;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage("Username is required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Email is required.");
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters.");
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters.");
        RuleFor(x => x.AvatarUrl)
            .MaximumLength(500).WithMessage("Avatar URL cannot exceed 500 characters.")
            .When(x => x.AvatarUrl != null);
        RuleFor(x => x.Position)
            .MaximumLength(100).WithMessage("Position cannot exceed 100 characters.")
            .When(x => x.Position != null);
        RuleFor(x => x.Department)
            .MaximumLength(100).WithMessage("Department cannot exceed 100 characters.")
            .When(x => x.Department != null);
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
