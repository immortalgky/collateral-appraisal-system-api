using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.ValidateGroupForPricing;

/// <summary>
/// Query that runs the pre-flight data-readiness checks for a property group before
/// the user is allowed to open Pricing Analysis. Read-only — performs no mutation.
/// </summary>
public record ValidateGroupForPricingQuery(
    Guid PropertyGroupId
) : IQuery<ValidateGroupForPricingResult>;
