using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.Shared;
using Shared.Dtos;

namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreementLandProperty;

/// <summary>
/// Handler for getting a lease agreement land property by ID
/// </summary>
public class GetLeaseAgreementLandPropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetLeaseAgreementLandPropertyQuery, GetLeaseAgreementLandPropertyResult>
{
    public async Task<GetLeaseAgreementLandPropertyResult> Handle(
        GetLeaseAgreementLandPropertyQuery query,
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
        if (property.PropertyType != PropertyType.LeaseAgreementLand)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a lease agreement land property");

        var landDetail = property.LandDetail;

        // 4. Map to result
        return new GetLeaseAgreementLandPropertyResult
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
            LandZoneTypeOther = landDetail?.LandZoneTypeOther,
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
            PropertyAnticipationTypeOther = landDetail?.PropertyAnticipationTypeOther,

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
            Remark = landDetail?.Remark,
            TotalLandAreaInSqWa = landDetail?.TotalLandAreaInSqWa ?? 0,

            Titles = landDetail?.Titles.Select(title => new LandTitleItemData(
                title.Id,
                title.TitleNumber,
                title.TitleType,
                title.BookNumber,
                title.PageNumber,
                title.LandParcelNumber,
                title.SurveyNumber,
                title.MapSheetNumber,
                title.Rawang,
                title.AerialMapName,
                title.AerialMapNumber,
                title.Area?.Rai,
                title.Area?.Ngan,
                title.Area?.SquareWa,
                title.BoundaryMarkerType,
                title.BoundaryMarkerRemark,
                title.DocumentValidationResultType,
                title.IsMissingFromSurvey,
                title.GovernmentPricePerSqWa,
                title.GovernmentPrice,
                title.Remark
            )).ToList(),

            // Lease Agreement & Rental Info
            LeaseAgreement = LeaseAgreementMapper.MapLeaseAgreement(property.LeaseAgreementDetail),
            RentalInfo = LeaseAgreementMapper.MapRentalInfo(property.RentalInfo),
        };
    }
}
