using FluentValidation;

namespace Auth.Application.Features.Teams.UpdateTeam;

public class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).NotEmpty().Must(t => t == "Internal" || t == "External")
            .WithMessage("Type must be 'Internal' or 'External'.");
    }
}
