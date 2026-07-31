using Auth.Domain;
using FluentValidation;

namespace Auth.Application.Features.Teams.UpdateTeam;

public class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scope).NotEmpty().Must(AuthScopes.IsValid)
            .WithMessage("Scope must be 'Bank' or 'Company'.");
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
