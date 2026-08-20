using Auth.Domain;
using FluentValidation;

namespace Auth.Application.Features.Teams.CreateTeam;

public class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scope).NotEmpty().Must(AuthScopes.IsValid)
            .WithMessage("Scope must be 'Bank' or 'Company'.");
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
