using FluentValidation;

namespace Auth.Domain.Auth.Features.RegisterClient;

public class RegisterClientCommandValidator : AbstractValidator<RegisterClientCommand>
{
    public RegisterClientCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage("DisplayName is required.");
        RuleFor(x => x.ClientType).NotEmpty().WithMessage("ClientType is required.");
        RuleFor(x => x.PostLogoutRedirectUris)
            .NotNull()
            .WithMessage("PostLogoutRedirectUris are required.");
        RuleForEach(x => x.PostLogoutRedirectUris)
            .NotNull()
            .WithMessage("PostLogoutRedirectUri is required.");
        RuleFor(x => x.RedirectUris).NotNull().WithMessage("RedirectUris are required.");
        RuleForEach(x => x.RedirectUris).NotNull().WithMessage("RedirectUri is required.");
        RuleFor(x => x.Permissions).NotNull().WithMessage("Permissions are required.");
        RuleForEach(x => x.Permissions).NotEmpty().WithMessage("Permission is required.");
        RuleFor(x => x.Requirements).NotNull().WithMessage("Requirements are required.");
        RuleForEach(x => x.Requirements).NotEmpty().WithMessage("Requirement is required.");
    }
}
