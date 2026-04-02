using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SaveMachineCostItems;

/// <summary>
/// Bulk upsert handler for machine cost items.
/// Uses ID-based upsert: existing items are updated, new items are created, missing items are deleted.
/// </summary>
public class SaveMachineCostItemsCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingCalculationServiceResolver calculationServiceResolver
) : ICommandHandler<SaveMachineCostItemsCommand, SaveMachineCostItemsResult>
{
    public async Task<SaveMachineCostItemsResult> Handle(
        SaveMachineCostItemsCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId, cancellationToken)
            ?? throw new InvalidOperationException($"PricingAnalysis {command.PricingAnalysisId} not found");

        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == command.MethodId)
            ?? throw new InvalidOperationException($"PricingAnalysisMethod {command.MethodId} not found");

        // Save remark
        method.SetRemark(command.Remark);

        // Upsert machine cost items
        UpsertMachineCostItems(method, command.Items);

        // Recalculate totals
        var calculationService = calculationServiceResolver.Resolve(method.MethodType);
        calculationService?.Recalculate(method);

        // Propagate value up if method is selected
        if (method.IsSelected && method.MethodValue.HasValue)
        {
            var parentApproach = pricingAnalysis.Approaches
                .First(a => a.Methods.Any(m => m.Id == method.Id));

            parentApproach.SetValue(method.MethodValue.Value);

            if (parentApproach.IsSelected)
                pricingAnalysis.SetFinalValues(parentApproach.ApproachValue!.Value);
        }

        var totalFmv = method.MachineCostItems.Sum(i => i.FairMarketValue ?? 0);

        return new SaveMachineCostItemsResult(
            command.PricingAnalysisId,
            command.MethodId,
            method.MachineCostItems.Count,
            totalFmv
        );
    }

    private static void UpsertMachineCostItems(
        PricingAnalysisMethod method,
        IReadOnlyList<MachineCostItemInput> inputs)
    {
        // Get IDs from request (only existing items have IDs)
        var inputIds = inputs
            .Where(i => i.Id.HasValue)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        // Get existing IDs
        var existingIds = method.MachineCostItems
            .Select(i => i.Id)
            .ToHashSet();

        // Delete items not in request
        var idsToDelete = existingIds.Except(inputIds).ToList();
        foreach (var id in idsToDelete) method.RemoveMachineCostItem(id);

        // Update or create items
        foreach (var input in inputs)
        {
            if (input.Id.HasValue)
            {
                // Update existing
                var existing = method.MachineCostItems.FirstOrDefault(i => i.Id == input.Id.Value);
                if (existing is not null)
                {
                    existing.Update(
                        input.RcnReplacementCost,
                        input.LifeSpanYears,
                        input.ConditionFactor,
                        input.FunctionalObsolescence,
                        input.EconomicObsolescence,
                        input.MarketDemandAvailable,
                        input.Notes,
                        input.DisplaySequence
                    );
                    existing.SetFairMarketValue(input.FairMarketValue);
                }
            }
            else
            {
                // Create new
                var item = method.AddMachineCostItem(input.AppraisalPropertyId, input.DisplaySequence);
                item.Update(
                    input.RcnReplacementCost,
                    input.LifeSpanYears,
                    input.ConditionFactor,
                    input.FunctionalObsolescence,
                    input.EconomicObsolescence,
                    input.MarketDemandAvailable,
                    input.Notes,
                    input.DisplaySequence
                );
                item.SetFairMarketValue(input.FairMarketValue);
            }
        }
    }
}
