namespace Appraisal.Application.Features.PricingAnalysis.SetManualCostBreakdown;

/// <summary>
/// Result of recording a hand-entered Cost-approach breakdown.
/// </summary>
public record SetManualCostBreakdownResult(
    Guid MethodId,
    Guid? FinalValueId,
    decimal? LandRatePerSqWa,
    decimal? LandArea,
    decimal? LandValue,
    decimal? BuildingValue,
    decimal ComputedTotal,
    decimal? AppraisalPrice,
    decimal? MethodValue,
    decimal? ApproachValue,
    decimal? FinalAppraisedValue
);
