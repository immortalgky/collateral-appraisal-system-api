namespace Appraisal.Application.Features.PricingAnalysis.SetManualCostBreakdown;

/// <summary>
/// Response for recording a hand-entered Cost-approach breakdown.
/// </summary>
public record SetManualCostBreakdownResponse(
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
