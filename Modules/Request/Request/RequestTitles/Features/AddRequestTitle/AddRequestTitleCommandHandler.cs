namespace Request.RequestTitles.Features.AddRequestTitle;

public class AddRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<AddRequestTitleCommand, AddRequestTitleResult>
{
    public async Task<AddRequestTitleResult> Handle(AddRequestTitleCommand command,
        CancellationToken cancellationToken)
    {
        
        var request = await requestTitleRepository.GetByIdAsync(command.RequestId);
        if (request is null)
            throw new RequestNotFoundException(command.RequestId);

        var requestTitle = RequestTitle.Create(
            command.RequestId,
            command.CollateralType,
            command.CollateralStatus,
            command.TitleNo,
            command.DeedType,
            command.TitleDetail,
            command.Rawang,
            command.LandNo,
            command.SurveyNo,
            LandArea.Of(command.AreaRai, command.AreaNgan, command.AreaSquareWa),
            command.OwnerName,
            command.RegistrationNo,
            Vehicle.Create(command.VehicleType, command.VehicleAppointmentLocation, command.ChassisNumber),
            Machine.Create(command.MachineStatus, command.MachineType, command.InstallationStatus, command.InvoiceNumber, command.NumberOfMachinery),
            command.BuildingType,
            command.UsableArea,
            command.NoOfBuilding,
            Condo.Create(command.CondoName, command.BuildingNo, command.RoomNo, command.FloorNo),
            Address.Create(
                command.TitleAddress.HouseNo,
                null,
                null,
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
                command.DopaAddress.HouseNo,
                null,
                null,
                command.DopaAddress.ProjectName,
                command.DopaAddress.Moo,
                command.DopaAddress.Soi,
                command.DopaAddress.Road,
                command.DopaAddress.SubDistrict,
                command.DopaAddress.District,
                command.DopaAddress.Province,
                command.DopaAddress.Postcode
            ),
            command.Notes
        );

        await requestTitleRepository.AddAsync(requestTitle, cancellationToken);
        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new AddRequestTitleResult(requestTitle.Id);
    }
}