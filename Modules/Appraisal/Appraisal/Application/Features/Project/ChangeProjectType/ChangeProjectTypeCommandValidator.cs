using Appraisal.Domain.Projects;
using FluentValidation;

namespace Appraisal.Application.Features.Project.ChangeProjectType;

/// <summary>
/// Validates the ChangeProjectTypeCommand.
/// Only structural invariants are enforced here; business guards (project not found,
/// type unchanged) are enforced in the command handler.
/// </summary>
public class ChangeProjectTypeCommandValidator : AbstractValidator<ChangeProjectTypeCommand>
{
    public ChangeProjectTypeCommandValidator()
    {
        RuleFor(x => x.AppraisalId)
            .NotEmpty().WithMessage("AppraisalId is required.");

        RuleFor(x => x.NewProjectType)
            .Must(ProjectType.IsValidCode).WithMessage("NewProjectType must be a valid code (\"U\"=Condo, \"LB\"=LandAndBuilding, \"L\"=Land).");
    }
}
