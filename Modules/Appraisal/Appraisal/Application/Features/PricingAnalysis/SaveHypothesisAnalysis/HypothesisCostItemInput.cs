using Appraisal.Domain.Appraisals.Hypothesis.CostItems;

namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

/// <summary>
/// Input DTO for a single cost item on the hypothesis analysis.
/// </summary>
public record HypothesisCostItemInput(
    Guid? Id,
    HypothesisCostCategory Category,
    string Description,
    int DisplaySequence,
    decimal Amount,
    decimal? RateAmount,
    decimal? Quantity,
    decimal? RatePercent,
    string? ModelName
);

/// <summary>
/// Input for Land &amp; Building summary fields (user-editable inputs only).
/// Computed fields are populated by the server.
/// </summary>
public record LandBuildingSummaryInput(
    decimal? C01TotalArea,
    decimal? C02SellingAreaPercent,
    decimal? C10PublicUtilityAreaPercent,
    int? C16EstSalesPeriod,
    decimal? C27PublicUtilityRatePerSqWa,
    decimal? C31LandFillingRatePerSqWa,
    decimal? C35ContingencyPercent,
    int? C40EstConstructionPeriod,
    decimal? C44AllocationPermitFee,
    decimal? C46LandTitleFeePerPlot,
    decimal? C50ProfessionalFeePerMonth,
    decimal? C54AdminCostPerMonth,
    decimal? C58SellingAdvPercent,
    decimal? C61ProjectContingencyPercent,
    decimal? C66TransferFeePercent,
    decimal? C69SpecificBizTaxPercent,
    decimal? C74RiskPremiumPercent,
    decimal? C78DiscountRate,
    string? Remark
);

/// <summary>
/// Input for Condominium summary fields (user-editable inputs only).
/// </summary>
public record CondominiumSummaryInput(
    decimal? E01AreaTitleDeed,
    decimal? E03FAR,
    decimal? E05TotalBuildingArea,
    int? E14EstSalesDurationMonths,
    decimal? E15CondoBuildingCostPerSqM,
    decimal? E20FurniturePerUnit,
    decimal? E23ExternalUtilities,
    decimal? E25HardCostContingencyPercent,
    int? E28EstConstructionPeriodMonths,
    decimal? E29ProfessionalFeePerMonth,
    decimal? E32AdminCostPerMonth,
    decimal? E35SellingAdvPercent,
    decimal? E37TitleDeedFee,
    decimal? E39EIACost,
    decimal? E41CondoRegistrationFee,
    decimal? E43OtherExpensesPercent,
    decimal? E46TransferFeePercent,
    decimal? E48SpecificBizTaxPercent,
    decimal? E51RiskProfitPercent,
    decimal? E55DiscountRate,
    string? Remark
);
