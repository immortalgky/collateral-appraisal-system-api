namespace Request.RequestTitles.Features.UpdateRequestTitle;

internal class UpdateRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<UpdateRequestTitleCommand, UpdateRequestTitleResult>
{
    public async Task<UpdateRequestTitleResult> Handle(UpdateRequestTitleCommand command,
        CancellationToken cancellationToken)
    {
        var requestTitle = await requestTitleRepository.GetByIdAsync(command.Id, cancellationToken);
        if (requestTitle is null || requestTitle.RequestId != command.RequestId)
        {
            throw new RequestTitleNotFoundException(command.Id);
        }

        requestTitle.UpdateDetails(
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

        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new UpdateRequestTitleResult(true);
    }
}