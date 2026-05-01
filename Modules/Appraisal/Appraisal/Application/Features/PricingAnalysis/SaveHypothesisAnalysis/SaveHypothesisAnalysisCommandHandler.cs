using Appraisal.Application.Configurations;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Hypothesis.CostItems;
using Appraisal.Domain.Appraisals.Hypothesis.Summaries;
using Appraisal.Domain.Services;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

/// <summary>
/// Persists user inputs, runs the server-side calculation, and stores computed outputs.
/// Backend is the sole source of truth for all computed fields.
/// </summary>
public class SaveHypothesisAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<SaveHypothesisAnalysisCommand, SaveHypothesisAnalysisResult>
{
    private readonly HypothesisCalculationService _calcService = new();

    public async Task<SaveHypothesisAnalysisResult> Handle(
        SaveHypothesisAnalysisCommand command,
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

        method.SetRemark(command.Remark);

        // ── Upsert cost items ─────────────────────────────────────────────
        analysis.ClearAllCostItems();
        foreach (var item in command.CostItems)
        {
            var costItem = analysis.AddCostItem(
                item.Category, item.Description, item.DisplaySequence, item.ModelName);
            costItem.SetAmounts(item.Amount, item.RateAmount, item.Quantity, item.RatePercent);
        }

        // ── Compute and persist ───────────────────────────────────────────
        if (analysis.Variant == HypothesisVariant.LandBuilding)
        {
            var inputSummary = MapLandBuildingInput(command.LandBuildingSummary);
            var snapshot = _calcService.ComputeLandBuilding(
                analysis, analysis.LandBuildingUnitRows, inputSummary);

            analysis.UpdateLandBuildingSummary(snapshot.Summary);

            // Set method value from C81
            var finalValue = snapshot.Summary.C81TotalAssetValueRounded ?? 0m;
            method.SetValue(finalValue);
            PropagateValue(pricingAnalysis, method, finalValue);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new SaveHypothesisAnalysisResult(
                analysis.Id, analysis.Variant, snapshot.Summary, null);
        }
        else
        {
            var inputSummary = MapCondominiumInput(command.CondominiumSummary);
            var computedSummary = _calcService.ComputeCondominium(
                analysis, analysis.CondominiumUnitRows, inputSummary);

            analysis.UpdateCondominiumSummary(computedSummary);

            var finalValue = computedSummary.E58TotalAssetValueRounded ?? 0m;
            method.SetValue(finalValue);
            PropagateValue(pricingAnalysis, method, finalValue);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new SaveHypothesisAnalysisResult(
                analysis.Id, analysis.Variant, null, computedSummary);
        }
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

    private static void PropagateValue(Domain.Appraisals.PricingAnalysis pricingAnalysis, PricingAnalysisMethod method, decimal value)
    {
        if (!method.IsSelected || !(method.MethodValue > 0)) return;

        var parentApproach = pricingAnalysis.Approaches
            .FirstOrDefault(a => a.Methods.Any(m => m.Id == method.Id));
        if (parentApproach is null) return;

        parentApproach.SetValue(value);
        if (parentApproach.IsSelected)
            pricingAnalysis.SetFinalValues(value);
    }
}
