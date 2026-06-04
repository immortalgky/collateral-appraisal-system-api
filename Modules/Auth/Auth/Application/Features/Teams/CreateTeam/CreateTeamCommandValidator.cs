using FluentValidation;

namespace Auth.Application.Features.Teams.CreateTeam;

public class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).NotEmpty().Must(t => t == "Internal" || t == "External")
            .WithMessage("Type must be 'Internal' or 'External'.");
    }
}
