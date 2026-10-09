using FluentValidation;
using SaveNs = Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewHypothesisAnalysis;

public class PreviewHypothesisAnalysisCommandValidator : AbstractValidator<PreviewHypothesisAnalysisCommand>
{
    public PreviewHypothesisAnalysisCommandValidator()
    {
        RuleFor(x => x.PricingAnalysisId)
            .NotEmpty().WithMessage("PricingAnalysisId is required.");

        RuleFor(x => x.MethodId)
            .NotEmpty().WithMessage("MethodId is required.");

        RuleFor(x => x.CostItems)
            .NotNull().WithMessage("CostItems must not be null.");

        RuleForEach(x => x.ModelBuildingMappings).ChildRules(m =>
        {
            m.RuleFor(i => i.ModelName)
                .NotEmpty().WithMessage("ModelBuildingMapping ModelName is required.")
                .MaximumLength(200).WithMessage("ModelBuildingMapping ModelName cannot exceed 200 characters.");

            m.RuleFor(i => i.TotalCost)
                .GreaterThanOrEqualTo(0m)
                .When(i => i.TotalCost.HasValue)
                .WithMessage("ModelBuildingMapping TotalCost cannot be negative.");
        }).When(x => x.ModelBuildingMappings is not null);

        // One mapping per house model. Save keeps one row per name while the calc (and so Preview)
        // takes the first match — duplicates would make the saved total differ from the preview.
        RuleFor(x => x.ModelBuildingMappings)
            .Must(m => m!.Select(i => (i.ModelName ?? "").Trim())
                           .Distinct(StringComparer.OrdinalIgnoreCase).Count() == m!.Count())
            .WithMessage("ModelBuildingMappings cannot contain the same house model twice.")
            .When(x => x.ModelBuildingMappings is not null);

        RuleForEach(x => x.CostItems).ChildRules(item =>
        {
            item.RuleFor(i => i.Category)
                .IsInEnum().WithMessage("CostItem Category must be a valid value.");

            item.RuleFor(i => i.Kind)
                .IsInEnum().WithMessage("CostItem Kind must be a valid value.");

            item.RuleFor(i => i.Amount)
                .GreaterThanOrEqualTo(0m).WithMessage("CostItem Amount cannot be negative.");
        });
    }
}
