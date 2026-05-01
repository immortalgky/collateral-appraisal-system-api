using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.CostItems;
using FluentValidation;

namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

public class SaveHypothesisAnalysisCommandValidator : AbstractValidator<SaveHypothesisAnalysisCommand>
{
    private const int MaxRemarkLength = 2000;

    public SaveHypothesisAnalysisCommandValidator()
    {
        RuleFor(x => x.PricingAnalysisId)
            .NotEmpty().WithMessage("PricingAnalysisId is required.");

        RuleFor(x => x.MethodId)
            .NotEmpty().WithMessage("MethodId is required.");

        RuleFor(x => x.Remark)
            .MaximumLength(MaxRemarkLength)
            .When(x => x.Remark is not null)
            .WithMessage($"Remark cannot exceed {MaxRemarkLength} characters.");

        RuleFor(x => x.CostItems)
            .NotNull().WithMessage("CostItems must not be null.");

        RuleForEach(x => x.CostItems).ChildRules(item =>
        {
            item.RuleFor(i => i.Category)
                .IsInEnum().WithMessage("CostItem Category must be a valid HypothesisCostCategory value.");

            item.RuleFor(i => i.Kind)
                .IsInEnum().WithMessage("CostItem Kind must be a valid CostItemKind value.");

            item.RuleFor(i => i.Description)
                .NotEmpty().WithMessage("CostItem Description is required.")
                .MaximumLength(500).WithMessage("CostItem Description cannot exceed 500 characters.");

            item.RuleFor(i => i.Amount)
                .GreaterThanOrEqualTo(0m).WithMessage("CostItem Amount cannot be negative.");

            item.RuleFor(i => i.RatePercent)
                .InclusiveBetween(0m, 100m)
                .When(i => i.RatePercent.HasValue)
                .WithMessage("CostItem RatePercent must be between 0 and 100.");

            // CostOfBuilding items must supply a ModelName
            item.RuleFor(i => i.ModelName)
                .NotEmpty()
                .When(i => i.Category == HypothesisCostCategory.CostOfBuilding)
                .WithMessage("ModelName is required for CostOfBuilding items.");

            // Variant/category compatibility
            item.RuleFor(i => i.Category)
                .Must(cat => IsLandBuildingCategory(cat))
                .When(i => IsLandBuildingCategory(i.Category))
                .WithMessage("Category is not valid for the requested variant.");
        });

        // At least one of LandBuildingSummary or CondominiumSummary must be supplied
        RuleFor(x => x)
            .Must(x => x.LandBuildingSummary is not null || x.CondominiumSummary is not null)
            .WithMessage("Either LandBuildingSummary or CondominiumSummary must be provided.");

        // Percent fields range (0-100) on L&B summary
        When(x => x.LandBuildingSummary is not null, () =>
        {
            RuleFor(x => x.LandBuildingSummary!.C78DiscountRate)
                .InclusiveBetween(0m, 100m)
                .When(x => x.LandBuildingSummary!.C78DiscountRate.HasValue)
                .WithMessage("C78 DiscountRate must be between 0 and 100.");

            RuleFor(x => x.LandBuildingSummary!.C74RiskPremiumPercent)
                .InclusiveBetween(0m, 100m)
                .When(x => x.LandBuildingSummary!.C74RiskPremiumPercent.HasValue)
                .WithMessage("C74 RiskPremiumPercent must be between 0 and 100.");
        });

        // Percent fields on Condo summary
        When(x => x.CondominiumSummary is not null, () =>
        {
            RuleFor(x => x.CondominiumSummary!.E55DiscountRate)
                .InclusiveBetween(0m, 100m)
                .When(x => x.CondominiumSummary!.E55DiscountRate.HasValue)
                .WithMessage("E55 DiscountRate must be between 0 and 100.");

            RuleFor(x => x.CondominiumSummary!.E51RiskProfitPercent)
                .InclusiveBetween(0m, 100m)
                .When(x => x.CondominiumSummary!.E51RiskProfitPercent.HasValue)
                .WithMessage("E51 RiskProfitPercent must be between 0 and 100.");
        });
    }

    private static bool IsLandBuildingCategory(HypothesisCostCategory cat) =>
        cat is HypothesisCostCategory.CostOfBuilding
            or HypothesisCostCategory.ProjectDevCost
            or HypothesisCostCategory.ProjectCost
            or HypothesisCostCategory.GovernmentTax;
}
