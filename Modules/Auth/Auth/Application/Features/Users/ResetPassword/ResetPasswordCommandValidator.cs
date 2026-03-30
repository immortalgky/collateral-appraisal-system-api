using FluentValidation;

namespace Auth.Application.Features.Users.ResetPassword;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.ConfirmPassword).NotEmpty()
            .Equal(x => x.NewPassword).WithMessage("Confirm password must match new password.");
    }
}
