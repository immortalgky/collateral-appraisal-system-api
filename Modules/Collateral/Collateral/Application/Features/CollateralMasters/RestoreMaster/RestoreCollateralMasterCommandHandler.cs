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

        // Dedup-key collision: another master may have been created with the same key while this was deleted.
        // Family-grouped arms — a master's CollateralType may have flipped via LATEST-wins
        // (e.g. L → LB when a building was appraised). Detect by detail-row presence rather than
        // re-enumerating every code variant so a future code addition can't silently skip the check.
        bool collides;
        if (master.LandDetail is not null)
        {
            collides = await repository.LandDedupCollidesAsync(
                master.Id,
                master.LandDetail.Province,
                master.LandDetail.District,
                master.LandDetail.SubDistrict,
                master.LandDetail.TitleType,
                master.LandDetail.TitleNumber,
                master.LandDetail.SurveyNumber,
                master.LandDetail.LandParcelNumber,
                master.LandDetail.Rawang,
                cancellationToken);
        }
        else if (master.CondoDetail is not null)
        {
            collides = await repository.CondoDedupCollidesAsync(
                master.Id,
                master.CondoDetail.CondoRegistrationNumber,
                master.CondoDetail.BuildingNumber,
                master.CondoDetail.FloorNumber,
                master.CondoDetail.RoomNumber,
                master.CondoDetail.Province,
                master.CondoDetail.District,
                master.CondoDetail.SubDistrict,
                cancellationToken);
        }
        else if (master.LeaseholdDetail is not null)
        {
            collides = await repository.LeaseholdDedupCollidesAsync(
                master.Id,
                master.LeaseholdDetail.LeaseRegistrationNo,
                master.LeaseholdDetail.UnderlyingMasterId,
                master.LeaseholdDetail.Lessor,
                master.LeaseholdDetail.Lessee,
                master.LeaseholdDetail.LeaseTermStart,
                cancellationToken);
        }
        else if (master.MachineDetail is not null)
        {
            collides = await repository.MachineDedupCollidesAsync(
                master.Id,
                master.MachineDetail.MachineRegistrationNo,
                master.MachineDetail.SerialNo,
                master.MachineDetail.Brand,
                master.MachineDetail.Model,
                master.MachineDetail.Manufacturer,
                cancellationToken);
        }
        else
        {
            collides = false;
        }

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
