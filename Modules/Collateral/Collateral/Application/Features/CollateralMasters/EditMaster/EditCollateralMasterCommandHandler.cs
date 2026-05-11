using Shared.Identity;

namespace Collateral.Application.Features.CollateralMasters.EditMaster;

public class EditCollateralMasterCommandHandler(
    ICollateralMasterRepository repository,
    ICurrentUserService currentUser
) : ICommandHandler<EditCollateralMasterCommand, EditCollateralMasterResult>
{
    public async Task<EditCollateralMasterResult> Handle(
        EditCollateralMasterCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
            throw new UnauthorizedAccessException("Only Admin users can edit collateral masters.");

        var master = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (master is null)
            throw new NotFoundException("CollateralMaster", command.Id);

        // Dedup-key collision check — only for fields that would change the dedup key
        if (command.LandEdit is not null && master.LandDetail is not null)
        {
            var d = command.LandEdit;
            var ld = master.LandDetail;
            var targetLoc = d.LandOfficeCode ?? ld.LandOfficeCode;
            var targetProv = d.Province ?? ld.Province;
            var targetDistrict = d.District ?? ld.District;
            var targetSubDistrict = d.SubDistrict ?? ld.SubDistrict;
            var targetType = d.TitleType ?? ld.TitleType;
            var targetNo = d.TitleNumber ?? ld.TitleNumber;
            var targetSurvey = d.SurveyNumber ?? ld.SurveyNumber;
            var targetParcel = d.LandParcelNumber ?? ld.LandParcelNumber;

            bool collides = await repository.LandDedupCollidesAsync(
                master.Id, targetLoc, targetProv, targetDistrict, targetSubDistrict,
                targetType, targetNo, targetSurvey, targetParcel, cancellationToken);

            if (collides)
                throw new ConflictException(
                    "Another non-deleted Land master already exists with the same dedup key.");
        }

        if (command.CondoEdit is not null && master.CondoDetail is not null)
        {
            var d = command.CondoEdit;
            var cd = master.CondoDetail;
            bool collides = await repository.CondoDedupCollidesAsync(
                master.Id,
                d.LandOfficeCode ?? cd.LandOfficeCode,
                d.CondoRegistrationNumber ?? cd.CondoRegistrationNumber,
                d.BuildingNumber ?? cd.BuildingNumber,
                d.FloorNumber ?? cd.FloorNumber,
                d.RoomNumber ?? cd.RoomNumber,
                d.TitleNumber ?? cd.TitleNumber,
                d.TitleType ?? cd.TitleType,
                cancellationToken);

            if (collides)
                throw new ConflictException(
                    "Another non-deleted Condo master already exists with the same dedup key.");
        }

        if (command.LeaseholdEdit is not null && master.LeaseholdDetail is not null)
        {
            var d = command.LeaseholdEdit;
            var lh = master.LeaseholdDetail;
            bool collides = await repository.LeaseholdDedupCollidesAsync(
                master.Id,
                d.LeaseRegistrationNo ?? lh.LeaseRegistrationNo,
                lh.UnderlyingMasterId,  // UnderlyingMasterId is not admin-editable
                d.Lessor ?? lh.Lessor,
                d.Lessee ?? lh.Lessee,
                d.LeaseTermStart ?? lh.LeaseTermStart,
                cancellationToken);

            if (collides)
                throw new ConflictException(
                    "Another non-deleted Leasehold master already exists with the same dedup key.");
        }

        if (command.MachineEdit is not null && master.MachineDetail is not null)
        {
            var d = command.MachineEdit;
            var md = master.MachineDetail;
            bool collides = await repository.MachineDedupCollidesAsync(
                master.Id,
                d.MachineRegistrationNo ?? md.MachineRegistrationNo,
                d.SerialNo ?? md.SerialNo,
                d.Brand ?? md.Brand,
                d.Model ?? md.Model,
                d.Manufacturer ?? md.Manufacturer,
                cancellationToken);

            if (collides)
                throw new ConflictException(
                    "Another non-deleted Machine master already exists with the same dedup key.");
        }

        var by = currentUser.Username ?? currentUser.UserId?.ToString() ?? "unknown";

        master.Edit(
            command.OwnerName,
            command.LandEdit,
            command.CondoEdit,
            command.LeaseholdEdit,
            command.MachineEdit,
            command.Reason,
            by);

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "CollateralMaster was modified by another request. Please reload and retry.");
        }

        return new EditCollateralMasterResult(master.Id);
    }
}
