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
            requestTitle.TitleDeedInfo.TitleNo,
            requestTitle.TitleDeedInfo.DeedType,
            requestTitle.TitleDeedInfo.TitleDetail,
            requestTitle.SurveyInfo.Rawang,
            requestTitle.SurveyInfo.LandNo,
            requestTitle.SurveyInfo.SurveyNo,
            requestTitle.LandArea.AreaRai,
            requestTitle.LandArea.AreaNgan,
            requestTitle.LandArea.AreaSquareWa,
            requestTitle.OwnerName,
            requestTitle.RegistrationNo,
            requestTitle.Vehicle.VehicleType,
            requestTitle.Vehicle.VehicleAppointmentLocation,
            requestTitle.Vehicle.ChassisNumber,
            requestTitle.Machinery.MachineryStatus,
            requestTitle.Machinery.MachineryType,
            requestTitle.Machinery.InstallationStatus,
            requestTitle.Machinery.InvoiceNumber,
            requestTitle.Machinery.NumberOfMachinery,
            requestTitle.BuildingInfo.BuildingType,
            requestTitle.BuildingInfo.UsableArea,
            requestTitle.BuildingInfo.NumberOfBuilding,
            requestTitle.TitleAddress.Adapt<AddressDto>(),
            requestTitle.DopaAddress.Adapt<AddressDto>()
        );


        return result;
    }
}