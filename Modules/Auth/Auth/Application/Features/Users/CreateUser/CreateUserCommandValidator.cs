using FluentValidation;

namespace Auth.Application.Features.Users.CreateUser;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(256);
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(256);
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters.");
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters.");
        RuleFor(x => x.Position)
            .MaximumLength(100).WithMessage("Position cannot exceed 100 characters.")
            .When(x => x.Position != null);
        RuleFor(x => x.Department)
            .MaximumLength(100).WithMessage("Department cannot exceed 100 characters.")
            .When(x => x.Department != null);
        RuleFor(x => x.Roles).NotNull().WithMessage("Roles are required.");
        RuleForEach(x => x.Roles).NotEmpty().WithMessage("RoleId is required.");
    }
}
