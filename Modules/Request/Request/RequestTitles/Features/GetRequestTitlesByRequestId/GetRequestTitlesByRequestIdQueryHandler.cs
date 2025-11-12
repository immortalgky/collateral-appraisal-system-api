namespace Request.RequestTitles.Features.GetRequestTitlesByRequestId;

internal class GetRequestTitlesByRequestIdQueryHandler(IRequestTitleReadRepository readRepository)
    : IQueryHandler<GetRequestTitlesByRequestIdQuery, GetRequestTitlesByRequestIdResult>
{
    public async Task<GetRequestTitlesByRequestIdResult> Handle(GetRequestTitlesByRequestIdQuery query,
        CancellationToken cancellationToken)
    {
        var requestTitleEntities = await readRepository
            .FindAsync(rt => rt.RequestId == query.RequestId, cancellationToken);

        var requestTitles = requestTitleEntities.Select(rt => new RequestTitleDto(
            rt.Id,
            rt.RequestId,
            rt.CollateralType,
            rt.CollateralStatus,
            rt.TitleNo,
            rt.DeedType,
            rt.TitleDetail,
            rt.Rawang,
            rt.LandNo,
            rt.SurveyNo,
            rt.LandArea.AreaRai,
            rt.LandArea.AreaNgan,
            rt.LandArea.AreaSquareWa,
            rt.OwnerName,
            rt.RegistrationNo,
            rt.Vehicle.VehicleType,
            rt.Vehicle.VehicleAppointmentLocation,
            rt.Vehicle.ChassisNumber,
            rt.Machine.MachineStatus,
            rt.Machine.MachineType,
            rt.Machine.InstallationStatus,
            rt.Machine.InvoiceNumber,
            rt.Machine.NumberOfMachinery,
            rt.BuildingType,
            rt.UsableArea,
            rt.NoOfBuilding,
            rt.TitleAddress.Adapt<AddressDto>(),
            rt.DopaAddress.Adapt<AddressDto>()
        )).ToList();

        return new GetRequestTitlesByRequestIdResult(requestTitles);
    }
}