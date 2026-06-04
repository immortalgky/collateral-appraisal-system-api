namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDetailById;

public class GetSupportingDetailByIdQueryHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService)
    : IQueryHandler<GetSupportingDetailByIdQuery, GetSupportingDetailByIdResult>
{
    public async Task<GetSupportingDetailByIdResult> Handle(
        GetSupportingDetailByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (detail, status) = await repo.GetDetailByIdWithImagesAsync(query.DetailId, cancellationToken);


        if (detail is null || detail.SupportingDataId != query.SupportingId)
            throw new SupportingDataDetailNotFoundException(query.DetailId);

        var isArchived = status == SupportingStatus.Approved
                        || status == SupportingStatus.Rejected
                        || status == SupportingStatus.Cancelled;

        var hasEditPermission = currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT") && !isArchived;

        var images = detail.Images
            .OrderBy(i => i.DisplaySequence)
            .Select(i => new SupportingDetailImageDto(
                i.Id,
                i.DocumentId,
                i.StorageUrl,
                i.FileName,
                i.Title,
                i.Description,
                i.DisplaySequence))
            .ToList();

        return new GetSupportingDetailByIdResult(
            detail.Id,
            hasEditPermission,
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
            detail.Remark,
            images);
    }
}
