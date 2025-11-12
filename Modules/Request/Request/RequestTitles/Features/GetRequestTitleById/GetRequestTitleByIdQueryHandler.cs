namespace Request.RequestTitles.Features.GetRequestTitleById;

internal class GetRequestTitleByIdQueryHandler(IRequestTitleReadRepository readRepository)
    : IQueryHandler<GetRequestTitleByIdQuery, GetRequestTitleByIdResult>
{
    public async Task<GetRequestTitleByIdResult> Handle(GetRequestTitleByIdQuery query,
        CancellationToken cancellationToken)
    {
        var requestTitle =
            await readRepository.FirstOrDefaultAsync(rt => rt.Id == query.Id && rt.RequestId == query.RequestId,
                cancellationToken);

        if (requestTitle is null)
            throw new RequestTitleNotFoundException(query.Id);

        var result = new GetRequestTitleByIdResult(
            requestTitle.Id,
            requestTitle.RequestId,
            requestTitle.CollateralType,
            requestTitle.CollateralStatus,
            requestTitle.TitleNo,
            requestTitle.DeedType,
            requestTitle.TitleDetail,
            requestTitle.Rawang,
            requestTitle.LandNo,
            requestTitle.SurveyNo,
            requestTitle.LandArea.AreaRai,
            requestTitle.LandArea.AreaNgan,
            requestTitle.LandArea.AreaSquareWa,
            requestTitle.OwnerName,
            requestTitle.RegistrationNo,
            requestTitle.Vehicle.VehicleType,
            requestTitle.Vehicle.VehicleAppointmentLocation,
            requestTitle.Vehicle.ChassisNumber,
            requestTitle.Machine.MachineStatus,
            requestTitle.Machine.MachineType,
            requestTitle.Machine.InstallationStatus,
            requestTitle.Machine.InvoiceNumber,
            requestTitle.Machine.NumberOfMachinery,
            requestTitle.BuildingType,
            requestTitle.UsableArea,
            requestTitle.NoOfBuilding,
            requestTitle.TitleAddress.Adapt<AddressDto>(),
            requestTitle.DopaAddress.Adapt<AddressDto>()
        );


        return result;
    }
}