using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetComparativeFactors;

/// <summary>
/// Query to get comparative factors for a pricing method with their current selections and values
/// </summary>
public record GetComparativeFactorsQuery(
    Guid PricingAnalysisId,
    Guid MethodId
) : IQuery<GetComparativeFactorsResult>;
