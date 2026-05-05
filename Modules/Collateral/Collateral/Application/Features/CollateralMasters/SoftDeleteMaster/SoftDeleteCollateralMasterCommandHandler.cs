using Shared.Identity;

namespace Collateral.Application.Features.CollateralMasters.SoftDeleteMaster;

public class SoftDeleteCollateralMasterCommandHandler(
    ICollateralMasterRepository repository,
    ICurrentUserService currentUser
) : ICommandHandler<SoftDeleteCollateralMasterCommand, SoftDeleteCollateralMasterResult>
{
    public async Task<SoftDeleteCollateralMasterResult> Handle(
        SoftDeleteCollateralMasterCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
            throw new UnauthorizedAccessException("Only Admin users can soft-delete collateral masters.");

        var master = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (master is null)
            throw new NotFoundException("CollateralMaster", command.Id);

        // RESTRICT: cannot soft-delete an underlying master that has active Leasehold masters
        var activeLeaseholdIds = await repository.GetActiveLeaseholdIdsForUnderlyingAsync(
            command.Id, cancellationToken);

        if (activeLeaseholdIds.Count > 0)
        {
            var idList = string.Join(", ", activeLeaseholdIds.Select(id => id.ToString()));
            throw new ConflictException(
                $"Cannot delete this master because it is referenced by active Leasehold master(s): {idList}. " +
                "Delete or reassign those leaseholds first.");
        }

        var by = currentUser.Username ?? currentUser.UserId?.ToString() ?? "unknown";
        master.SoftDelete(command.Reason, by);

        await repository.SaveChangesAsync(cancellationToken);

        return new SoftDeleteCollateralMasterResult(master.Id);
    }
}
