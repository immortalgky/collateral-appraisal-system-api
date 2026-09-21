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

        RuleFor(x => x.ConstructionProgressPercent)
            .InclusiveBetween(0m, 100m).When(x => x.ConstructionProgressPercent.HasValue)
            .WithMessage("ConstructionProgressPercent must be between 0 and 100.");

        // The column is nvarchar(500) and this is a free-text list of deed numbers -- without a
        // rule here an over-long value surfaces as a DbUpdateException (500) instead of a 400.
        RuleFor(x => x.BuiltOnTitleDeedNumber)
            .MaximumLength(500)
            .WithMessage("BuiltOnTitleDeedNumber must be at most 500 characters.");

        // The column is decimal(7,4), so SQL Server rounds anything finer on store. Without this a
        // posted 99.99995 passes InclusiveBetween, lands as 100.0000, and the result feed reports a
        // development that is not finished as complete. Reject it rather than round it.
        RuleFor(x => x.ConstructionProgressPercent)
            .PrecisionScale(7, 4, ignoreTrailingZeros: true)
            .When(x => x.ConstructionProgressPercent.HasValue)
            .WithMessage(
                "ConstructionProgressPercent must have at most 3 digits before the decimal point "
                + "and 4 after.");

        RuleFor(x => x.ProjectSaleLaunchDate)
            .Must(PartialDate.IsValid)
            .WithMessage("ProjectSaleLaunchDate must be 'YYYY', 'YYYY-MM', or 'YYYY-MM-DD'.");
    }
}
