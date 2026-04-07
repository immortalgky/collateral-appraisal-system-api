using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockVillage.CalculateVillageUnitPrices;

public class CalculateVillageUnitPricesCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<CalculateVillageUnitPricesCommand>
{
    private const string LocationMethodSqm = "AdjustPriceSqm";
    private const string LocationMethodPercentage = "AdjustPricePercentage";

    public async Task<Unit> Handle(
        CalculateVillageUnitPricesCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var assumption = appraisal.VillagePricingAssumption
                         ?? throw new InvalidOperationException("Pricing assumptions must be set before calculating prices.");

        // Load existing unit prices
        var unitIds = appraisal.VillageUnits.Select(u => u.Id).ToList();
        var existingPrices = await dbContext.VillageUnitPrices
            .Where(p => unitIds.Contains(p.VillageUnitId))
            .ToListAsync(cancellationToken);

        var existingPriceMap = existingPrices.ToDictionary(p => p.VillageUnitId);

        // Build lookups by ModelType (matched against unit's ModelName)
        // GroupBy guards against duplicate ModelType entries in the assumption list
        var modelAssumptionMap = assumption.ModelAssumptions
            .Where(ma => ma.ModelType != null)
            .GroupBy(ma => ma.ModelType!)
            .ToDictionary(g => g.Key, g => g.First());

        // Build lookup for standard land area from VillageModels by model name
        var villageModelMap = appraisal.VillageModels
            .Where(m => m.ModelName != null)
            .GroupBy(m => m.ModelName!)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var unit in appraisal.VillageUnits)
        {
            // Get or create unit price
            if (!existingPriceMap.TryGetValue(unit.Id, out var unitPrice))
            {
                unitPrice = VillageUnitPrice.Create(unit.Id);
                dbContext.VillageUnitPrices.Add(unitPrice);
            }

            // Find matching model assumption by ModelName
            VillageModelAssumption? modelAssumption = null;
            if (unit.ModelName != null && modelAssumptionMap.TryGetValue(unit.ModelName, out var matchedAssumption))
                modelAssumption = matchedAssumption;

            // Find standard land area from VillageModel
            var standardLandArea = 0m;
            if (unit.ModelName != null && villageModelMap.TryGetValue(unit.ModelName, out var villageModel))
                standardLandArea = villageModel.StandardLandArea ?? 0m;

            var standardPricePerSqm = modelAssumption?.StandardPrice ?? 0m;
            var coverageAmount = modelAssumption?.CoverageAmount;

            // StandardPrice = StandardPrice (per sqm) * UsableArea
            var usableArea = unit.UsableArea ?? 0m;
            var standardPrice = standardPricePerSqm * usableArea;

            // LandIncreaseDecreaseAmount = (LandArea - StandardLandArea) * LandIncreaseDecreaseRate
            var landArea = unit.LandArea ?? 0m;
            var landIncreaseDecreaseRate = assumption.LandIncreaseDecreaseRate ?? 0m;
            var landIncreaseDecreaseAmount = (landArea - standardLandArea) * landIncreaseDecreaseRate;

            // Calculate location adjustment based on LocationMethod
            var adjustPriceLocation = CalculateLocationAdjustment(
                assumption, unitPrice, standardPrice, usableArea);

            // TotalAppraisalValue = StandardPrice + LandIncreaseDecreaseAmount + AdjustPriceLocation
            var totalAppraisalValue = standardPrice + landIncreaseDecreaseAmount + adjustPriceLocation;

            // Round to nearest 10,000
            var totalAppraisalValueRounded = RoundToNearest10000(totalAppraisalValue);

            // ForceSellingPrice = Rounded * ForceSalePercentage / 100
            var forceSellingPrice = assumption.ForceSalePercentage.HasValue
                ? Math.Round(totalAppraisalValueRounded * assumption.ForceSalePercentage.Value / 100m, 0)
                : (decimal?)null;

            unitPrice.UpdateCalculatedValues(
                landIncreaseDecreaseAmount,
                adjustPriceLocation,
                standardPrice,
                totalAppraisalValue,
                totalAppraisalValueRounded,
                forceSellingPrice,
                coverageAmount);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }

    private static decimal CalculateLocationAdjustment(
        VillagePricingAssumption assumption,
        VillageUnitPrice unitPrice,
        decimal standardPrice,
        decimal usableArea)
    {
        var cornerAdj = assumption.CornerAdjustment ?? 0m;
        var edgeAdj = assumption.EdgeAdjustment ?? 0m;
        var nearGardenAdj = assumption.NearGardenAdjustment ?? 0m;
        var otherAdj = assumption.OtherAdjustment ?? 0m;

        var rawAdjustment = 0m;
        if (unitPrice.IsCorner) rawAdjustment += cornerAdj;
        if (unitPrice.IsEdge) rawAdjustment += edgeAdj;
        if (unitPrice.IsNearGarden) rawAdjustment += nearGardenAdj;
        if (unitPrice.IsOther) rawAdjustment += otherAdj;

        return assumption.LocationMethod switch
        {
            LocationMethodSqm => rawAdjustment * usableArea,
            LocationMethodPercentage => standardPrice * rawAdjustment / 100m,
            _ => rawAdjustment
        };
    }

    private static decimal RoundToNearest10000(decimal value)
    {
        return Math.Round(value / 10000m, MidpointRounding.AwayFromZero) * 10000m;
    }
}
