using FluentValidation;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteHypothesisUnitDetailUpload;

public class DeleteHypothesisUnitDetailUploadCommandValidator : AbstractValidator<DeleteHypothesisUnitDetailUploadCommand>
{
    public DeleteHypothesisUnitDetailUploadCommandValidator()
    {
        RuleFor(x => x.PricingAnalysisId)
            .NotEmpty().WithMessage("PricingAnalysisId is required.");

        RuleFor(x => x.MethodId)
            .NotEmpty().WithMessage("MethodId is required.");

        RuleFor(x => x.UploadId)
            .NotEmpty().WithMessage("UploadId is required.");
    }
}
