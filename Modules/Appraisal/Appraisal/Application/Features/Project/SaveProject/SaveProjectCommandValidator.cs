using Appraisal.Domain.Projects;
using FluentValidation;

namespace Appraisal.Application.Features.Project.SaveProject;

/// <summary>
/// Validates the SaveProjectCommand (FINAL save). Enforces the sanity invariants shared with
/// the draft validator PLUS the required business fields that a finished project must have.
/// The draft flow (SaveProjectDraftCommandValidator) omits the required-field block below.
/// </summary>
public class SaveProjectCommandValidator : AbstractValidator<SaveProjectCommand>
{
    public SaveProjectCommandValidator()
    {
        RuleFor(x => x.AppraisalId)
            .NotEmpty().WithMessage("AppraisalId is required.");

        RuleFor(x => x.ProjectType)
            .Must(ProjectType.IsValidCode).WithMessage("ProjectType must be a valid code (\"U\"=Condo, \"LB\"=LandAndBuilding, \"L\"=Land).");

        // ---- Final-only required business fields (NOT enforced for drafts) ----
        // Mirrors the frontend's required fields on final Save (blockProject form).
        // Extend this block with any other fields the business requires on a completed project.
        RuleFor(x => x.ProjectName)
            .NotEmpty().WithMessage("ProjectName is required.");

        RuleFor(x => x.LandOffice)
            .NotEmpty().WithMessage("LandOffice is required.");

        // Type-specific cross-field rules
        // TODO(Land): Land follows LandAndBuilding rules in v1 — BuiltOnTitleDeedNumber is Condo-only
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
