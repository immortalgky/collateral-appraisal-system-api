using FluentValidation;

namespace Appraisal.Application.Features.PricingAnalysis.UploadHypothesisUnitDetails;

public class UploadHypothesisUnitDetailsCommandValidator : AbstractValidator<UploadHypothesisUnitDetailsCommand>
{
    private const int MaxFileNameLength = 500;

    public UploadHypothesisUnitDetailsCommandValidator()
    {
        RuleFor(x => x.PricingAnalysisId)
            .NotEmpty().WithMessage("PricingAnalysisId is required.");

        RuleFor(x => x.MethodId)
            .NotEmpty().WithMessage("MethodId is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("FileName is required.")
            .MaximumLength(MaxFileNameLength).WithMessage($"FileName cannot exceed {MaxFileNameLength} characters.");

        RuleFor(x => x.FileStream)
            .NotNull().WithMessage("FileStream is required.");
    }
}
