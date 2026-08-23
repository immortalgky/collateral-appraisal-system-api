namespace Appraisal.Application.Features.PricingAnalysis.SetManualCostBreakdown;

/// <summary>
/// Request to record a hand-entered Cost-approach breakdown.
/// Only the two figures the appraiser actually types are accepted — everything else is derived.
/// </summary>
public record SetManualCostBreakdownRequest(
    decimal? LandRatePerSqWa = null,
    decimal? AppraisalPrice = null
);
