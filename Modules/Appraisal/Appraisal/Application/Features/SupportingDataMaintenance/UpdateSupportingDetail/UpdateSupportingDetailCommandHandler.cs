namespace Appraisal.Application.Features.SupportingDataMaintenance.UpdateSupportingDetail;

internal class UpdateSupportingDetailCommandHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService)
    : ICommandHandler<UpdateSupportingDetailCommand, UpdateSupportingDetailResult>
{
    public async Task<UpdateSupportingDetailResult> Handle(
        UpdateSupportingDetailCommand cmd, CancellationToken ct)
    {
        if (!currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT"))
            throw new UnauthorizedAccessException("You are not allowed to edit supporting data.");

        var supportingData = await repo.GetByIdWithDetailsAsync(cmd.SupportingId, ct)
            ?? throw new SupportingDataNotFoundException(cmd.SupportingId);

        supportingData.UpdateDetail(cmd.Id, new SupportingDataDetailData(
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
        return new UpdateSupportingDetailResult(cmd.SupportingId, cmd.Id);
    }
}
