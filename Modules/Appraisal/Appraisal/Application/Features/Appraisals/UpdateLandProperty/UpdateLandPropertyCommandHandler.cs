namespace Appraisal.Application.Features.Appraisals.UpdateLandProperty;

/// <summary>
/// Handler for updating a land property detail
/// </summary>
public class UpdateLandPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateLandPropertyCommand>
{
    public async Task<Unit> Handle(
        UpdateLandPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.GetProperty(command.PropertyId)
                       ?? throw new PropertyNotFoundException(command.PropertyId);

        if (property.PropertyType != PropertyType.Land)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a land property");

        var landDetail = property.LandDetail
                         ?? throw new InvalidOperationException(
                             $"Land detail not found for property {command.PropertyId}");

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

        return Unit.Value;
    }
}