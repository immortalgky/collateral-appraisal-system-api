using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockCondo.CalculateCondoUnitPrices;

public class CalculateCondoUnitPricesCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<CalculateCondoUnitPricesCommand>
{
    public async Task<Unit> Handle(
        CalculateCondoUnitPricesCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var assumption = appraisal.CondoPricingAssumption
                         ?? throw new InvalidOperationException("Pricing assumptions must be set before calculating prices.");

        // Load existing unit prices
        var existingPrices = await dbContext.CondoUnitPrices
            .Where(p => appraisal.CondoUnits.Select(u => u.Id).Contains(p.CondoUnitId))
            .ToListAsync(cancellationToken);

        var existingPriceMap = existingPrices.ToDictionary(p => p.CondoUnitId);

        // Build model assumption lookup by model type name
        var modelAssumptionMap = assumption.ModelAssumptions
            .Where(ma => ma.ModelType != null)
            .ToDictionary(ma => ma.ModelType!, ma => ma);

        foreach (var unit in appraisal.CondoUnits)
        {
            // Get or create unit price
            if (!existingPriceMap.TryGetValue(unit.Id, out var unitPrice))
            {
                unitPrice = CondoUnitPrice.Create(unit.Id);
                dbContext.CondoUnitPrices.Add(unitPrice);
            }

            // Find matching model assumption
            CondoModelAssumption? modelAssumption = null;
            if (unit.ModelType != null && modelAssumptionMap.TryGetValue(unit.ModelType, out var matched))
                modelAssumption = matched;

            var standardPrice = modelAssumption?.StandardPrice ?? 0m;
            var coverageAmount = modelAssumption?.CoverageAmount;

            // Calculate location adjustment
            var adjustPriceLocation = 0m;
            if (unitPrice.IsCorner) adjustPriceLocation += assumption.CornerAdjustment ?? 0m;
            if (unitPrice.IsEdge) adjustPriceLocation += assumption.EdgeAdjustment ?? 0m;
            if (unitPrice.IsPoolView) adjustPriceLocation += assumption.PoolViewAdjustment ?? 0m;
            if (unitPrice.IsSouth) adjustPriceLocation += assumption.SouthAdjustment ?? 0m;
            if (unitPrice.IsOther) adjustPriceLocation += assumption.OtherAdjustment ?? 0m;

            // Calculate floor increment
            var priceIncrementPerFloor = 0m;
            if (unit.Floor.HasValue
                && assumption.FloorIncrementEveryXFloor.HasValue
                && assumption.FloorIncrementEveryXFloor.Value > 0
                && assumption.FloorIncrementAmount.HasValue)
            {
                var floorGroups = (unit.Floor.Value - 1) / assumption.FloorIncrementEveryXFloor.Value;
                priceIncrementPerFloor = floorGroups * assumption.FloorIncrementAmount.Value;
            }

            // Total = StandardPrice * UsableArea + AdjustPriceLocation + PriceIncrementPerFloor
            var usableArea = unit.UsableArea ?? 0m;
            var totalAppraisalValue = (standardPrice * usableArea) + adjustPriceLocation + priceIncrementPerFloor;
            var totalAppraisalValueRounded = Math.Round(totalAppraisalValue, 0);

            // Force selling price
            var forceSellingPrice = assumption.ForceSalePercentage.HasValue
                ? Math.Round(totalAppraisalValueRounded * assumption.ForceSalePercentage.Value / 100m, 0)
                : (decimal?)null;

            unitPrice.UpdateCalculatedValues(
                adjustPriceLocation,
                standardPrice,
                priceIncrementPerFloor,
                totalAppraisalValue,
                totalAppraisalValueRounded,
                forceSellingPrice,
                coverageAmount);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
