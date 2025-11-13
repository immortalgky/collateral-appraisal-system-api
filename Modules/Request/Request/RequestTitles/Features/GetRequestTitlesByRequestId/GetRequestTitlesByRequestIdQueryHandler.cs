namespace Request.RequestTitles.Features.GetRequestTitlesByRequestId;

internal class GetRequestTitlesByRequestIdQueryHandler(IRequestTitleReadRepository readRepository)
    : IQueryHandler<GetRequestTitlesByRequestIdQuery, GetRequestTitlesByRequestIdResult>
{
    public async Task<GetRequestTitlesByRequestIdResult> Handle(GetRequestTitlesByRequestIdQuery query,
        CancellationToken cancellationToken)
    {
        var requestTitleEntities = await readRepository
            .FindAsync(rt => rt.RequestId == query.RequestId, cancellationToken);

        var requestTitles = requestTitleEntities
            .OrderBy(rt => rt.CreatedOn)
            .Select(rt => new RequestTitleDto(
                rt.Id,
                rt.RequestId,
                rt.CollateralType,
                rt.CollateralStatus,
                rt.TitleDeedInfo.TitleNo,
                rt.TitleDeedInfo.DeedType,
                rt.TitleDeedInfo.TitleDetail,
                rt.SurveyInfo.Rawang,
                rt.SurveyInfo.LandNo,
                rt.SurveyInfo.SurveyNo,
                rt.LandArea.AreaRai,
                rt.LandArea.AreaNgan,
                rt.LandArea.AreaSquareWa,
                rt.OwnerName,
                rt.RegistrationNo,
                rt.Vehicle.VehicleType,
                rt.Vehicle.VehicleAppointmentLocation,
                rt.Vehicle.ChassisNumber,
                rt.Machinery.MachineryStatus,
                rt.Machinery.MachineryType,
                rt.Machinery.InstallationStatus,
                rt.Machinery.InvoiceNumber,
                rt.Machinery.NumberOfMachinery,
                rt.BuildingInfo.BuildingType,
                rt.BuildingInfo.UsableArea,
                rt.BuildingInfo.NumberOfBuilding,
                rt.TitleAddress.Adapt<AddressDto>(),
                rt.DopaAddress.Adapt<AddressDto>()
            ))
            .ToList();

        return new GetRequestTitlesByRequestIdResult(requestTitles);
    }
}