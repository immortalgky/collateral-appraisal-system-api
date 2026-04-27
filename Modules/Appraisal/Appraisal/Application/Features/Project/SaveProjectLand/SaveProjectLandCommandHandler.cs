namespace Appraisal.Application.Features.Project.SaveProjectLand;

/// <summary>
/// Handler for saving project land (LandAndBuilding projects only).
/// Domain's GetOrCreateLand() enforces the type guard.
/// </summary>
public class SaveProjectLandCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IProjectRepository projectRepository
) : ICommandHandler<SaveProjectLandCommand, SaveProjectLandResult>
{
    public async Task<SaveProjectLandResult> Handle(
        SaveProjectLandCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        // Build value objects
        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null || command.Province is not null)
            address = AdministrativeAddress.Create(command.SubDistrict, command.District, command.Province, command.LandOffice);

        // Domain guard: throws InvalidOperationException if ProjectType != LandAndBuilding
        var land = project.GetOrCreateLand();

        land.Update(
            command.PropertyName, command.LandDescription, coordinates, address,
            command.OwnerName, command.IsOwnerVerified, command.HasObligation, command.ObligationDetails,
            command.IsLandLocationVerified, command.LandCheckMethodType, command.LandCheckMethodTypeOther,
            command.Street, command.Soi, command.DistanceFromMainRoad, command.Village, command.AddressLocation,
            command.LandShapeType, command.UrbanPlanningType,
            command.LandZoneType, command.LandZoneTypeOther,
            command.PlotLocationType, command.PlotLocationTypeOther,
            command.LandFillType, command.LandFillTypeOther, command.LandFillPercent, command.SoilLevel,
            command.AccessRoadWidth, command.RightOfWay, command.RoadFrontage,
            command.NumberOfSidesFacingRoad, command.RoadPassInFrontOfLand,
            command.LandAccessibilityType, command.LandAccessibilityRemark,
            command.RoadSurfaceType, command.RoadSurfaceTypeOther,
            command.HasElectricity, command.ElectricityDistance,
            command.PublicUtilityType, command.PublicUtilityTypeOther,
            command.LandUseType, command.LandUseTypeOther,
            command.LandEntranceExitType, command.LandEntranceExitTypeOther,
            command.TransportationAccessType, command.TransportationAccessTypeOther,
            command.PropertyAnticipationType, command.PropertyAnticipationTypeOther,
            command.IsExpropriated, command.ExpropriationRemark,
            command.IsInExpropriationLine, command.ExpropriationLineRemark,
            command.RoyalDecree, command.IsEncroached, command.EncroachmentRemark, command.EncroachmentArea,
            command.IsLandlocked, command.LandlockedRemark,
            command.IsForestBoundary, command.ForestBoundaryRemark,
            command.OtherLegalLimitations,
            command.EvictionType, command.EvictionTypeOther, command.AllocationType,
            command.NorthAdjacentArea, command.NorthBoundaryLength,
            command.SouthAdjacentArea, command.SouthBoundaryLength,
            command.EastAdjacentArea, command.EastBoundaryLength,
            command.WestAdjacentArea, command.WestBoundaryLength,
            command.PondArea, command.PondDepth,
            command.HasBuilding, command.HasBuildingOther, command.Remark);

        if (command.Titles is not null)
            SyncTitles(land, command.Titles);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveProjectLandResult(land.Id);
    }

    private static void SyncTitles(ProjectLand land, List<ProjectLandTitleDto> incomingTitles)
    {
        var incomingIds = incomingTitles
            .Where(t => t.Id.HasValue)
            .Select(t => t.Id!.Value)
            .ToHashSet();

        // Remove titles not present in the incoming list
        var titlesToRemove = land.Titles
            .Where(t => !incomingIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToList();
        foreach (var id in titlesToRemove)
            land.RemoveTitle(id);

        foreach (var dto in incomingTitles)
        {
            LandArea? area = null;
            if (dto.Rai.HasValue || dto.Ngan.HasValue || dto.SquareWa.HasValue)
                area = LandArea.Create(dto.Rai, dto.Ngan, dto.SquareWa);

            if (dto.Id.HasValue)
            {
                var existing = land.Titles.FirstOrDefault(t => t.Id == dto.Id.Value);
                existing?.Update(
                    dto.BookNumber, dto.PageNumber, dto.LandParcelNumber, dto.SurveyNumber,
                    dto.MapSheetNumber, dto.Rawang, dto.AerialMapName, dto.AerialMapNumber,
                    area, dto.BoundaryMarkerType, dto.BoundaryMarkerRemark,
                    dto.DocumentValidationResultType, dto.IsMissingFromSurvey,
                    dto.GovernmentPricePerSqWa, dto.GovernmentPrice, dto.Remark);
            }
            else
            {
                var title = ProjectLandTitle.Create(land.Id, dto.TitleNumber, dto.TitleType);
                title.Update(
                    dto.BookNumber, dto.PageNumber, dto.LandParcelNumber, dto.SurveyNumber,
                    dto.MapSheetNumber, dto.Rawang, dto.AerialMapName, dto.AerialMapNumber,
                    area, dto.BoundaryMarkerType, dto.BoundaryMarkerRemark,
                    dto.DocumentValidationResultType, dto.IsMissingFromSurvey,
                    dto.GovernmentPricePerSqWa, dto.GovernmentPrice, dto.Remark);
                land.AddTitle(title);
            }
        }
    }
}
