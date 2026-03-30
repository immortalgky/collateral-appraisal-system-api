using FluentValidation;

namespace Auth.Application.Features.Users.ChangePassword;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.ConfirmPassword).NotEmpty()
            .Equal(x => x.NewPassword).WithMessage("Confirm password must match new password.");
    }
}
