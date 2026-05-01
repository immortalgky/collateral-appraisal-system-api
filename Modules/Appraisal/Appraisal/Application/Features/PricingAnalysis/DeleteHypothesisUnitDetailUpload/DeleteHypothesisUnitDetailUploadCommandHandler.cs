using Appraisal.Application.Configurations;
using Appraisal.Domain.Appraisals;
using MediatR;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteHypothesisUnitDetailUpload;

public class DeleteHypothesisUnitDetailUploadCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<DeleteHypothesisUnitDetailUploadCommand>
{
    public async Task<Unit> Handle(
        DeleteHypothesisUnitDetailUploadCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
                                 command.PricingAnalysisId, cancellationToken)
                             ?? throw new InvalidOperationException(
                                 $"PricingAnalysis {command.PricingAnalysisId} not found");

        var method = pricingAnalysis.Approaches
                         .SelectMany(a => a.Methods)
                         .FirstOrDefault(m => m.Id == command.MethodId)
                     ?? throw new InvalidOperationException(
                         $"PricingAnalysisMethod {command.MethodId} not found");

        var analysis = method.HypothesisAnalysis
                       ?? throw new InvalidOperationException("Hypothesis analysis not found.");

        analysis.RemoveUpload(command.UploadId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
