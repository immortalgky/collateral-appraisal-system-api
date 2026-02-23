using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateFactorScore;

/// <summary>
/// Handler for updating a factor score.
/// Factor scores are now at the method level.
/// </summary>
public class UpdateFactorScoreCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<UpdateFactorScoreCommand, UpdateFactorScoreResult>
{
    public async Task<UpdateFactorScoreResult> Handle(
        UpdateFactorScoreCommand command,
        CancellationToken cancellationToken)
    {
        // Load pricing analysis aggregate
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new InvalidOperationException($"PricingAnalysis {command.PricingAnalysisId} not found");

        // Navigate to the factor score (now at method level)
        var factorScore = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .SelectMany(m => m.FactorScores)
            .FirstOrDefault(fs => fs.Id == command.FactorScoreId);

        if (factorScore is null)
            throw new InvalidOperationException($"FactorScore {command.FactorScoreId} not found");

        // Update values if provided (unified model)
        if (command.SubjectValue is not null || command.SubjectScore.HasValue)
            factorScore.SetValues(command.SubjectValue, command.SubjectScore);

        // Update weight if provided
        if (command.FactorWeight.HasValue)
            factorScore.UpdateWeight(command.FactorWeight.Value);

        // Update adjustment if provided
        if (command.AdjustmentPct.HasValue || command.Remarks is not null)
            factorScore.SetAdjustment(command.AdjustmentPct, command.Remarks);

        // Repository saves via EF change tracking
        await pricingAnalysisRepository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new UpdateFactorScoreResult(
            factorScore.Id,
            factorScore.FactorId,
            factorScore.FactorWeight,
            factorScore.Value,
            factorScore.Score,
            null, // ComparableValue - deprecated
            null, // ComparableScore - deprecated
            null, // ScoreDifference - deprecated
            factorScore.WeightedScore,
            factorScore.AdjustmentPct
        );
    }
}
