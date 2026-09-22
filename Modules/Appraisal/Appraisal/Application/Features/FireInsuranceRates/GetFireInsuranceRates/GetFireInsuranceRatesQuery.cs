namespace Appraisal.Application.Features.FireInsuranceRates.GetFireInsuranceRates;

/// <summary>
/// Returns the seeded fire-insurance coverage rates used to derive the recommended insurance
/// coverage for Condo and LandAndBuilding collaterals. Omit <paramref name="PropertyKind"/> to get
/// every kind — the block unit-price calculation needs both in one dictionary.
/// </summary>
public record GetFireInsuranceRatesQuery(string? PropertyKind = null)
    : IQuery<GetFireInsuranceRatesResult>;

public record GetFireInsuranceRatesResult(IReadOnlyList<FireInsuranceRateDto> Rates);

/// <summary>
/// One rate row. <c>RatePerSqm</c> is Baht per sq.m. of usable area. <c>Code</c> is what the
/// appraisal and project-model records store; <c>Condition</c> is the readable name of the same row.
/// </summary>
public record FireInsuranceRateDto(
    string Code,
    string Condition,
    string PropertyKind,
    decimal RatePerSqm,
    int DisplaySeq);
