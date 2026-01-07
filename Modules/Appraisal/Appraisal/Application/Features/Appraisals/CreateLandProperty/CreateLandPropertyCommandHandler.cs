namespace Appraisal.Application.Features.Appraisals.CreateLandProperty;

/// <summary>
/// Handler for creating a new land property with detail
/// </summary>
public class CreateLandPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreateLandPropertyCommand, CreateLandPropertyResult>
{
    public async Task<CreateLandPropertyResult> Handle(
        CreateLandPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Execute domain operation via aggregate (creates BOTH property + detail)
        var property = appraisal.AddLandProperty(
            command.OwnerName,
            command.Description);

        // 3. Create value objects if provided
        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
        {
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);
        }

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null ||
            command.Province is not null || command.LandOffice is not null)
        {
            address = AdministrativeAddress.Create(
                command.SubDistrict,
                command.District,
                command.Province,
                command.LandOffice);
        }

        // 4. Update detail with additional fields
        property.LandDetail!.Update(
            propertyName: command.PropertyName,
            landDescription: command.LandDescription,
            coordinates: coordinates,
            address: address,
            isOwnerVerified: command.IsOwnerVerified,
            hasObligation: command.HasObligation,
            obligationDetails: command.ObligationDetails,
            isLandLocationVerified: command.IsLandLocationVerified,
            landCheckMethodType: command.LandCheckMethodType,
            landCheckMethodTypeOther: command.LandCheckMethodTypeOther,
            street: command.Street,
            soi: command.Soi,
            distanceFromMainRoad: command.DistanceFromMainRoad,
            village: command.Village,
            addressLocation: command.AddressLocation,
            landShapeType: command.LandShapeType,
            urbanPlanningType: command.UrbanPlanningType,
            plotLocationType: command.PlotLocationType,
            plotLocationTypeOther: command.PlotLocationTypeOther,
            landFillStatusType: command.LandFillStatusType,
            landFillStatusTypeOther: command.LandFillStatusTypeOther,
            landFillPercent: command.LandFillPercent,
            soilLevel: command.SoilLevel,
            accessRoadWidth: command.AccessRoadWidth,
            rightOfWay: command.RightOfWay,
            roadFrontage: command.RoadFrontage,
            numberOfSidesFacingRoad: command.NumberOfSidesFacingRoad,
            roadPassInFrontOfLand: command.RoadPassInFrontOfLand,
            landAccessibilityType: command.LandAccessibilityType,
            landAccessibilityRemark: command.LandAccessibilityRemark,
            roadSurfaceType: command.RoadSurfaceType,
            roadSurfaceTypeOther: command.RoadSurfaceTypeOther,
            hasElectricity: command.HasElectricity,
            electricityDistance: command.ElectricityDistance,
            publicUtilityType: command.PublicUtilityType,
            publicUtilityTypeOther: command.PublicUtilityTypeOther,
            landUseType: command.LandUseType,
            landUseTypeOther: command.LandUseTypeOther,
            landEntranceExitType: command.LandEntranceExitType,
            landEntranceExitTypeOther: command.LandEntranceExitTypeOther,
            transportationAccessType: command.TransportationAccessType,
            transportationAccessTypeOther: command.TransportationAccessTypeOther,
            propertyAnticipationType: command.PropertyAnticipationType,
            isExpropriated: command.IsExpropriated,
            expropriationRemark: command.ExpropriationRemark,
            isInExpropriationLine: command.IsInExpropriationLine,
            expropriationLineRemark: command.ExpropriationLineRemark,
            royalDecree: command.RoyalDecree,
            isEncroached: command.IsEncroached,
            encroachmentRemark: command.EncroachmentRemark,
            encroachmentArea: command.EncroachmentArea,
            isLandlocked: command.IsLandlocked,
            landlockedRemark: command.LandlockedRemark,
            isForestBoundary: command.IsForestBoundary,
            forestBoundaryRemark: command.ForestBoundaryRemark,
            otherLegalLimitations: command.OtherLegalLimitations,
            evictionStatusType: command.EvictionStatusType,
            evictionStatusTypeOther: command.EvictionStatusTypeOther,
            allocationStatusType: command.AllocationStatusType,
            northAdjacentArea: command.NorthAdjacentArea,
            northBoundaryLength: command.NorthBoundaryLength,
            southAdjacentArea: command.SouthAdjacentArea,
            southBoundaryLength: command.SouthBoundaryLength,
            eastAdjacentArea: command.EastAdjacentArea,
            eastBoundaryLength: command.EastBoundaryLength,
            westAdjacentArea: command.WestAdjacentArea,
            westBoundaryLength: command.WestBoundaryLength,
            pondArea: command.PondArea,
            pondDepth: command.PondDepth,
            hasBuilding: command.HasBuilding,
            hasBuildingOther: command.HasBuildingOther,
            remark: command.Remark);

        // 5. Save aggregate (UoW pattern)
        //await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        // 6. Return property ID and detail ID
        return new CreateLandPropertyResult(property.Id, property.LandDetail.Id);
    }
}
