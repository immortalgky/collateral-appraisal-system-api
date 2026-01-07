using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetLandAndBuildingProperty;

/// <summary>
/// Handler for getting a land and building property with its detail
/// </summary>
public class GetLandAndBuildingPropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetLandAndBuildingPropertyQuery, GetLandAndBuildingPropertyResult>
{
    public async Task<GetLandAndBuildingPropertyResult> Handle(
        GetLandAndBuildingPropertyQuery query,
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
        if (property.PropertyType != PropertyType.LandAndBuilding)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a land and building property");

        // 4. Get the detail
        var detail = property.LandAndBuildingDetail
            ?? throw new InvalidOperationException($"Land and building detail not found for property {query.PropertyId}");

        // 5. Map to result
        return new GetLandAndBuildingPropertyResult(
            // Property
            PropertyId: property.Id,
            AppraisalId: property.AppraisalId,
            SequenceNumber: property.SequenceNumber,
            PropertyType: property.PropertyType.ToString(),
            Description: property.Description,
            DetailId: detail.Id,
            // Property Identification
            PropertyName: detail.PropertyName,
            LandDescription: detail.LandDescription,
            Latitude: detail.Coordinates?.Latitude,
            Longitude: detail.Coordinates?.Longitude,
            SubDistrict: detail.Address?.SubDistrict,
            District: detail.Address?.District,
            Province: detail.Address?.Province,
            LandOffice: detail.Address?.LandOffice,
            // Owner Fields
            OwnerName: detail.OwnerName,
            OwnershipType: detail.OwnershipType,
            OwnershipDocument: detail.OwnershipDocument,
            OwnershipPercentage: detail.OwnershipPercentage,
            IsOwnerVerified: detail.IsOwnerVerified,
            HasObligation: detail.HasObligation,
            ObligationDetails: detail.ObligationDetails,
            PropertyUsage: detail.PropertyUsage,
            OccupancyStatus: detail.OccupancyStatus,
            // Land - Title Deed
            TitleDeedType: detail.TitleDeedType,
            TitleDeedNumber: detail.TitleDeedNumber,
            LandNumber: detail.LandNumber,
            SurveyPageNumber: detail.SurveyPageNumber,
            Rai: detail.Area?.Rai,
            Ngan: detail.Area?.Ngan,
            SquareWa: detail.Area?.SquareWa,
            // Land - Document Verification
            LandLocationVerification: detail.LandLocationVerification,
            LandCheckMethod: detail.LandCheckMethod,
            LandCheckMethodOther: detail.LandCheckMethodOther,
            // Land - Location Details
            Street: detail.Street,
            Soi: detail.Soi,
            DistanceFromMainRoad: detail.DistanceFromMainRoad,
            Village: detail.Village,
            AddressLocation: detail.AddressLocation,
            // Land - Characteristics
            LandShape: detail.LandShape,
            UrbanPlanningType: detail.UrbanPlanningType,
            PlotLocation: detail.PlotLocation,
            PlotLocationOther: detail.PlotLocationOther,
            LandFillStatus: detail.LandFillStatus,
            LandFillStatusOther: detail.LandFillStatusOther,
            LandFillPercent: detail.LandFillPercent,
            TerrainType: detail.TerrainType,
            SoilCondition: detail.SoilCondition,
            SoilLevel: detail.SoilLevel,
            FloodRisk: detail.FloodRisk,
            LandUseZoning: detail.LandUseZoning,
            LandUseZoningOther: detail.LandUseZoningOther,
            // Land - Road Access
            AccessRoadType: detail.AccessRoadType,
            AccessRoadWidth: detail.AccessRoadWidth,
            RightOfWay: detail.RightOfWay,
            RoadFrontage: detail.RoadFrontage,
            NumberOfSidesFacingRoad: detail.NumberOfSidesFacingRoad,
            RoadPassInFrontOfLand: detail.RoadPassInFrontOfLand,
            LandAccessibility: detail.LandAccessibility,
            LandAccessibilityDescription: detail.LandAccessibilityDescription,
            RoadSurfaceType: detail.RoadSurfaceType,
            RoadSurfaceTypeOther: detail.RoadSurfaceTypeOther,
            // Land - Utilities
            ElectricityAvailable: detail.ElectricityAvailable,
            ElectricityDistance: detail.ElectricityDistance,
            WaterSupplyAvailable: detail.WaterSupplyAvailable,
            SewerageAvailable: detail.SewerageAvailable,
            PublicUtilities: detail.PublicUtilities,
            PublicUtilitiesOther: detail.PublicUtilitiesOther,
            LandEntranceExit: detail.LandEntranceExit,
            LandEntranceExitOther: detail.LandEntranceExitOther,
            TransportationAccess: detail.TransportationAccess,
            TransportationAccessOther: detail.TransportationAccessOther,
            PropertyAnticipation: detail.PropertyAnticipation,
            // Land - Legal
            IsExpropriated: detail.IsExpropriated,
            ExpropriationRemark: detail.ExpropriationRemark,
            IsInExpropriationLine: detail.IsInExpropriationLine,
            ExpropriationLineRemark: detail.ExpropriationLineRemark,
            RoyalDecree: detail.RoyalDecree,
            IsEncroached: detail.IsEncroached,
            EncroachmentRemark: detail.EncroachmentRemark,
            EncroachmentArea: detail.EncroachmentArea,
            IsLandlocked: detail.IsLandlocked,
            LandlockedRemark: detail.LandlockedRemark,
            IsForestBoundary: detail.IsForestBoundary,
            ForestBoundaryRemark: detail.ForestBoundaryRemark,
            OtherLegalLimitations: detail.OtherLegalLimitations,
            EvictionStatus: detail.EvictionStatus,
            EvictionStatusOther: detail.EvictionStatusOther,
            AllocationStatus: detail.AllocationStatus,
            // Land - Boundaries
            NorthAdjacentArea: detail.NorthAdjacentArea,
            NorthBoundaryLength: detail.NorthBoundaryLength,
            SouthAdjacentArea: detail.SouthAdjacentArea,
            SouthBoundaryLength: detail.SouthBoundaryLength,
            EastAdjacentArea: detail.EastAdjacentArea,
            EastBoundaryLength: detail.EastBoundaryLength,
            WestAdjacentArea: detail.WestAdjacentArea,
            WestBoundaryLength: detail.WestBoundaryLength,
            // Land - Other
            PondArea: detail.PondArea,
            PondDepth: detail.PondDepth,
            // Building - Identification
            BuildingNumber: detail.BuildingNumber,
            ModelName: detail.ModelName,
            BuiltOnTitleNumber: detail.BuiltOnTitleNumber,
            HouseNumber: detail.HouseNumber,
            // Building - Info
            BuildingType: detail.BuildingType,
            BuildingTypeOther: detail.BuildingTypeOther,
            NumberOfBuildings: detail.NumberOfBuildings,
            BuildingAge: detail.BuildingAge,
            ConstructionYear: detail.ConstructionYear,
            IsResidentialRemark: detail.IsResidentialRemark,
            // Building - Status
            BuildingCondition: detail.BuildingCondition,
            IsUnderConstruction: detail.IsUnderConstruction,
            ConstructionCompletionPercent: detail.ConstructionCompletionPercent,
            ConstructionLicenseExpirationDate: detail.ConstructionLicenseExpirationDate,
            IsAppraisable: detail.IsAppraisable,
            MaintenanceStatus: detail.MaintenanceStatus,
            RenovationHistory: detail.RenovationHistory,
            // Building - Area
            TotalBuildingArea: detail.TotalBuildingArea,
            BuildingAreaUnit: detail.BuildingAreaUnit,
            UsableArea: detail.UsableArea,
            // Building - Structure
            NumberOfFloors: detail.NumberOfFloors,
            NumberOfUnits: detail.NumberOfUnits,
            NumberOfBedrooms: detail.NumberOfBedrooms,
            NumberOfBathrooms: detail.NumberOfBathrooms,
            // Building - Style
            BuildingMaterial: detail.BuildingMaterial,
            BuildingStyle: detail.BuildingStyle,
            IsResidential: detail.IsResidential,
            ConstructionStyleType: detail.ConstructionStyleType,
            ConstructionStyleRemark: detail.ConstructionStyleRemark,
            ConstructionType: detail.ConstructionType,
            ConstructionTypeOther: detail.ConstructionTypeOther,
            // Building - Components
            StructureType: detail.StructureType,
            StructureTypeOther: detail.StructureTypeOther,
            FoundationType: detail.FoundationType,
            RoofFrameType: detail.RoofFrameType,
            RoofFrameTypeOther: detail.RoofFrameTypeOther,
            RoofType: detail.RoofType,
            RoofTypeOther: detail.RoofTypeOther,
            RoofMaterial: detail.RoofMaterial,
            CeilingType: detail.CeilingType,
            CeilingTypeOther: detail.CeilingTypeOther,
            InteriorWallType: detail.InteriorWallType,
            InteriorWallTypeOther: detail.InteriorWallTypeOther,
            ExteriorWallType: detail.ExteriorWallType,
            ExteriorWallTypeOther: detail.ExteriorWallTypeOther,
            WallMaterial: detail.WallMaterial,
            FloorMaterial: detail.FloorMaterial,
            FenceType: detail.FenceType,
            FenceTypeOther: detail.FenceTypeOther,
            // Building - Decoration
            DecorationType: detail.DecorationType,
            DecorationTypeOther: detail.DecorationTypeOther,
            // Building - Utilization
            UtilizationType: detail.UtilizationType,
            OtherPurposeUsage: detail.OtherPurposeUsage,
            // Building - Permits
            BuildingPermitNumber: detail.BuildingPermitNumber,
            BuildingPermitDate: detail.BuildingPermitDate,
            HasOccupancyPermit: detail.HasOccupancyPermit,
            // Building - Pricing
            BuildingInsurancePrice: detail.BuildingInsurancePrice,
            SellingPrice: detail.SellingPrice,
            ForcedSalePrice: detail.ForcedSalePrice,
            // Remarks
            LandRemark: detail.LandRemark,
            BuildingRemark: detail.BuildingRemark);
    }
}
