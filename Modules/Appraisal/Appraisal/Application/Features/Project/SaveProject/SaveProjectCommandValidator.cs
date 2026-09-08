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
        //
        // KNOWN GAP (shared by every ProjectType-gated rule in this file, the construction-progress
        // rules below included): these gate on the ProjectType in the REQUEST, but on the update
        // path ProjectSaveService ignores it -- type is immutable after creation -- and
        // Project.Update branches on the PERSISTED type. A payload carrying the wrong type
        // therefore skips the guard while the aggregate applies the other one's rules; for the
        // construction pair that means a "ticked but blank" row can slip through.
        //
        // Comparing the two types in ProjectSaveService looks like the cheap fix. It is not: the
        // frontend has no route for Land, so a Land project posts "LB" on every save and a strict
        // comparison 400s it forever (see the comment there). Closing this properly means
        // validating against the stored project -- a DB read inside the validator -- across every
        // rule here, and belongs in its own PR alongside the missing "L" route.
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

        RuleFor(x => x.ConstructionProgressPercent)
            .InclusiveBetween(0m, 100m).When(x => x.ConstructionProgressPercent.HasValue)
            .WithMessage("ConstructionProgressPercent must be between 0 and 100.");

        // Deliberately NOT requiring IsUnderConstruction itself. Doing so would make a NULL flag
        // mean "row predates this column" and nothing else, which is tempting -- but it is a new
        // hard requirement on an endpoint that already ships, and the frontend releases from a
        // separate repo on a separate schedule. Any client that does not yet post the field would
        // 400 on every final Save of a U/LB block until the paired release lands. What NULL means
        // is settled downstream instead (GetAppraisalResult reads it as "not under construction"
        // on a completed appraisal -- a PO decision, no backfill), so nothing here has to break
        // older clients to make it unambiguous.
        //
        // A project flagged as still being built has to say how far along it is: that percent is
        // what the result API hands to LOS.
        // A percent sent without the flag is DISCARDED by the aggregate, not rejected: the form
        // clears the input on untick, so a stray value means the clamp lost a race, and blocking
        // Save over a field the appraiser cannot see or reach is the worse failure. Discarding it
        // matches how the aggregate treats every other inapplicable field. Leaving the flag off is not an error -- a completed
        // appraisal then reports 100, an open one reports nothing.
        // Scoped to the types that can carry a structure, because Project.Create/Update discard
        // both fields on bare Land. Note the scope is mostly theoretical today: it reads the type
        // from the REQUEST, and the frontend has no "L" route, so a Land project posts "LB" and
        // takes this rule anyway (see ProjectSaveService). It becomes real the moment that route
        // exists -- keep it.
        RuleFor(x => x.ConstructionProgressPercent)
            .NotNull()
            .When(x => ProjectType.HasStructuresCode(x.ProjectType) && x.IsUnderConstruction == true)
            .WithMessage("ConstructionProgressPercent is required when the project is under construction.");

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
