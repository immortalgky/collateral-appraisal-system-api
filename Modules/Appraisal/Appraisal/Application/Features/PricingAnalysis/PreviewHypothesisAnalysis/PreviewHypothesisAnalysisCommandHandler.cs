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
                C01TotalArea = input.C01TotalArea,
                C02SellingAreaPercent = input.C02SellingAreaPercent,
                C10PublicUtilityAreaPercent = input.C10PublicUtilityAreaPercent,
                C16EstSalesPeriod = input.C16EstSalesPeriod,
                C27PublicUtilityRatePerSqWa = input.C27PublicUtilityRatePerSqWa,
                C31LandFillingRatePerSqWa = input.C31LandFillingRatePerSqWa,
                C35ContingencyPercent = input.C35ContingencyPercent ?? 3m,
                C40EstConstructionPeriod = input.C40EstConstructionPeriod,
                C44AllocationPermitFee = input.C44AllocationPermitFee,
                C46LandTitleFeePerPlot = input.C46LandTitleFeePerPlot,
                C50ProfessionalFeePerMonth = input.C50ProfessionalFeePerMonth,
                C54AdminCostPerMonth = input.C54AdminCostPerMonth,
                C58SellingAdvPercent = input.C58SellingAdvPercent,
                C61ProjectContingencyPercent = input.C61ProjectContingencyPercent ?? 3m,
                C66TransferFeePercent = input.C66TransferFeePercent,
                C69SpecificBizTaxPercent = input.C69SpecificBizTaxPercent,
                C74RiskPremiumPercent = input.C74RiskPremiumPercent,
                C78DiscountRate = input.C78DiscountRate,
                Remark = input.Remark
            };

    private static CondominiumSummary MapCondominiumInput(CondominiumSummaryInput? input) =>
        input is null
            ? new CondominiumSummary()
            : new CondominiumSummary
            {
                E01AreaTitleDeed = input.E01AreaTitleDeed,
                E03FAR = input.E03FAR,
                E05TotalBuildingArea = input.E05TotalBuildingArea,
                E14EstSalesDurationMonths = input.E14EstSalesDurationMonths,
                E15CondoBuildingCostPerSqM = input.E15CondoBuildingCostPerSqM,
                E20FurniturePerUnit = input.E20FurniturePerUnit,
                E23ExternalUtilities = input.E23ExternalUtilities,
                E25HardCostContingencyPercent = input.E25HardCostContingencyPercent ?? 3m,
                E28EstConstructionPeriodMonths = input.E28EstConstructionPeriodMonths,
                E29ProfessionalFeePerMonth = input.E29ProfessionalFeePerMonth,
                E32AdminCostPerMonth = input.E32AdminCostPerMonth,
                E35SellingAdvPercent = input.E35SellingAdvPercent,
                E37TitleDeedFee = input.E37TitleDeedFee,
                E39EIACost = input.E39EIACost,
                E41CondoRegistrationFee = input.E41CondoRegistrationFee,
                E43OtherExpensesPercent = input.E43OtherExpensesPercent,
                E46TransferFeePercent = input.E46TransferFeePercent ?? 1m,
                E48SpecificBizTaxPercent = input.E48SpecificBizTaxPercent,
                E51RiskProfitPercent = input.E51RiskProfitPercent,
                E55DiscountRate = input.E55DiscountRate,
                Remark = input.Remark
            };
}
