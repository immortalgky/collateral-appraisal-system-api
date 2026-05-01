using FluentValidation;

namespace Appraisal.Application.Features.PricingAnalysis.GetHypothesisAnalysis;

public class GetHypothesisAnalysisQueryValidator : AbstractValidator<GetHypothesisAnalysisQuery>
{
    public GetHypothesisAnalysisQueryValidator()
    {
        RuleFor(x => x.PricingAnalysisId)
            .NotEmpty().WithMessage("PricingAnalysisId is required.");

        RuleFor(x => x.MethodId)
            .NotEmpty().WithMessage("MethodId is required.");
    }
}
