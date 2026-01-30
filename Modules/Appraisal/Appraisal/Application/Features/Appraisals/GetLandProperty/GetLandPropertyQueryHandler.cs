using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetLandProperty;

/// <summary>
/// Handler for getting a land property by ID
/// </summary>
public class GetLandPropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetLandPropertyQuery, GetLandPropertyResult>
{
    public async Task<GetLandPropertyResult> Handle(
        GetLandPropertyQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            query.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(query.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(query.PropertyId)
            ?? throw new PropertyNotFoundException(query.PropertyId);

        // 3. Validate property type
        if (property.PropertyType != PropertyType.Land)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a land property");

        var landDetail = property.LandDetail;

        // 4. Map to result
        return new GetLandPropertyResult
        {
            PropertyId = property.Id,
            AppraisalId = property.AppraisalId,
            SequenceNumber = property.SequenceNumber,
            PropertyType = property.PropertyType.ToString(),
            Description = property.Description,

            LandDetailId = landDetail?.Id,
            PropertyName = landDetail?.PropertyName,
            LandOffice = landDetail?.Address?.LandOffice,
            LandDescription = landDetail?.LandDescription,
            OwnerName = landDetail?.OwnerName,
            IsOwnerVerified = landDetail?.IsOwnerVerified ?? false,
            HasObligation = landDetail?.HasObligation ?? false,
            ObligationDetails = landDetail?.ObligationDetails,

            Street = landDetail?.Street,
            Soi = landDetail?.Soi,
            Village = landDetail?.Village,
            SubDistrict = landDetail?.Address?.SubDistrict,
            District = landDetail?.Address?.District,
            Province = landDetail?.Address?.Province,

            Latitude = landDetail?.Coordinates?.Latitude,
            Longitude = landDetail?.Coordinates?.Longitude,

            IsLandLocationVerified = landDetail?.IsLandLocationVerified,
            LandCheckMethodType = landDetail?.LandCheckMethodType,
            LandCheckMethodTypeOther = landDetail?.LandCheckMethodTypeOther,
            DistanceFromMainRoad = landDetail?.DistanceFromMainRoad,
            AddressLocation = landDetail?.AddressLocation,
            LandShapeType = landDetail?.LandShapeType,
            UrbanPlanningType = landDetail?.UrbanPlanningType,
            LandZoneType = landDetail?.LandZoneType.Adapt<List<string>>(),
            PlotLocationType = landDetail?.PlotLocationType.Adapt<List<string>>(),
            PlotLocationTypeOther = landDetail?.PlotLocationTypeOther,
            LandFillType = landDetail?.LandFillType,
            LandFillTypeOther = landDetail?.LandFillTypeOther,
            LandFillPercent = landDetail?.LandFillPercent,
            SoilLevel = landDetail?.SoilLevel,

            AccessRoadWidth = landDetail?.AccessRoadWidth,
            RightOfWay = landDetail?.RightOfWay,
            RoadFrontage = landDetail?.RoadFrontage,
            NumberOfSidesFacingRoad = landDetail?.NumberOfSidesFacingRoad,
            RoadPassInFrontOfLand = landDetail?.RoadPassInFrontOfLand,
            LandAccessibilityType = landDetail?.LandAccessibilityType,
            LandAccessibilityRemark = landDetail?.LandAccessibilityRemark,
            RoadSurfaceType = landDetail?.RoadSurfaceType,
            RoadSurfaceTypeOther = landDetail?.RoadSurfaceTypeOther,

            HasElectricity = landDetail?.HasElectricity,
            ElectricityDistance = landDetail?.ElectricityDistance,
            PublicUtilityType = landDetail?.PublicUtilityType.Adapt<List<string>>(),
            PublicUtilityTypeOther = landDetail?.PublicUtilityTypeOther,
            LandUseType = landDetail?.LandUseType.Adapt<List<string>>(),
            LandUseTypeOther = landDetail?.LandUseTypeOther,
            LandEntranceExitType = landDetail?.LandEntranceExitType.Adapt<List<string>>(),
            LandEntranceExitTypeOther = landDetail?.LandEntranceExitTypeOther,
            TransportationAccessType = landDetail?.TransportationAccessType.Adapt<List<string>>(),
            TransportationAccessTypeOther = landDetail?.TransportationAccessTypeOther,
            PropertyAnticipationType = landDetail?.PropertyAnticipationType,

            IsExpropriated = landDetail?.IsExpropriated,
            ExpropriationRemark = landDetail?.ExpropriationRemark,
            IsInExpropriationLine = landDetail?.IsInExpropriationLine,
            ExpropriationLineRemark = landDetail?.ExpropriationLineRemark,
            RoyalDecree = landDetail?.RoyalDecree,
            IsEncroached = landDetail?.IsEncroached,
            EncroachmentRemark = landDetail?.EncroachmentRemark,
            EncroachmentArea = landDetail?.EncroachmentArea,
            IsLandlocked = landDetail?.IsLandlocked,
            LandlockedRemark = landDetail?.LandlockedRemark,
            IsForestBoundary = landDetail?.IsForestBoundary,
            ForestBoundaryRemark = landDetail?.ForestBoundaryRemark,
            OtherLegalLimitations = landDetail?.OtherLegalLimitations,
            EvictionType = landDetail?.EvictionType.Adapt<List<string>>(),
            EvictionTypeOther = landDetail?.EvictionTypeOther,
            AllocationType = landDetail?.AllocationType,

            NorthAdjacentArea = landDetail?.NorthAdjacentArea,
            NorthBoundaryLength = landDetail?.NorthBoundaryLength,
            SouthAdjacentArea = landDetail?.SouthAdjacentArea,
            SouthBoundaryLength = landDetail?.SouthBoundaryLength,
            EastAdjacentArea = landDetail?.EastAdjacentArea,
            EastBoundaryLength = landDetail?.EastBoundaryLength,
            WestAdjacentArea = landDetail?.WestAdjacentArea,
            WestBoundaryLength = landDetail?.WestBoundaryLength,

            PondArea = landDetail?.PondArea,
            PondDepth = landDetail?.PondDepth,
            HasBuilding = landDetail?.HasBuilding,
            HasBuildingOther = landDetail?.HasBuildingOther,
            Remark = landDetail?.Remark
        };
    }
}
