using FluentValidation;

namespace Auth.Application.Features.Users.SetAccessWindow;

public class SetAccessWindowCommandValidator : AbstractValidator<SetAccessWindowCommand>
{
    public SetAccessWindowCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ExpiresAt).NotEmpty();
        // The whole system runs on bank-local time and the handler compares this against a local
        // clock. A UTC instant ("…Z") would arrive seven hours behind, and since an expiry in the
        // past means "close the window", an afternoon window would silently close the account and
        // rotate its password while reporting success. Refuse it instead of guessing the intent.
        RuleFor(x => x.ExpiresAt)
            .Must(value => value.Kind != DateTimeKind.Utc)
            .WithMessage("Send the expiry in local time, without a UTC offset.");
        // The reason is the whole audit story for a shared account: the log records which admin
        // opened the window, but only this says why. Mandatory, not advisory.
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required — it is recorded in the audit log.")
            .MaximumLength(500);
    }
}
