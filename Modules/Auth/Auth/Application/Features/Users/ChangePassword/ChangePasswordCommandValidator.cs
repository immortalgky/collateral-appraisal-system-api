using FluentValidation;

namespace Auth.Application.Features.Users.ChangePassword;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        // Length/complexity are enforced by DbPasswordValidator from the DB-maintained policy.
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
        RuleFor(x => x.ConfirmPassword).NotEmpty()
            .Equal(x => x.NewPassword).WithMessage("Confirm password must match new password.");
    }
}
