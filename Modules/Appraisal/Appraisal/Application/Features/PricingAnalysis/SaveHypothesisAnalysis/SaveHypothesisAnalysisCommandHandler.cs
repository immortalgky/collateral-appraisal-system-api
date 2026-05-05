using Appraisal.Application.Configurations;
using Appraisal.Application.Services;
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
///
/// Cost-item upsert semantics (mirror IncomeAnalysis):
///   - Incoming row with Id that matches an existing row → UPDATE in place (stable Id).
///   - Incoming row with Id not found (or null) → INSERT with Guid.CreateVersion7().
///   - Existing rows whose Id is absent from the incoming set → DELETE.
/// </summary>
public class SaveHypothesisAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IAppraisalUnitOfWork unitOfWork,
    PricingPropertyDataService propertyDataService
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

        // ── Fetch system land area from titles (PropertyGroup only) ───────
        decimal? totalLandAreaFromTitles = null;
        if (pricingAnalysis.PropertyGroupId.HasValue)
            totalLandAreaFromTitles = await propertyDataService.GetTotalLandAreaFromTitlesAsync(
                pricingAnalysis.PropertyGroupId.Value, cancellationToken);

        // ── Selective upsert cost items ───────────────────────────────────
        SyncCostItems(analysis, command.CostItems);

        // ── Compute and persist ───────────────────────────────────────────
        if (analysis.Variant == HypothesisVariant.LandBuilding)
        {
            var inputSummary = MapLandBuildingInput(command.LandBuildingSummary);
            var snapshot = _calcService.ComputeLandBuilding(
                analysis, analysis.LandBuildingUnitRows, inputSummary, totalLandAreaFromTitles);

            analysis.UpdateLandBuildingSummary(snapshot.Summary);

            // Set method value from FSD C81 (TotalAssetValueRounded)
            var finalValue = snapshot.Summary.TotalAssetValueRounded ?? 0m;
            method.SetValue(finalValue);
            PropagateValue(pricingAnalysis, method, finalValue);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new SaveHypothesisAnalysisResult(
                analysis.Id, analysis.Variant, snapshot.Summary, null, totalLandAreaFromTitles);
        }
        else
        {
            var inputSummary = MapCondominiumInput(command.CondominiumSummary);
            var computedSummary = _calcService.ComputeCondominium(
                analysis, analysis.CondominiumUnitRows, inputSummary, totalLandAreaFromTitles);

            analysis.UpdateCondominiumSummary(computedSummary);

            // Set method value from FSD E58 (TotalAssetValueRounded)
            var finalValue = computedSummary.TotalAssetValueRounded ?? 0m;
            method.SetValue(finalValue);
            PropagateValue(pricingAnalysis, method, finalValue);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new SaveHypothesisAnalysisResult(
                analysis.Id, analysis.Variant, null, computedSummary, totalLandAreaFromTitles);
        }
    }

    // ── Selective upsert: UPDATE existing, INSERT new, DELETE orphans ─────
    // clientId (input.Id) == entity.Id for rows the frontend received from prior save.
    // New rows carry a fresh frontend-generated Guid that won't match any existing Id.
    private static void SyncCostItems(
        HypothesisAnalysis analysis,
        IReadOnlyList<HypothesisCostItemInput> inputs)
    {
        var existingById = analysis.CostItems.ToDictionary(i => i.Id);
        var processedIds = new HashSet<Guid>();

        foreach (var input in inputs)
        {
            if (input.Id.HasValue && existingById.TryGetValue(input.Id.Value, out var existing))
            {
                // UPDATE in place — Id stays stable
                existing.UpdateDescription(input.Description);
                existing.UpdateSequence(input.DisplaySequence);
                existing.SetAmounts(input.Amount, input.RateAmount, input.Quantity, input.RatePercent);
                if (input.Category == HypothesisCostCategory.CostOfBuilding)
                    existing.SetBuildingCostInputs(
                        input.Area, input.PricePerSqM, input.Year, input.AnnualDepreciationPercent,
                        input.IsBuilding, input.DepreciationMethod,
                        MapPeriods(input.DepreciationPeriods));
                processedIds.Add(input.Id.Value);
            }
            else
            {
                // INSERT new row
                var newItem = analysis.AddCostItem(
                    input.Category,
                    input.Kind,
                    input.Description,
                    input.DisplaySequence,
                    input.ModelName);
                newItem.SetAmounts(input.Amount, input.RateAmount, input.Quantity, input.RatePercent);
                if (input.Category == HypothesisCostCategory.CostOfBuilding)
                    newItem.SetBuildingCostInputs(
                        input.Area, input.PricePerSqM, input.Year, input.AnnualDepreciationPercent,
                        input.IsBuilding, input.DepreciationMethod,
                        MapPeriods(input.DepreciationPeriods));
            }
        }

        // DELETE rows that were not in the incoming set
        foreach (var (id, _) in existingById)
        {
            if (!processedIds.Contains(id))
                analysis.RemoveCostItem(id);
        }
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

    private static IReadOnlyList<(int, int, decimal)>? MapPeriods(IReadOnlyList<DepreciationPeriodInput>? inputs)
        => inputs is null || inputs.Count == 0
            ? null
            : inputs.Select(p => (p.AtYear, p.ToYear, p.DepreciationPerYear)).ToList();

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
