using FluentValidation;

namespace Auth.Application.Features.Auth.PasswordPolicyAdmin.UpdatePasswordPolicyConfig;

public class UpdatePasswordPolicyConfigCommandValidator : AbstractValidator<UpdatePasswordPolicyConfigCommand>
{
    public UpdatePasswordPolicyConfigCommandValidator()
    {
        RuleFor(x => x.RequiredLength).InclusiveBetween(1, 128);
        RuleFor(x => x.RequiredUniqueChars).InclusiveBetween(0, 128);
        RuleFor(x => x.ExpiryDays).InclusiveBetween(0, 3650);
        RuleFor(x => x.HistoryCount).InclusiveBetween(0, 50);
        RuleFor(x => x.MaxFailedAccessAttempts).InclusiveBetween(1, 100);
        RuleFor(x => x.LockoutMinutes).InclusiveBetween(0, 100000);
        RuleFor(x => x.Blocklist).MaximumLength(8000).When(x => x.Blocklist is not null);
    }
}
