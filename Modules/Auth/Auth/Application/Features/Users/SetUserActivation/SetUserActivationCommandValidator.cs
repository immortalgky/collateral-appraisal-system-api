using FluentValidation;

namespace Auth.Application.Features.Users.SetUserActivation;

public class SetUserActivationCommandValidator : AbstractValidator<SetUserActivationCommand>
{
    public SetUserActivationCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
