using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateLandProperty;

/// <summary>
/// Handler for updating a land property detail
/// </summary>
public class UpdateLandPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateLandPropertyCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateLandPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(command.PropertyId)
            ?? throw new PropertyNotFoundException(command.PropertyId);

        // 3. Validate property type
        if (property.PropertyType != PropertyType.Land)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a land property");

        // 4. Get the land detail
        var landDetail = property.LandDetail
            ?? throw new InvalidOperationException($"Land detail not found for property {command.PropertyId}");

        // 5. Create value objects if coordinates or address provided
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

        // 6. Update via domain method
        landDetail.Update(
            propertyName: command.PropertyName,
            landDescription: command.LandDescription,
            coordinates: coordinates,
            address: address,
            ownerName: command.OwnerName,
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

        // 7. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return MediatR.Unit.Value;
    }
}
