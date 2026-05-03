using Appraisal.Domain.Appraisals.Hypothesis.CostItems;

namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

/// <summary>
/// Input DTO for a single cost item on the hypothesis analysis.
/// For CostOfBuilding rows, B01/B02/B04/B05 are user inputs;
/// B03/B06/B07/B08 are computed server-side and returned in the response.
/// </summary>
public record HypothesisCostItemInput(
    Guid? Id,
    HypothesisCostCategory Category,
    CostItemKind Kind,
    string Description,
    int DisplaySequence,
    decimal Amount,
    decimal? RateAmount,
    decimal? Quantity,
    decimal? RatePercent,
    string? ModelName,
    // ── CostOfBuilding categorisation ────────────────────────────────────
    bool IsBuilding,
    string DepreciationMethod,
    // ── CostOfBuilding B-field inputs (FSD B01/B02/B04/B05) ─────────────
    decimal? Area,
    decimal? PricePerSqM,
    int? Year,
    decimal? AnnualDepreciationPercent,
    // ── Period depreciation rows (used when DepreciationMethod == "Period") ─
    IReadOnlyList<DepreciationPeriodInput>? DepreciationPeriods
);

/// <summary>
/// A single depreciation period input row.
/// </summary>
public record DepreciationPeriodInput(
    int AtYear,
    int ToYear,
    decimal DepreciationPerYear
);

/// <summary>
/// Input for Land &amp; Building summary fields (user-editable inputs only).
/// Computed fields are populated by the server.
/// FSD field codes are in XML doc comments on <see cref="Appraisal.Domain.Appraisals.Hypothesis.Summaries.LandBuildingSummary"/>.
/// </summary>
public record LandBuildingSummaryInput(
    decimal? TotalArea,                      // FSD C01
    decimal? SellingAreaPercent,             // FSD C02
    decimal? PublicUtilityAreaPercent,       // FSD C10
    int? EstSalesPeriod,                     // FSD C16
    decimal? PublicUtilityRatePerSqWa,       // FSD C27
    decimal? LandFillingRatePerSqWa,         // FSD C31
    decimal? ContingencyPercent,             // FSD C35
    int? EstConstructionPeriod,              // FSD C40
    decimal? AllocationPermitFee,            // FSD C44
    decimal? LandTitleFeePerPlot,            // FSD C46
    decimal? ProfessionalFeePerMonth,        // FSD C50
    decimal? AdminCostPerMonth,              // FSD C54
    decimal? SellingAdvPercent,              // FSD C58
    decimal? ProjectContingencyPercent,      // FSD C61
    decimal? TransferFeePercent,             // FSD C66
    decimal? SpecificBizTaxPercent,          // FSD C69
    decimal? RiskPremiumPercent,             // FSD C74
    decimal? DiscountRate,                   // FSD C78
    string? Remark
);

/// <summary>
/// Input for Condominium summary fields (user-editable inputs only).
/// FSD field codes are in XML doc comments on <see cref="Appraisal.Domain.Appraisals.Hypothesis.Summaries.CondominiumSummary"/>.
/// </summary>
public record CondominiumSummaryInput(
    decimal? AreaTitleDeed,                  // FSD E01
    decimal? FAR,                            // FSD E03
    decimal? TotalBuildingArea,              // FSD E05
    int? EstSalesDurationMonths,             // FSD E14
    decimal? CondoBuildingCostPerSqM,        // FSD E15
    int? SetAvgRoomSizeUnits,                // FSD E18 (override unit count from upload)
    decimal? FurniturePerUnit,               // FSD E20
    decimal? ExternalUtilities,              // FSD E23
    decimal? HardCostContingencyPercent,     // FSD E25
    int? EstConstructionPeriodMonths,        // FSD E28
    decimal? ProfessionalFeePerMonth,        // FSD E29
    decimal? AdminCostPerMonth,              // FSD E32
    decimal? SellingAdvPercent,              // FSD E35
    decimal? TitleDeedFee,                   // FSD E37
    decimal? EIACost,                        // FSD E39
    decimal? CondoRegistrationFee,           // FSD E41
    decimal? OtherExpensesPercent,           // FSD E43
    decimal? TransferFeePercent,             // FSD E46
    decimal? SpecificBizTaxPercent,          // FSD E48
    decimal? RiskProfitPercent,              // FSD E51
    decimal? DiscountRate,                   // FSD E55
    string? Remark
);
