namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateSupportingDetail;

internal class CreateSupportingDetailCommandHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService)
    : ICommandHandler<CreateSupportingDetailCommand, CreateSupportingDetailResult>
{
    public async Task<CreateSupportingDetailResult> Handle(
        CreateSupportingDetailCommand cmd, CancellationToken ct)
    {
        if (currentUserService.IsInRole("IntAppraisalChecker") || currentUserService.IsInRole("ExtAppraisalChecker"))
        {
            throw new UnauthorizedAccessException("Checkers are not allowed to create supporting details.");
        }

        var supportingData = await repo.GetByIdWithDetailsAsync(cmd.SupportingId, ct) ?? throw new NotFoundException($"Supporting data with ID {cmd.SupportingId} not found.");

        var detail = supportingData.AddDetail(new SupportingDataDetailData(
            cmd.Detail.PropertyName,
            cmd.Detail.Developer,
            cmd.Detail.ModelName,
            cmd.Detail.CollateralType,
            cmd.Detail.BuildingType,
            cmd.Detail.LandArea,
            cmd.Detail.UsableArea,
            cmd.Detail.ProjectName,
            cmd.Detail.RoomFloor,
            cmd.Detail.HouseNo,
            cmd.Detail.SubDistrict,
            cmd.Detail.District,
            cmd.Detail.Province,
            cmd.Detail.Latitude,
            cmd.Detail.Longitude,
            cmd.Detail.PlotLocationType,
            cmd.Detail.PlotLocationTypeOther,
            cmd.Detail.PricePerUnit,
            cmd.Detail.OfferingPrice,
            cmd.Detail.SellingPrice,
            cmd.Detail.PhoneNo,
            cmd.Detail.InformationDate,
            cmd.Detail.Website,
            cmd.Detail.SourceUrl,
            cmd.Detail.Remark
        ));

        // No SaveChangesAsync — TransactionalBehavior handles it.
        return new CreateSupportingDetailResult(detail.SupportingDataId, detail.Id);
    }
}