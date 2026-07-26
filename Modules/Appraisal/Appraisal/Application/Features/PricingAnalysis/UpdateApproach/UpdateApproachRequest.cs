namespace Appraisal.Application.Features.PricingAnalysis.UpdateApproach;

/// <summary>
/// Weight only. An approach's VALUE is never client-supplied — it is always derived from that
/// approach's selected method (see PricingAnalysisApproach.SelectMethod /
/// SyncValueFromSelectedMethod). Accepting an ApproachValue here was the one path in the system
/// able to put a figure on an approach that no method produced, which any later method-level save
/// would then silently re-derive away.
/// </summary>
public record UpdateApproachRequest(
    decimal? Weight = null
);
