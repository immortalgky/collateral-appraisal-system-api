using Appraisal.Application.Features.PricingAnalysis.GetHypothesisAnalysis;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.CostItems;
using Appraisal.Domain.Appraisals.Hypothesis.Summaries;
using Appraisal.Domain.Services;
using Shared.CQRS;
using LandBuildingSummaryInput = Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis.LandBuildingSummaryInput;
using CondominiumSummaryInput = Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis.CondominiumSummaryInput;
using HypothesisCostItemInput = Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis.HypothesisCostItemInput;
using DepreciationPeriodInput = Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis.DepreciationPeriodInput;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewHypothesisAnalysis;

/// <summary>
/// Returns a full computed snapshot without persisting.
/// Loads the current analysis from DB to get unit rows,
/// builds a transient cost-item list from the command,
/// then passes both directly to the calculation service — no throwaway aggregate needed.
/// </summary>
public class PreviewHypothesisAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingPropertyDataService propertyDataService
) : IQueryHandler<PreviewHypothesisAnalysisCommand, PreviewHypothesisAnalysisResult>
{
    private readonly HypothesisCalculationService _calcService = new();

    public async Task<PreviewHypothesisAnalysisResult> Handle(
        PreviewHypothesisAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
                                 command.PricingAnalysisId, cancellationToken)
                             ?? throw new InvalidOperationException(
                                 $"PricingAnalysis {command.PricingAnalysisId} not found");

        var method = pricingAnalysis.Approaches
                         .SelectMany(a => a.Methods)
                         .FirstOrDefault(m => m.Id == command.MethodId)
                     ?? throw new InvalidOperationException(
                         $"PricingAnalysisMethod {command.MethodId} not found");

        var analysis = method.HypothesisAnalysis
                       ?? throw new InvalidOperationException("Hypothesis analysis not found.");

        // ── Fetch system land area from titles (PropertyGroup only) ───────
        decimal? totalLandAreaFromTitles = null;
        if (pricingAnalysis.PropertyGroupId.HasValue)
            totalLandAreaFromTitles = await propertyDataService.GetTotalLandAreaFromTitlesAsync(
                pricingAnalysis.PropertyGroupId.Value, cancellationToken);

        // Build a transient cost-item list from command inputs (no aggregate mutation)
        var transientCostItems = BuildTransientCostItems(analysis.Id, command.CostItems);

        if (analysis.Variant == HypothesisVariant.LandBuilding)
        {
            var inputSummary = MapLandBuildingInput(command.LandBuildingSummary);
            // Pass cost-item list directly — no throwaway aggregate.
            // ComputeLandBuilding also calls ComputeBuildingDepreciation internally,
            // which populates B03/B06/B07/B08 on each transient item.
            var snapshot = _calcService.ComputeLandBuilding(
                transientCostItems, analysis.LandBuildingUnitRows, inputSummary, totalLandAreaFromTitles);

            var costItemDtos = MapCostItemDtos(transientCostItems);

            return new PreviewHypothesisAnalysisResult(
                analysis.Variant, snapshot.Summary, snapshot.Models, null, costItemDtos,
                totalLandAreaFromTitles);
        }
        else
        {
            var inputSummary = MapCondominiumInput(command.CondominiumSummary);
            var computedSummary = _calcService.ComputeCondominium(
                transientCostItems, analysis.CondominiumUnitRows, inputSummary, totalLandAreaFromTitles);

            return new PreviewHypothesisAnalysisResult(
                analysis.Variant, null, null, computedSummary, null, totalLandAreaFromTitles);
        }
    }

    private static IReadOnlyList<CostItemDto> MapCostItemDtos(IReadOnlyList<HypothesisCostItem> items)
        => items
            .OrderBy(i => i.Category)
            .ThenBy(i => i.DisplaySequence)
            .Select(i => new CostItemDto(
                i.Id, i.Category, i.Kind, i.Description, i.DisplaySequence,
                i.Amount, i.RateAmount, i.Quantity, i.RatePercent, i.CategoryRatio, i.ModelName,
                i.IsBuilding, i.DepreciationMethod,
                i.Area, i.PricePerSqM, i.PriceBeforeDepreciation,
                i.Year, i.AnnualDepreciationPercent,
                i.TotalDepreciationPercent, i.DepreciationAmount, i.ValueAfterDepreciation,
                i.DepreciationPeriods
                    .OrderBy(p => p.Sequence)
                    .Select(p => new DepreciationPeriodDto(p.Id, p.Sequence, p.AtYear, p.ToYear, p.DepreciationPerYear))
                    .ToList()))
            .ToList();

    private static IReadOnlyList<HypothesisCostItem> BuildTransientCostItems(
        Guid analysisId,
        IReadOnlyList<HypothesisCostItemInput> inputs)
    {
        var result = new List<HypothesisCostItem>(inputs.Count);
        foreach (var i in inputs)
        {
            var item = HypothesisCostItem.Create(
                analysisId, i.Category, i.Kind, i.Description, i.DisplaySequence, i.ModelName);
            item.SetAmounts(i.Amount, i.RateAmount, i.Quantity, i.RatePercent);
            if (i.Category == HypothesisCostCategory.CostOfBuilding)
                item.SetBuildingCostInputs(
                    i.Area, i.PricePerSqM, i.Year, i.AnnualDepreciationPercent,
                    i.IsBuilding, i.DepreciationMethod,
                    MapPeriods(i.DepreciationPeriods));
            result.Add(item);
        }
        return result;
    }

    private static IReadOnlyList<(int, int, decimal)>? MapPeriods(IReadOnlyList<DepreciationPeriodInput>? inputs)
        => inputs is null || inputs.Count == 0
            ? null
            : inputs.Select(p => (p.AtYear, p.ToYear, p.DepreciationPerYear)).ToList();

    private static LandBuildingSummary MapLandBuildingInput(LandBuildingSummaryInput? input) =>
        input is null
            ? new LandBuildingSummary()
            : new LandBuildingSummary
            {
                TotalArea = input.TotalArea,                                            // FSD C01
                SellingAreaPercent = input.SellingAreaPercent,                          // FSD C02
                PublicUtilityAreaPercent = input.PublicUtilityAreaPercent,              // FSD C10
                EstSalesPeriod = input.EstSalesPeriod,                                  // FSD C16
                PublicUtilityRatePerSqWa = input.PublicUtilityRatePerSqWa,              // FSD C27
                LandFillingRatePerSqWa = input.LandFillingRatePerSqWa,                  // FSD C31
                ContingencyPercent = input.ContingencyPercent ?? 3m,                    // FSD C35
                EstConstructionPeriod = input.EstConstructionPeriod,                    // FSD C40
                AllocationPermitFee = input.AllocationPermitFee,                        // FSD C44
                LandTitleFeePerPlot = input.LandTitleFeePerPlot,                        // FSD C46
                ProfessionalFeePerMonth = input.ProfessionalFeePerMonth,                // FSD C50
                AdminCostPerMonth = input.AdminCostPerMonth,                            // FSD C54
                SellingAdvPercent = input.SellingAdvPercent ?? 3m,                      // FSD C58 — soft default 3%
                ProjectContingencyPercent = input.ProjectContingencyPercent ?? 3m,      // FSD C61
                TransferFeePercent = input.TransferFeePercent ?? 1m,                    // FSD C66 — soft default 1%
                SpecificBizTaxPercent = input.SpecificBizTaxPercent ?? 3.30m,           // FSD C69 — soft default 3.30%
                RiskPremiumPercent = input.RiskPremiumPercent ?? 30m,                   // FSD C74 — soft default 30%
                DiscountRate = input.DiscountRate,                                      // FSD C78
                Remark = input.Remark
            };

    private static CondominiumSummary MapCondominiumInput(CondominiumSummaryInput? input) =>
        input is null
            ? new CondominiumSummary()
            : new CondominiumSummary
            {
                AreaTitleDeed = input.AreaTitleDeed,                                    // FSD E01
                FAR = input.FAR,                                                        // FSD E03
                TotalBuildingArea = input.TotalBuildingArea,                            // FSD E05
                EstSalesDurationMonths = input.EstSalesDurationMonths,                  // FSD E14
                CondoBuildingCostPerSqM = input.CondoBuildingCostPerSqM,                // FSD E15
                SetAvgRoomSizeUnits = input.SetAvgRoomSizeUnits,                        // FSD E18
                FurniturePerUnit = input.FurniturePerUnit,                              // FSD E20
                ExternalUtilities = input.ExternalUtilities,                            // FSD E23
                HardCostContingencyPercent = input.HardCostContingencyPercent ?? 3m,    // FSD E25
                EstConstructionPeriodMonths = input.EstConstructionPeriodMonths,        // FSD E28
                ProfessionalFeePerMonth = input.ProfessionalFeePerMonth,                // FSD E29
                AdminCostPerMonth = input.AdminCostPerMonth,                            // FSD E32
                SellingAdvPercent = input.SellingAdvPercent ?? 3m,                      // FSD E35 — soft default 3%
                TitleDeedFee = input.TitleDeedFee,                                      // FSD E37
                EIACost = input.EIACost,                                                // FSD E39
                CondoRegistrationFee = input.CondoRegistrationFee,                      // FSD E41
                OtherExpensesPercent = input.OtherExpensesPercent ?? 3m,                // FSD E43 — soft default 3%
                TransferFeePercent = input.TransferFeePercent ?? 1m,                    // FSD E46 — soft default 1%
                SpecificBizTaxPercent = input.SpecificBizTaxPercent ?? 3.30m,           // FSD E48 — soft default 3.30%
                RiskProfitPercent = input.RiskProfitPercent ?? 30m,                     // FSD E51 — soft default 30%
                DiscountRate = input.DiscountRate,                                      // FSD E55
                Remark = input.Remark
            };
}
