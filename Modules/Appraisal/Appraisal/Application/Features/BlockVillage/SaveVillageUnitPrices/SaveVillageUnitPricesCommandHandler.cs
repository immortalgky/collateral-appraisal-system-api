using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockVillage.SaveVillageUnitPrices;

public class SaveVillageUnitPricesCommandHandler(
    AppraisalDbContext dbContext,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<SaveVillageUnitPricesCommand>
{
    public async Task<Unit> Handle(
        SaveVillageUnitPricesCommand command,
        CancellationToken cancellationToken)
    {
        var unitIds = command.UnitPriceFlags.Select(f => f.VillageUnitId).ToList();

        // Validate all submitted unit IDs belong to the given appraisal
        var validUnitIds = await dbContext.VillageUnits
            .Where(u => u.AppraisalId == command.AppraisalId && unitIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var invalidIds = unitIds.Except(validUnitIds).ToList();
        if (invalidIds.Count > 0)
            throw new BadRequestException(
                "One or more village unit IDs do not belong to this appraisal.",
                $"Invalid IDs: {string.Join(", ", invalidIds)}");

        var existingPrices = await dbContext.VillageUnitPrices
            .Where(p => unitIds.Contains(p.VillageUnitId))
            .ToListAsync(cancellationToken);

        var priceMap = existingPrices.ToDictionary(p => p.VillageUnitId);

        foreach (var flag in command.UnitPriceFlags)
        {
            if (!priceMap.TryGetValue(flag.VillageUnitId, out var unitPrice))
            {
                unitPrice = VillageUnitPrice.Create(flag.VillageUnitId);
                dbContext.VillageUnitPrices.Add(unitPrice);
            }

            unitPrice.UpdateLocationFlags(
                flag.IsCorner, flag.IsEdge, flag.IsNearGarden, flag.IsOther);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
