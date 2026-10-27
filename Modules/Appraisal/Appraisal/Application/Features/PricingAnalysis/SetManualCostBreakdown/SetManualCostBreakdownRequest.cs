namespace Appraisal.Application.Features.PricingAnalysis.SetManualCostBreakdown;

/// <summary>
/// Request to record a hand-entered Cost-approach breakdown.
/// Only the figures the appraiser actually types are accepted — everything else is derived.
/// <paramref name="BuildingValue"/> applies to a Cost method with Role = LandAndBuilding only.
/// </summary>
public record SetManualCostBreakdownRequest(
    decimal? LandRatePerSqWa = null,
    decimal? IndicatedValue = null,
    decimal? BuildingValue = null
);
