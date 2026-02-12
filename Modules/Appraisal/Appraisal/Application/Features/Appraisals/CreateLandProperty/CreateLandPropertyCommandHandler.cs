namespace Appraisal.Application.Features.Appraisals.CreateLandProperty;

/// <summary>
/// Handler for creating a new land property with detail
/// </summary>
public class CreateLandPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<CreateLandPropertyCommand, CreateLandPropertyResult>
{
    public async Task<CreateLandPropertyResult> Handle(
        CreateLandPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.AddLandProperty();

        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null ||
            command.Province is not null || command.LandOffice is not null)
            address = AdministrativeAddress.Create(
                command.SubDistrict,
                command.District,
                command.Province,
                command.LandOffice);

        var landDetail = property.LandDetail!;

        landDetail.Update(
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

        // Add land titles if provided
        if (command.Titles is { Count: > 0 })
            foreach (var titleData in command.Titles)
            {
                var title = LandTitle.Create(
                    landDetail.Id,
                    titleData.TitleNumber,
                    titleData.TitleType);

                LandArea? area = null;
                if (titleData.Rai.HasValue || titleData.Ngan.HasValue || titleData.SquareWa.HasValue)
                    area = LandArea.Create(titleData.Rai, titleData.Ngan, titleData.SquareWa);

                title.Update(
                    titleData.BookNumber,
                    titleData.PageNumber,
                    titleData.LandParcelNumber,
                    titleData.SurveyNumber,
                    titleData.MapSheetNumber,
                    titleData.Rawang,
                    titleData.AerialMapName,
                    titleData.AerialMapNumber,
                    area,
                    titleData.HasBoundaryMarker,
                    titleData.BoundaryMarkerRemark,
                    titleData.IsDocumentValidated,
                    titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa,
                    titleData.GovernmentPrice,
                    titleData.Remark);

                landDetail.AddTitle(title);
            }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (command.GroupId.HasValue) appraisal.AddPropertyToGroup(command.GroupId.Value, property.Id);

        return new CreateLandPropertyResult(property.Id, landDetail.Id);
    }
}