using Appraisal.Application.Services;

namespace Appraisal.Application.Features.PricingAnalysis.StartPricingAnalysis;

/// <summary>
/// Handler for starting pricing analysis. Re-runs the readiness preconditions
/// in case the underlying data regressed since the analysis was created
/// (e.g. an appraiser reverted a property to Draft, removed market surveys, etc.).
/// </summary>
public class StartPricingAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IPricingAnalysisReadinessService readinessService
) : ICommandHandler<StartPricingAnalysisCommand, StartPricingAnalysisResult>
{
    public async Task<StartPricingAnalysisResult> Handle(
        StartPricingAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdAsync(command.Id, cancellationToken)
                              ?? throw new InvalidOperationException(
                                  $"Pricing analysis {command.Id} not found");
        
        if (pricingAnalysis.SubjectType != PricingAnalysisSubjectType.PropertyGroup || !pricingAnalysis.PropertyGroupId.HasValue)
          throw new InvalidOperationException($"Pricing analysis {command.Id} is not a PropertyGroup analysis");

        // Re-check the same four rules before transitioning Draft -> InProgress.
        var readiness = await readinessService.EvaluateByGroupIdAsync(
            pricingAnalysis.PropertyGroupId.Value, cancellationToken);

        if (readiness is null)
            throw new NotFoundException(
                $"Property group {pricingAnalysis.PropertyGroupId} was not found");

        if (!readiness.IsReady)
            throw new PricingAnalysisNotReadyException(readiness.Violations);

        pricingAnalysis.StartProgress();

        await pricingAnalysisRepository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new StartPricingAnalysisResult(pricingAnalysis.Id, pricingAnalysis.Status);
    }
}
