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
                rt.TitleNo,
                rt.TitleDetail,
                rt.Owner,
                rt.LandArea.Rai,
                rt.LandArea.Ngan,
                rt.LandArea.Wa,
                rt.BuildingType,
                rt.UsageArea,
                rt.NoOfBuilding,
                new AddressDto(
                    rt.TitleAddress.HouseNo,
                    rt.TitleAddress.RoomNo,
                    rt.TitleAddress.FloorNo,
                    rt.TitleAddress.BuildingNo,
                    rt.TitleAddress.ProjectName,
                    rt.TitleAddress.Moo,
                    rt.TitleAddress.Soi,
                    rt.TitleAddress.Road,
                    rt.TitleAddress.SubDistrict,
                    rt.TitleAddress.District,
                    rt.TitleAddress.Province,
                    rt.TitleAddress.Postcode
                ),
                new AddressDto(
                    rt.DopaAddress.HouseNo,
                    rt.DopaAddress.RoomNo,
                    rt.DopaAddress.FloorNo,
                    rt.DopaAddress.BuildingNo,
                    rt.DopaAddress.ProjectName,
                    rt.DopaAddress.Moo,
                    rt.DopaAddress.Soi,
                    rt.DopaAddress.Road,
                    rt.DopaAddress.SubDistrict,
                    rt.DopaAddress.District,
                    rt.DopaAddress.Province,
                    rt.DopaAddress.Postcode
                ),
                rt.Vehicle.VehicleType,
                rt.Vehicle.VehicleRegistrationNo,
                rt.Vehicle.VehicleLocation,
                rt.Machine.MachineStatus,
                rt.Machine.MachineType,
                rt.Machine.MachineRegistrationStatus,
                rt.Machine.MachineRegistrationNo,
                rt.Machine.MachineInvoiceNo,
                rt.Machine.NoOfMachine
            ))
            .ToList();

        return new GetRequestTitlesByRequestIdResult(requestTitles);
    }
}