using MediatR;

namespace Parameter.Contracts.PricingParameters;

/// <summary>
/// Cross-module query: returns the seeded fire-insurance coverage rates used to derive the
/// recommended insurance coverage amount for Condo and LandAndBuilding collaterals.
/// Handler lives in Parameter module; Appraisal module dispatches via MediatR.
/// </summary>
public record GetFireInsuranceRatesQuery(string? PropertyKind = null) : IRequest<GetFireInsuranceRatesResult>;

public record GetFireInsuranceRatesResult(IReadOnlyList<FireInsuranceRateDto> Rates);

/// <summary>
/// A single fire-insurance rate row. <c>RatePerSqm</c> is in Baht per sq.m. of usable area.
/// <c>Condition</c> matches the value stored in appraisal.ProjectModels.FireInsuranceCondition.
/// </summary>
public record FireInsuranceRateDto(string Code, string Condition, string PropertyKind, decimal RatePerSqm, int DisplaySeq);
