using Parameter.Contracts.PricingParameters;

namespace Appraisal.Application.Features.Appraisals;

/// <summary>
/// Derives a condo unit's fire-insurance coverage amount from the Parameter module's rate table.
/// Shared by CreateCondoProperty and UpdateCondoProperty — BuildingInsurancePrice is never
/// accepted from the client; it is always RatePerSqm × UsableArea for the selected condition.
/// </summary>
internal static class CondoFireInsuranceCalculator
{
    internal static async Task<decimal?> DeriveBuildingInsurancePriceAsync(
        ISender mediator,
        string? fireInsuranceCondition,
        decimal? usableArea,
        CancellationToken cancellationToken)
    {
        // No condition selected, or the condition doesn't match a seeded rate: the price is
        // undetermined (null), NOT zero — "not chosen yet" must stay distinguishable from
        // "genuinely zero" since this is a money field.
        if (string.IsNullOrEmpty(fireInsuranceCondition))
            return null;

        var ratesResult = await mediator.Send(
            new GetFireInsuranceRatesQuery(PropertyKind: "Condo"), cancellationToken);

        var rate = ratesResult.Rates.FirstOrDefault(
            r => string.Equals(r.Condition, fireInsuranceCondition, StringComparison.Ordinal));

        // Once a condition is matched, a missing UsableArea is treated as 0 — a determinate value.
        return rate is null ? null : rate.RatePerSqm * (usableArea ?? 0m);
    }

    /// <summary>Used by the command validators to reject a condition absent from the seeded Condo rate set.</summary>
    internal static async Task<bool> IsKnownConditionAsync(
        ISender mediator,
        string condition,
        CancellationToken cancellationToken)
    {
        var ratesResult = await mediator.Send(
            new GetFireInsuranceRatesQuery(PropertyKind: "Condo"), cancellationToken);

        return ratesResult.Rates.Any(
            r => string.Equals(r.Condition, condition, StringComparison.Ordinal));
    }
}
