using FluentValidation;

namespace Appraisal.Application.Features.Project.SaveProject;

/// <summary>
/// Validates the SaveProjectCommand.
/// PUT-style: the command always carries the full project state.
/// Truly optional fields (all the nullable project details) are allowed to be null.
/// Only structural invariants are enforced here.
/// </summary>
public class SaveProjectCommandValidator : AbstractValidator<SaveProjectCommand>
{
    public SaveProjectCommandValidator()
    {
        RuleFor(x => x.AppraisalId)
            .NotEmpty().WithMessage("AppraisalId is required.");

        RuleFor(x => x.ProjectType)
            .IsInEnum().WithMessage("ProjectType must be a valid value (Condo=1, LandAndBuilding=2).");

        // Type-specific cross-field rules
        RuleFor(x => x.BuiltOnTitleDeedNumber)
            .Null()
            .When(x => x.ProjectType == ProjectType.LandAndBuilding)
            .WithMessage("BuiltOnTitleDeedNumber is only applicable to Condo projects.");

        RuleFor(x => x.LicenseExpirationDate)
            .Null()
            .When(x => x.ProjectType == ProjectType.Condo)
            .WithMessage("LicenseExpirationDate is only applicable to LandAndBuilding projects.");

        // Numeric range guards
        RuleFor(x => x.LandAreaRai)
            .GreaterThanOrEqualTo(0).When(x => x.LandAreaRai.HasValue)
            .WithMessage("LandAreaRai cannot be negative.");

        RuleFor(x => x.LandAreaNgan)
            .GreaterThanOrEqualTo(0).When(x => x.LandAreaNgan.HasValue)
            .WithMessage("LandAreaNgan cannot be negative.");

        RuleFor(x => x.LandAreaWa)
            .GreaterThanOrEqualTo(0).When(x => x.LandAreaWa.HasValue)
            .WithMessage("LandAreaWa cannot be negative.");

        RuleFor(x => x.UnitForSaleCount)
            .GreaterThanOrEqualTo(0).When(x => x.UnitForSaleCount.HasValue)
            .WithMessage("UnitForSaleCount cannot be negative.");

        RuleFor(x => x.NumberOfPhase)
            .GreaterThanOrEqualTo(0).When(x => x.NumberOfPhase.HasValue)
            .WithMessage("NumberOfPhase cannot be negative.");
    }
}
