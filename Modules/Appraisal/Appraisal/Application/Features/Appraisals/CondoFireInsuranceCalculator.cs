using Appraisal.Application.Features.FireInsuranceRates.GetFireInsuranceRates;

namespace Appraisal.Application.Features.Appraisals;

/// <summary>
/// Derives a condo unit's fire-insurance coverage amount from the appraisal.FireInsuranceRates table.
/// Shared by CreateCondoProperty and UpdateCondoProperty — BuildingInsurancePrice is never
/// accepted from the client; it is always RatePerSqm × UsableArea for the selected rate code.
/// </summary>
internal static class CondoFireInsuranceCalculator
{
    internal static async Task<decimal?> DeriveBuildingInsurancePriceAsync(
        ISender mediator,
        string? fireInsuranceCode,
        decimal? usableArea,
        CancellationToken cancellationToken)
    {
        // No condition selected, or the condition doesn't match a seeded rate: the price is
        // undetermined (null), NOT zero — "not chosen yet" must stay distinguishable from
        // "genuinely zero" since this is a money field.
        if (string.IsNullOrEmpty(fireInsuranceCode))
            return null;

        var ratesResult = await mediator.Send(
            new GetFireInsuranceRatesQuery(PropertyKind: "Condo"), cancellationToken);

        var rate = ratesResult.Rates.FirstOrDefault(
            r => string.Equals(r.Code, fireInsuranceCode, StringComparison.Ordinal));

        // Once a condition is matched, a missing UsableArea is treated as 0 — a determinate value.
        // Coverage is quoted in whole thousands, so the stored figure is rounded to the nearest 1,000
        // (half away from zero, matching SQL ROUND(x, -3) in BuildingInsuranceCalculator).
        return rate is null
            ? null
            : Math.Round(rate.RatePerSqm * (usableArea ?? 0m) / 1000m, MidpointRounding.AwayFromZero) * 1000m;
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
            r => string.Equals(r.Code, condition, StringComparison.Ordinal));
    }

    /// <summary>
    /// Membership check across EVERY property kind, for the project-model commands: a block model is
    /// a Condo model or a LandAndBuilding one depending on the project it hangs off, and the command
    /// does not carry the project type, so the kind-filtered check above cannot be reused.
    /// </summary>
    internal static async Task<bool> IsKnownRateCodeAsync(
        ISender mediator,
        string code,
        CancellationToken cancellationToken)
    {
        var ratesResult = await mediator.Send(new GetFireInsuranceRatesQuery(), cancellationToken);

        return ratesResult.Rates.Any(r => string.Equals(r.Code, code, StringComparison.Ordinal));
    }
}
