using FluentValidation;

namespace Auth.Domain.Auth.Features.UpdateProfile;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required.");
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
    }
}
