using Appraisal.Application.Configurations;
using Appraisal.Domain.Appraisals;
using MediatR;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteHypothesisAnalysis;

/// <summary>
/// Resets the hypothesis analysis for the given method (the "Reset" button).
/// Removes the HypothesisAnalysis entity — cascade deletes all uploads, rows, and cost items.
/// </summary>
public class DeleteHypothesisAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<DeleteHypothesisAnalysisCommand>
{
    public async Task<Unit> Handle(
        DeleteHypothesisAnalysisCommand command,
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

        if (method.HypothesisAnalysis is null)
            return Unit.Value; // Idempotent — nothing to delete

        method.ClearHypothesisAnalysis();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
