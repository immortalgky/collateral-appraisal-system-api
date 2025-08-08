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
            requestTitle.TitleNo,
            requestTitle.TitleDetail,
            requestTitle.Owner,
            requestTitle.LandArea.Rai,
            requestTitle.LandArea.Ngan,
            requestTitle.LandArea.Wa,
            requestTitle.BuildingType,
            requestTitle.UsageArea,
            requestTitle.NoOfBuilding,
            new AddressDto(
                requestTitle.TitleAddress.HouseNo,
                requestTitle.TitleAddress.RoomNo,
                requestTitle.TitleAddress.FloorNo,
                requestTitle.TitleAddress.BuildingNo,
                requestTitle.TitleAddress.ProjectName,
                requestTitle.TitleAddress.Moo,
                requestTitle.TitleAddress.Soi,
                requestTitle.TitleAddress.Road,
                requestTitle.TitleAddress.SubDistrict,
                requestTitle.TitleAddress.District,
                requestTitle.TitleAddress.Province,
                requestTitle.TitleAddress.Postcode
            ),
            new AddressDto(
                requestTitle.DopaAddress.HouseNo,
                requestTitle.DopaAddress.RoomNo,
                requestTitle.DopaAddress.FloorNo,
                requestTitle.DopaAddress.BuildingNo,
                requestTitle.DopaAddress.ProjectName,
                requestTitle.DopaAddress.Moo,
                requestTitle.DopaAddress.Soi,
                requestTitle.DopaAddress.Road,
                requestTitle.DopaAddress.SubDistrict,
                requestTitle.DopaAddress.District,
                requestTitle.DopaAddress.Province,
                requestTitle.DopaAddress.Postcode
            ),
            requestTitle.Vehicle.VehicleType,
            requestTitle.Vehicle.VehicleRegistrationNo,
            requestTitle.Vehicle.VehicleLocation,
            requestTitle.Machine.MachineStatus,
            requestTitle.Machine.MachineType,
            requestTitle.Machine.MachineRegistrationStatus,
            requestTitle.Machine.MachineRegistrationNo,
            requestTitle.Machine.MachineInvoiceNo,
            requestTitle.Machine.NoOfMachine
        );

        return result;
    }
}