using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SaveComparativeAnalysis;

/// <summary>
/// Command to save the entire comparative analysis screen in a single transaction.
/// Includes Step 1 (factor selection) and Step 2 (factor scoring) data.
/// </summary>
public record SaveComparativeAnalysisCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    IReadOnlyList<ComparativeFactorInput> ComparativeFactors,
    IReadOnlyList<FactorScoreInput> FactorScores,
    IReadOnlyList<CalculationInput> Calculations
) : ICommand<SaveComparativeAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
