using FluentValidation;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteHypothesisAnalysis;

public class DeleteHypothesisAnalysisCommandValidator : AbstractValidator<DeleteHypothesisAnalysisCommand>
{
    public DeleteHypothesisAnalysisCommandValidator()
    {
        RuleFor(x => x.PricingAnalysisId)
            .NotEmpty().WithMessage("PricingAnalysisId is required.");

        RuleFor(x => x.MethodId)
            .NotEmpty().WithMessage("MethodId is required.");
    }
}
