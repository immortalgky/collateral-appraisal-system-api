using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockCondo.SaveCondoUnitPrices;

public class SaveCondoUnitPricesCommandHandler(
    AppraisalDbContext dbContext,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<SaveCondoUnitPricesCommand>
{
    public async Task<Unit> Handle(
        SaveCondoUnitPricesCommand command,
        CancellationToken cancellationToken)
    {
        var unitIds = command.UnitPriceFlags.Select(f => f.CondoUnitId).ToList();

        // Validate all submitted unit IDs belong to the given appraisal
        var validUnitIds = await dbContext.CondoUnits
            .Where(u => u.AppraisalId == command.AppraisalId && unitIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var invalidIds = unitIds.Except(validUnitIds).ToList();
        if (invalidIds.Count > 0)
            throw new BadRequestException(
                "One or more condo unit IDs do not belong to this appraisal.",
                $"Invalid IDs: {string.Join(", ", invalidIds)}");

        var existingPrices = await dbContext.CondoUnitPrices
            .Where(p => unitIds.Contains(p.CondoUnitId))
            .ToListAsync(cancellationToken);

        var priceMap = existingPrices.ToDictionary(p => p.CondoUnitId);

        foreach (var flag in command.UnitPriceFlags)
        {
            if (!priceMap.TryGetValue(flag.CondoUnitId, out var unitPrice))
            {
                unitPrice = CondoUnitPrice.Create(flag.CondoUnitId);
                dbContext.CondoUnitPrices.Add(unitPrice);
            }

            unitPrice.UpdateLocationFlags(
                flag.IsCorner, flag.IsEdge, flag.IsPoolView,
                flag.IsSouth, flag.IsOther);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
