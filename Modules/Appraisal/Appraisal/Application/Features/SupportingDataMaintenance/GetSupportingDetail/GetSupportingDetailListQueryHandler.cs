namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDetailList;

public class GetSupportingDetailListQueryHandler(
        ISupportingDataRepository repo
    )
    : IQueryHandler<GetSupportingDetailListQuery, GetSupportingDetailListResult>
{
    public async Task<GetSupportingDetailListResult> Handle(
        GetSupportingDetailListQuery req, CancellationToken ct)
    {
        var page = new PaginationRequest(req.Page, req.PageSize);

        var paged = await repo.GetDetailListAsync(page, req.SupportingId, ct);

        var items = paged.Items.Select(d => new SupportingDetailListItem(
            d.Id,
            d.PropertyName,
            d.Developer,
            d.ModelName,
            d.CollateralType,
            d.BuildingType,
            d.LandArea,
            d.UsableArea,
            d.ProjectName,
            d.RoomFloor,
            d.Address?.HouseNo,
            d.Address?.SubDistrict,
            d.Address?.District,
            d.Address?.Province,
            d.Location?.Latitude,
            d.Location?.Longitude,
            d.PlotLocationType,
            d.PricePerUnit,
            d.OfferingPrice,
            d.SellingPrice,
            d.PhoneNo,
            d.InformationDate,
            d.Website,
            d.SourceUrl,
            d.Remark));

        return new GetSupportingDetailListResult(
            items,
            (int)paged.Count,
            paged.PageNumber,
            paged.PageSize
        );
    }
}
