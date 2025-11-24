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
            .Select(rt => new RequestTitleDto{
                Id = rt.Id,
                RequestId = rt.RequestId,
                CollateralType = rt.CollateralType,
                CollateralStatus = rt.CollateralStatus!.Value,
                TitleNo = rt.TitleDeedInfo.TitleNo,
                DeedType = rt.TitleDeedInfo.DeedType,
                TitleDetail = rt.TitleDeedInfo.TitleDetail,
                Rawang = rt.SurveyInfo.Rawang,
                LandNo = rt.SurveyInfo.LandNo,
                SurveyNo = rt.SurveyInfo.SurveyNo,
                AreaRai = rt.LandArea.AreaRai,
                AreaNgan = rt.LandArea.AreaNgan,
                AreaSquareWa = rt.LandArea.AreaSquareWa,
                OwnerName = rt.OwnerName,
                RegistrationNumber = rt.RegistrationNo,
                VehicleType = rt.VehicleInfo.VehicleType,
                VehicleAppointmentLocation = rt.VehicleInfo.VehicleAppointmentLocation,
                ChassisNumber = rt.VehicleInfo.ChassisNumber,
                MachineStatus = rt.MachineInfo.MachineStatus,
                MachineType = rt.MachineInfo.MachineType,
                InstallationStatus = rt.MachineInfo.InstallationStatus,
                InvoiceNumber = rt.MachineInfo.InvoiceNumber,
                NumberOfMachine = rt.MachineInfo.NumberOfMachinery,
                BuildingType = rt.BuildingInfo.BuildingType,
                UsableArea = rt.BuildingInfo.UsableArea,
                NumberOfBuilding = rt.BuildingInfo.NumberOfBuilding,
                TitleAddress = rt.TitleAddress.Adapt<AddressDto>(),
                DopaAddress = rt.DopaAddress.Adapt<AddressDto>()
            })
            .ToList();

        return new GetRequestTitlesByRequestIdResult(requestTitles);
    }
}