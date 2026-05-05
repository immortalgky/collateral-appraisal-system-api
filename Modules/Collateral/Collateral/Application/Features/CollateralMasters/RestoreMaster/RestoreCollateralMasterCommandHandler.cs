using Shared.Identity;

namespace Collateral.Application.Features.CollateralMasters.RestoreMaster;

public class RestoreCollateralMasterCommandHandler(
    ICollateralMasterRepository repository,
    ICurrentUserService currentUser
) : ICommandHandler<RestoreCollateralMasterCommand, RestoreCollateralMasterResult>
{
    public async Task<RestoreCollateralMasterResult> Handle(
        RestoreCollateralMasterCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
            throw new UnauthorizedAccessException("Only Admin users can restore collateral masters.");

        // FindByIdAsync filters on !IsDeleted — we need to find deleted masters too
        var master = await repository.FindByIdIncludingDeletedAsync(command.Id, cancellationToken);
        if (master is null)
            throw new NotFoundException("CollateralMaster", command.Id);

        // Dedup-key collision: another master may have been created with the same key while this was deleted
        bool collides = master.CollateralType switch
        {
            CollateralTypes.Land when master.LandDetail is not null => await repository.LandDedupCollidesAsync(
                master.Id,
                master.LandDetail.LandOfficeCode,
                master.LandDetail.Province,
                master.LandDetail.Amphur,
                master.LandDetail.Tambon,
                master.LandDetail.TitleDeedType,
                master.LandDetail.TitleDeedNo,
                master.LandDetail.SurveyOrParcelNo,
                cancellationToken),

            CollateralTypes.Condo when master.CondoDetail is not null => await repository.CondoDedupCollidesAsync(
                master.Id,
                master.CondoDetail.LandOfficeCode,
                master.CondoDetail.CondoRegistrationNumber,
                master.CondoDetail.BuildingNumber,
                master.CondoDetail.FloorNumber,
                master.CondoDetail.UnitNumber,
                master.CondoDetail.TitleNumber,
                master.CondoDetail.TitleType,
                cancellationToken),

            CollateralTypes.Leasehold when master.LeaseholdDetail is not null => await repository.LeaseholdDedupCollidesAsync(
                master.Id,
                master.LeaseholdDetail.LeaseRegistrationNo,
                master.LeaseholdDetail.UnderlyingMasterId,
                master.LeaseholdDetail.Lessor,
                master.LeaseholdDetail.Lessee,
                master.LeaseholdDetail.LeaseTermStart,
                cancellationToken),

            CollateralTypes.Machine when master.MachineDetail is not null => await repository.MachineDedupCollidesAsync(
                master.Id,
                master.MachineDetail.MachineRegistrationNo,
                master.MachineDetail.SerialNo,
                master.MachineDetail.Brand,
                master.MachineDetail.Model,
                master.MachineDetail.Manufacturer,
                cancellationToken),

            _ => false
        };

        if (collides)
            throw new ConflictException(
                "Cannot restore: another active master already exists with the same dedup key. " +
                "Resolve the conflict before restoring.");

        var by = currentUser.Username ?? currentUser.UserId?.ToString() ?? "unknown";
        master.Restore(command.Reason, by);

        await repository.SaveChangesAsync(cancellationToken);

        return new RestoreCollateralMasterResult(master.Id);
    }
}
