namespace Collateral.Application.Features.BlockUnitMaintenance.UpdateProjectUnitSaleInfo;

public class UpdateProjectUnitSaleInfoCommandHandler(
    ICollateralMasterRepository repository,
    CollateralDbContext dbContext)
    : ICommandHandler<UpdateProjectUnitSaleInfoCommand>
{
    public async Task<Unit> Handle(
        UpdateProjectUnitSaleInfoCommand command,
        CancellationToken cancellationToken)
    {
        // Load all units for this project (tracked by change-tracker).
        var units = await dbContext.ProjectUnits
            .Where(u => u.CollateralMasterId == command.CollateralMasterId)
            .ToListAsync(cancellationToken);

        if (units.Count == 0)
            throw new NotFoundException("ProjectUnits for CollateralMaster", command.CollateralMasterId);

        // Build a lookup to verify every requested UnitId belongs to this project.
        var unitMap = units.ToDictionary(u => u.Id);

        foreach (var item in command.Items)
        {
            if (!unitMap.TryGetValue(item.UnitId, out var unit))
                throw new InvalidOperationException(
                    $"Unit {item.UnitId} does not belong to CollateralMaster {command.CollateralMasterId}.");

            // Domain method enforces invariants (Loan requires LoanBankName, etc.).
            unit.SetSaleInfo(item.IsSold, item.PurchaseBy, item.LoanBankName);
        }

        // Recalculate RemainingUnits on the ProjectDetail.
        // We load ProjectDetail with its Units navigation so RecountRemaining() can read the
        // updated IsSold values from the already-tracked unit instances (EF identity map).
        var projectDetail = await dbContext.Set<ProjectDetail>()
            .Include(d => d.Units)
            .FirstOrDefaultAsync(d => d.CollateralMasterId == command.CollateralMasterId, cancellationToken);

        projectDetail?.RecountRemaining();

        await repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
