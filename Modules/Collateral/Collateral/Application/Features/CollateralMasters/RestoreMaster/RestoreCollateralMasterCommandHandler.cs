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
        async Task<bool> CollidesAsync(CollateralMaster m)
        {
            if (m.LandDetail is not null)
            {
                return await repository.LandDedupCollidesAsync(
                    m.Id,
                    m.LandDetail.Province,
                    m.LandDetail.District,
                    m.LandDetail.SubDistrict,
                    m.LandDetail.TitleNumber,
                    cancellationToken);
            }

            if (m.CondoDetail is not null)
            {
                return await repository.CondoDedupCollidesAsync(
                    m.Id,
                    m.CondoDetail.CondoRegistrationNumber,
                    m.CondoDetail.BuildingNumber,
                    m.CondoDetail.FloorNumber,
                    m.CondoDetail.RoomNumber,
                    m.CondoDetail.Province,
                    m.CondoDetail.District,
                    m.CondoDetail.SubDistrict,
                    cancellationToken);
            }

            if (m.LeaseholdDetail is not null)
            {
                return await repository.LeaseholdDedupCollidesAsync(
                    m.Id,
                    m.LeaseholdDetail.LeaseRegistrationNo,
                    m.LeaseholdDetail.UnderlyingMasterId,
                    m.LeaseholdDetail.Lessor,
                    m.LeaseholdDetail.Lessee,
                    m.LeaseholdDetail.LeaseTermStart,
                    cancellationToken);
            }

            if (m.MachineDetail is not null)
            {
                return await repository.MachineDedupCollidesAsync(
                    m.Id,
                    m.MachineDetail.MachineRegistrationNo,
                    m.MachineDetail.SerialNo,
                    m.MachineDetail.Brand,
                    m.MachineDetail.Model,
                    m.MachineDetail.Manufacturer,
                    cancellationToken);
            }

            return false;
        }

        if (await CollidesAsync(master))
            throw new ConflictException(
                "Cannot restore: another active master already exists with the same dedup key. " +
                "Resolve the conflict before restoring.");

        // Soft-delete takes the alias rows down with the parent, so restore has to bring them back —
        // otherwise the parent goes live while its other titles stay deleted, the dedup lookup misses
        // them, and the next appraisal mints duplicate masters for titles we already own.
        var aliases = (await repository.FindAllAliasesByParentMasterIdAsync(master.Id, cancellationToken))
            .Where(a => a.IsDeleted)
            .ToList();

        foreach (var alias in aliases)
        {
            // Checked per alias: each carries its own dedup key, and one of them may have been
            // re-created as a live master while this group was deleted. Fail before writing anything
            // rather than letting the filtered unique index reject the whole batch.
            if (await CollidesAsync(alias))
                throw new ConflictException(
                    $"Cannot restore: alias master {alias.Id} shares its dedup key with another active " +
                    "master. Resolve the conflict before restoring.");
        }

        var by = currentUser.Username ?? currentUser.UserId?.ToString() ?? "unknown";
        master.Restore(command.Reason, by);

        foreach (var alias in aliases)
            alias.Restore(command.Reason, by);

        await repository.SaveChangesAsync(cancellationToken);

        return new RestoreCollateralMasterResult(master.Id);
    }
}
