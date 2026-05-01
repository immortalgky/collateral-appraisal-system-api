using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.CostItems;
using Appraisal.Domain.Appraisals.Hypothesis.Summaries;
using Appraisal.Domain.Services;
using Shared.CQRS;
using LandBuildingSummaryInput = Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis.LandBuildingSummaryInput;
using CondominiumSummaryInput = Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis.CondominiumSummaryInput;
using HypothesisCostItemInput = Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis.HypothesisCostItemInput;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewHypothesisAnalysis;

/// <summary>
/// Returns a full computed snapshot without persisting.
/// Loads the current analysis from DB to get unit rows,
/// builds a transient cost-item list from the command,
/// then passes both directly to the calculation service — no throwaway aggregate needed.
/// </summary>
public class PreviewHypothesisAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
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

        // Build a transient cost-item list from command inputs (no aggregate mutation)
        var transientCostItems = BuildTransientCostItems(analysis.Id, command.CostItems);

        if (analysis.Variant == HypothesisVariant.LandBuilding)
        {
            var inputSummary = MapLandBuildingInput(command.LandBuildingSummary);
            // Pass cost-item list directly — no throwaway aggregate
            var snapshot = _calcService.ComputeLandBuilding(
                transientCostItems, analysis.LandBuildingUnitRows, inputSummary);

            return new PreviewHypothesisAnalysisResult(
                analysis.Variant, snapshot.Summary, snapshot.Models, null);
        }
        else
        {
            var inputSummary = MapCondominiumInput(command.CondominiumSummary);
            var computedSummary = _calcService.ComputeCondominium(
                transientCostItems, analysis.CondominiumUnitRows, inputSummary);

            return new PreviewHypothesisAnalysisResult(
                analysis.Variant, null, null, computedSummary);
        }
    }

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
            result.Add(item);
        }
        return result;
    }

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
                SellingAdvPercent = input.SellingAdvPercent,                            // FSD C58
                ProjectContingencyPercent = input.ProjectContingencyPercent ?? 3m,      // FSD C61
                TransferFeePercent = input.TransferFeePercent,                          // FSD C66
                SpecificBizTaxPercent = input.SpecificBizTaxPercent,                    // FSD C69
                RiskPremiumPercent = input.RiskPremiumPercent,                          // FSD C74
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
                FurniturePerUnit = input.FurniturePerUnit,                              // FSD E20
                ExternalUtilities = input.ExternalUtilities,                            // FSD E23
                HardCostContingencyPercent = input.HardCostContingencyPercent ?? 3m,    // FSD E25
                EstConstructionPeriodMonths = input.EstConstructionPeriodMonths,        // FSD E28
                ProfessionalFeePerMonth = input.ProfessionalFeePerMonth,                // FSD E29
                AdminCostPerMonth = input.AdminCostPerMonth,                            // FSD E32
                SellingAdvPercent = input.SellingAdvPercent,                            // FSD E35
                TitleDeedFee = input.TitleDeedFee,                                      // FSD E37
                EIACost = input.EIACost,                                                // FSD E39
                CondoRegistrationFee = input.CondoRegistrationFee,                      // FSD E41
                OtherExpensesPercent = input.OtherExpensesPercent,                      // FSD E43
                TransferFeePercent = input.TransferFeePercent ?? 1m,                    // FSD E46
                SpecificBizTaxPercent = input.SpecificBizTaxPercent,                    // FSD E48
                RiskProfitPercent = input.RiskProfitPercent,                            // FSD E51
                DiscountRate = input.DiscountRate,                                      // FSD E55
                Remark = input.Remark
            };
}
