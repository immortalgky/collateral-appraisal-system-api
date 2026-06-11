using FluentValidation;

namespace Auth.Application.Features.Users.ResetPassword;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        // Length/complexity are enforced by DbPasswordValidator from the DB-maintained policy.
        RuleFor(x => x.NewPassword).NotEmpty();
        RuleFor(x => x.ConfirmPassword).NotEmpty()
            .Equal(x => x.NewPassword).WithMessage("Confirm password must match new password.");
    }
}
