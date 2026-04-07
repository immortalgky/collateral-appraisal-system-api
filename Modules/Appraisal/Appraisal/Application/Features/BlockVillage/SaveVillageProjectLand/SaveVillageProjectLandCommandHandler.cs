using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockVillage.SaveVillageProjectLand;

public class SaveVillageProjectLandCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<SaveVillageProjectLandCommand, SaveVillageProjectLandResult>
{
    public async Task<SaveVillageProjectLandResult> Handle(
        SaveVillageProjectLandCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null || command.Province is not null)
            address = AdministrativeAddress.Create(command.SubDistrict, command.District, command.Province, command.LandOffice);

        // Create or get existing land
        var land = appraisal.VillageProjectLand;
        if (land is null)
        {
            land = VillageProjectLand.Create(command.AppraisalId);
            dbContext.VillageProjectLands.Add(land);
            appraisal.SetVillageProjectLand(land);
        }

        land.Update(
            command.PropertyName,
            command.LandDescription,
            coordinates,
            address,
            command.OwnerName,
            command.IsOwnerVerified,
            command.HasObligation,
            command.ObligationDetails,
            command.IsLandLocationVerified,
            command.LandCheckMethodType,
            command.LandCheckMethodTypeOther,
            command.Street,
            command.Soi,
            command.DistanceFromMainRoad,
            command.Village,
            command.AddressLocation,
            command.LandShapeType,
            command.UrbanPlanningType,
            command.LandZoneType,
            command.LandZoneTypeOther,
            command.PlotLocationType,
            command.PlotLocationTypeOther,
            command.LandFillType,
            command.LandFillTypeOther,
            command.LandFillPercent,
            command.SoilLevel,
            command.AccessRoadWidth,
            command.RightOfWay,
            command.RoadFrontage,
            command.NumberOfSidesFacingRoad,
            command.RoadPassInFrontOfLand,
            command.LandAccessibilityType,
            command.LandAccessibilityRemark,
            command.RoadSurfaceType,
            command.RoadSurfaceTypeOther,
            command.HasElectricity,
            command.ElectricityDistance,
            command.PublicUtilityType,
            command.PublicUtilityTypeOther,
            command.LandUseType,
            command.LandUseTypeOther,
            command.LandEntranceExitType,
            command.LandEntranceExitTypeOther,
            command.TransportationAccessType,
            command.TransportationAccessTypeOther,
            command.PropertyAnticipationType,
            command.PropertyAnticipationTypeOther,
            command.IsExpropriated,
            command.ExpropriationRemark,
            command.IsInExpropriationLine,
            command.ExpropriationLineRemark,
            command.RoyalDecree,
            command.IsEncroached,
            command.EncroachmentRemark,
            command.EncroachmentArea,
            command.IsLandlocked,
            command.LandlockedRemark,
            command.IsForestBoundary,
            command.ForestBoundaryRemark,
            command.OtherLegalLimitations,
            command.EvictionType,
            command.EvictionTypeOther,
            command.AllocationType,
            command.NorthAdjacentArea,
            command.NorthBoundaryLength,
            command.SouthAdjacentArea,
            command.SouthBoundaryLength,
            command.EastAdjacentArea,
            command.EastBoundaryLength,
            command.WestAdjacentArea,
            command.WestBoundaryLength,
            command.PondArea,
            command.PondDepth,
            command.HasBuilding,
            command.HasBuildingOther,
            command.Remark);

        if (command.Titles is not null)
            SyncTitles(land, command.Titles);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveVillageProjectLandResult(land.Id);
    }

    private static void SyncTitles(VillageProjectLand land, List<VillageProjectLandTitleDto> incomingTitles)
    {
        var incomingIds = incomingTitles
            .Where(t => t.Id.HasValue)
            .Select(t => t.Id!.Value)
            .ToHashSet();

        // Remove titles not in incoming list
        var titlesToRemove = land.Titles
            .Where(t => !incomingIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToList();
        foreach (var id in titlesToRemove)
            land.RemoveTitle(id);

        // Add or update
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
                var title = VillageProjectLandTitle.Create(land.Id, dto.TitleNumber, dto.TitleType);
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
