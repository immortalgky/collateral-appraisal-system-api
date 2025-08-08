namespace Request.RequestTitles.Features.AddRequestTitle;

public class AddRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<AddRequestTitleCommand, AddRequestTitleResult>
{
    public async Task<AddRequestTitleResult> Handle(AddRequestTitleCommand command,
        CancellationToken cancellationToken)
    {
        // TODO: Validate if request exists and is in a valid state for adding titles.

        var requestTitle = RequestTitle.Create(
            command.RequestId,
            command.CollateralType,
            command.TitleNo,
            command.TitleDetail,
            command.Owner,
            LandArea.Of(command.Rai, command.Ngan, command.Wa),
            command.BuildingType,
            command.UsageArea,
            command.NoOfBuilding,
            Address.Create(
                command.TitleAddress.HouseNo,
                command.TitleAddress.RoomNo,
                command.TitleAddress.FloorNo,
                command.TitleAddress.BuildingNo,
                command.TitleAddress.ProjectName,
                command.TitleAddress.Moo,
                command.TitleAddress.Soi,
                command.TitleAddress.Road,
                command.TitleAddress.SubDistrict,
                command.TitleAddress.District,
                command.TitleAddress.Province,
                command.TitleAddress.Postcode
            ),
            Address.Create(
                command.DopaAddress?.HouseNo,
                command.DopaAddress?.RoomNo,
                command.DopaAddress?.FloorNo,
                command.DopaAddress?.BuildingNo,
                command.DopaAddress?.ProjectName,
                command.DopaAddress?.Moo,
                command.DopaAddress?.Soi,
                command.DopaAddress?.Road,
                command.DopaAddress?.SubDistrict,
                command.DopaAddress?.District,
                command.DopaAddress?.Province,
                command.DopaAddress?.Postcode
            ),
            Vehicle.Create(
                command.VehicleType,
                command.VehicleRegistrationNo,
                command.VehicleLocation
            ),
            Machine.Create(
                command.MachineStatus,
                command.MachineType,
                command.MachineRegistrationStatus,
                command.MachineRegistrationNo,
                command.MachineInvoiceNo,
                command.NoOfMachine
            )
        );

        await requestTitleRepository.AddAsync(requestTitle, cancellationToken);
        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new AddRequestTitleResult(requestTitle.Id);
    }
}