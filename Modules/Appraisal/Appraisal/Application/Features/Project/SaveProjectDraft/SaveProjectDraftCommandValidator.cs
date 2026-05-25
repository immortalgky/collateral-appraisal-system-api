using Appraisal.Domain.Projects;
using FluentValidation;

namespace Appraisal.Application.Features.Project.SaveProjectDraft;

/// <summary>
/// Lenient validator for DRAFT saves. Enforces only structural/sanity invariants — NOT
/// required business fields. A half-filled draft must be savable. The sanity rules below
/// mirror what the domain Project.Create/Update enforce, so they are kept here too (a draft
/// still cannot persist a negative area, a malformed date, or a type/field mismatch).
/// </summary>
public class SaveProjectDraftCommandValidator : AbstractValidator<SaveProjectDraftCommand>
{
    public SaveProjectDraftCommandValidator()
    {
        RuleFor(x => x.AppraisalId)
            .NotEmpty().WithMessage("AppraisalId is required.");

        // ProjectType is the aggregate discriminator and a NOT NULL column — required even for drafts.
        RuleFor(x => x.ProjectType)
            .Must(ProjectType.IsValidCode)
            .WithMessage("ProjectType must be a valid code (\"U\"=Condo, \"LB\"=LandAndBuilding, \"L\"=Land).");

        // Type-specific cross-field rules (domain enforces these regardless of mode)
        RuleFor(x => x.BuiltOnTitleDeedNumber)
            .Null()
            .When(x => ProjectType.IsLandAndBuildingLikeCode(x.ProjectType))
            .WithMessage("BuiltOnTitleDeedNumber is only applicable to Condo projects.");

        RuleFor(x => x.LicenseExpirationDate)
            .Null()
            .When(x => ProjectType.IsCondoCode(x.ProjectType))
            .WithMessage("LicenseExpirationDate is only applicable to LandAndBuilding / Land projects.");

        // Numeric range guards
        RuleFor(x => x.LandAreaRai)
            .GreaterThanOrEqualTo(0).When(x => x.LandAreaRai.HasValue)
            .WithMessage("LandAreaRai cannot be negative.");

        RuleFor(x => x.LandAreaNgan)
            .GreaterThanOrEqualTo(0).When(x => x.LandAreaNgan.HasValue)
            .WithMessage("LandAreaNgan cannot be negative.");

        RuleFor(x => x.LandAreaSquareWa)
            .GreaterThanOrEqualTo(0).When(x => x.LandAreaSquareWa.HasValue)
            .WithMessage("LandAreaSquareWa cannot be negative.");

        RuleFor(x => x.UnitForSaleCount)
            .GreaterThanOrEqualTo(0).When(x => x.UnitForSaleCount.HasValue)
            .WithMessage("UnitForSaleCount cannot be negative.");

        RuleFor(x => x.NumberOfPhase)
            .GreaterThanOrEqualTo(0).When(x => x.NumberOfPhase.HasValue)
            .WithMessage("NumberOfPhase cannot be negative.");

        RuleFor(x => x.ProjectSaleLaunchDate)
            .Must(PartialDate.IsValid)
            .WithMessage("ProjectSaleLaunchDate must be 'YYYY', 'YYYY-MM', or 'YYYY-MM-DD'.");
    }
}
