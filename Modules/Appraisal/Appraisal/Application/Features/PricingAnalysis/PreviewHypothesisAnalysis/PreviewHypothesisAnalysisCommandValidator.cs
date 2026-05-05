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
