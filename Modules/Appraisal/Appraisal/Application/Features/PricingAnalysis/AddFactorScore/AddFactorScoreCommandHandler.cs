using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.AddFactorScore;

/// <summary>
/// Handler for adding a factor score to a pricing method.
/// Factor scores are now at the method level with MarketComparableId to distinguish per-comparable scores.
/// </summary>
public class AddFactorScoreCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<AddFactorScoreCommand, AddFactorScoreResult>
{
    public async Task<AddFactorScoreResult> Handle(
        AddFactorScoreCommand command,
        CancellationToken cancellationToken)
    {
        // Load pricing analysis aggregate
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new InvalidOperationException($"PricingAnalysis {command.PricingAnalysisId} not found");

        // Navigate to the method (factor scores are now at method level, not calculation)
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Calculations.Any(c => c.Id == command.PricingCalculationId));

        if (method is null)
            throw new InvalidOperationException($"PricingCalculation {command.PricingCalculationId} not found");

        // Get the calculation to find the market comparable ID
        var calculation = method.Calculations.First(c => c.Id == command.PricingCalculationId);

        // Add factor score using domain method at method level with market comparable ID
        var factorScore = method.AddFactorScore(
            command.FactorId,
            command.FactorWeight,
            method.FactorScores.Count + 1,
            calculation.MarketComparableId);

        // Set values if provided (unified model now)
        if (command.SubjectValue is not null || command.SubjectScore.HasValue)
            factorScore.SetValues(command.SubjectValue, command.SubjectScore);

        // Set adjustment if provided
        if (command.AdjustmentPct.HasValue)
            factorScore.SetAdjustment(command.AdjustmentPct, command.Remarks);

        // Repository saves via EF change tracking
        await pricingAnalysisRepository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new AddFactorScoreResult(
            factorScore.Id,
            factorScore.FactorId,
            factorScore.FactorWeight,
            factorScore.Value,
            factorScore.Score,
            null, // ComparableValue - deprecated in new model
            null, // ComparableScore - deprecated in new model
            null, // ScoreDifference - deprecated in new model
            factorScore.WeightedScore,
            factorScore.AdjustmentPct,
            factorScore.DisplaySequence
        );
    }
}
