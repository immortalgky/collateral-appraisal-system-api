using Request.Extensions;

namespace Request.RequestTitles.Features.UpdateRequestTitle;

internal class UpdateRequestTitleCommandHandler(IRequestTitleRepository requestTitleRepository)
    : ICommandHandler<UpdateRequestTitleCommand, UpdateRequestTitleResult>
{
    public async Task<UpdateRequestTitleResult> Handle(UpdateRequestTitleCommand command, CancellationToken cancellationToken)
    {
        var requestTitle = await requestTitleRepository.GetByIdAsync(command.Id, cancellationToken);

        if (requestTitle is null || requestTitle.RequestId != command.RequestId)
        {
            throw new RequestTitleNotFoundException(command.Id);
        }
        
        requestTitle.UpdateDetails(
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
            DtoExtensions.ToDomain(command.TitleAddress),
            DtoExtensions.ToDomain(command.DopaAddress),
            command.Notes
        );

        await requestTitleRepository.SaveChangesAsync(cancellationToken);

        return new UpdateRequestTitleResult(true);
    }
}