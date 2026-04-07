namespace Appraisal.Application.Features.BlockVillage.GetVillageProjectLand;

public class GetVillageProjectLandQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetVillageProjectLandQuery, GetVillageProjectLandResult>
{
    public async Task<GetVillageProjectLandResult> Handle(
        GetVillageProjectLandQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var land = appraisal.VillageProjectLand;
        if (land is null)
            return null!;

        var titles = land.Titles.Select(t => new VillageProjectLandTitleResultDto(
            Id: t.Id,
            TitleNumber: t.TitleNumber,
            TitleType: t.TitleType,
            BookNumber: t.BookNumber,
            PageNumber: t.PageNumber,
            LandParcelNumber: t.LandParcelNumber,
            SurveyNumber: t.SurveyNumber,
            MapSheetNumber: t.MapSheetNumber,
            Rawang: t.Rawang,
            AerialMapName: t.AerialMapName,
            AerialMapNumber: t.AerialMapNumber,
            Rai: t.Area?.Rai,
            Ngan: t.Area?.Ngan,
            SquareWa: t.Area?.SquareWa,
            BoundaryMarkerType: t.BoundaryMarkerType,
            BoundaryMarkerRemark: t.BoundaryMarkerRemark,
            DocumentValidationResultType: t.DocumentValidationResultType,
            IsMissingFromSurvey: t.IsMissingFromSurvey,
            GovernmentPricePerSqWa: t.GovernmentPricePerSqWa,
            GovernmentPrice: t.GovernmentPrice,
            Remark: t.Remark
        )).ToList();

        return new GetVillageProjectLandResult(
            Id: land.Id,
            AppraisalId: land.AppraisalId,
            PropertyName: land.PropertyName,
            LandDescription: land.LandDescription,
            Latitude: land.Coordinates?.Latitude,
            Longitude: land.Coordinates?.Longitude,
            SubDistrict: land.Address?.SubDistrict,
            District: land.Address?.District,
            Province: land.Address?.Province,
            LandOffice: land.Address?.LandOffice,
            OwnerName: land.OwnerName,
            IsOwnerVerified: land.IsOwnerVerified,
            HasObligation: land.HasObligation,
            ObligationDetails: land.ObligationDetails,
            IsLandLocationVerified: land.IsLandLocationVerified,
            LandCheckMethodType: land.LandCheckMethodType,
            LandCheckMethodTypeOther: land.LandCheckMethodTypeOther,
            Street: land.Street,
            Soi: land.Soi,
            DistanceFromMainRoad: land.DistanceFromMainRoad,
            Village: land.Village,
            AddressLocation: land.AddressLocation,
            LandShapeType: land.LandShapeType,
            UrbanPlanningType: land.UrbanPlanningType,
            LandZoneType: land.LandZoneType,
            LandZoneTypeOther: land.LandZoneTypeOther,
            PlotLocationType: land.PlotLocationType,
            PlotLocationTypeOther: land.PlotLocationTypeOther,
            LandFillType: land.LandFillType,
            LandFillTypeOther: land.LandFillTypeOther,
            LandFillPercent: land.LandFillPercent,
            SoilLevel: land.SoilLevel,
            AccessRoadWidth: land.AccessRoadWidth,
            RightOfWay: land.RightOfWay,
            RoadFrontage: land.RoadFrontage,
            NumberOfSidesFacingRoad: land.NumberOfSidesFacingRoad,
            RoadPassInFrontOfLand: land.RoadPassInFrontOfLand,
            LandAccessibilityType: land.LandAccessibilityType,
            LandAccessibilityRemark: land.LandAccessibilityRemark,
            RoadSurfaceType: land.RoadSurfaceType,
            RoadSurfaceTypeOther: land.RoadSurfaceTypeOther,
            HasElectricity: land.HasElectricity,
            ElectricityDistance: land.ElectricityDistance,
            PublicUtilityType: land.PublicUtilityType,
            PublicUtilityTypeOther: land.PublicUtilityTypeOther,
            LandUseType: land.LandUseType,
            LandUseTypeOther: land.LandUseTypeOther,
            LandEntranceExitType: land.LandEntranceExitType,
            LandEntranceExitTypeOther: land.LandEntranceExitTypeOther,
            TransportationAccessType: land.TransportationAccessType,
            TransportationAccessTypeOther: land.TransportationAccessTypeOther,
            PropertyAnticipationType: land.PropertyAnticipationType,
            PropertyAnticipationTypeOther: land.PropertyAnticipationTypeOther,
            IsExpropriated: land.IsExpropriated,
            ExpropriationRemark: land.ExpropriationRemark,
            IsInExpropriationLine: land.IsInExpropriationLine,
            ExpropriationLineRemark: land.ExpropriationLineRemark,
            RoyalDecree: land.RoyalDecree,
            IsEncroached: land.IsEncroached,
            EncroachmentRemark: land.EncroachmentRemark,
            EncroachmentArea: land.EncroachmentArea,
            IsLandlocked: land.IsLandlocked,
            LandlockedRemark: land.LandlockedRemark,
            IsForestBoundary: land.IsForestBoundary,
            ForestBoundaryRemark: land.ForestBoundaryRemark,
            OtherLegalLimitations: land.OtherLegalLimitations,
            EvictionType: land.EvictionType,
            EvictionTypeOther: land.EvictionTypeOther,
            AllocationType: land.AllocationType,
            NorthAdjacentArea: land.NorthAdjacentArea,
            NorthBoundaryLength: land.NorthBoundaryLength,
            SouthAdjacentArea: land.SouthAdjacentArea,
            SouthBoundaryLength: land.SouthBoundaryLength,
            EastAdjacentArea: land.EastAdjacentArea,
            EastBoundaryLength: land.EastBoundaryLength,
            WestAdjacentArea: land.WestAdjacentArea,
            WestBoundaryLength: land.WestBoundaryLength,
            PondArea: land.PondArea,
            PondDepth: land.PondDepth,
            HasBuilding: land.HasBuilding,
            HasBuildingOther: land.HasBuildingOther,
            Remark: land.Remark,
            TotalLandAreaInSqWa: land.TotalLandAreaInSqWa,
            Titles: titles);
    }
}
