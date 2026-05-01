using FluentValidation;

namespace Appraisal.Application.Features.PricingAnalysis.GenerateHypothesisAnalysis;

public class GenerateHypothesisAnalysisCommandValidator : AbstractValidator<GenerateHypothesisAnalysisCommand>
{
    public GenerateHypothesisAnalysisCommandValidator()
    {
        RuleFor(x => x.PricingAnalysisId)
            .NotEmpty().WithMessage("PricingAnalysisId is required.");

        RuleFor(x => x.MethodId)
            .NotEmpty().WithMessage("MethodId is required.");

        RuleFor(x => x.Variant)
            .IsInEnum().WithMessage("Variant must be a valid value (LandBuilding=1, Condominium=2).");
    }
}
