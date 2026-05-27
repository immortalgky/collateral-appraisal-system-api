namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDetailById;

public class GetSupportingDetailByIdQueryHandler(ISupportingDataRepository repo)
    : IQueryHandler<GetSupportingDetailByIdQuery, GetSupportingDetailByIdResult>
{
    public async Task<GetSupportingDetailByIdResult> Handle(
        GetSupportingDetailByIdQuery query,
        CancellationToken cancellationToken)
    {
        var detail = await repo.GetDetailByIdAsync(query.DetailId, cancellationToken);

        if (detail is null || detail.SupportingDataId != query.SupportingId)
            throw new SupportingDataDetailNotFoundException(query.DetailId);

        return new GetSupportingDetailByIdResult(
            detail.Id,
            detail.PropertyName,
            detail.Developer,
            detail.ModelName,
            detail.CollateralType,
            detail.BuildingType,
            detail.LandArea,
            detail.UsableArea,
            detail.ProjectName,
            detail.RoomFloor,
            detail.Address?.HouseNo,
            detail.Address?.SubDistrict,
            detail.Address?.District,
            detail.Address?.Province,
            detail.Location?.Latitude,
            detail.Location?.Longitude,
            detail.PlotLocationType,
            detail.PricePerUnit,
            detail.OfferingPrice,
            detail.SellingPrice,
            detail.PhoneNo,
            detail.InformationDate,
            detail.Website,
            detail.SourceUrl,
            detail.Remark);
    }
}
