using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CreateLandAndBuildingProperty;

/// <summary>
/// Handler for creating a land and building property with its appraisal detail
/// </summary>
public class CreateLandAndBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreateLandAndBuildingPropertyCommand, CreateLandAndBuildingPropertyResult>
{
    public async Task<CreateLandAndBuildingPropertyResult> Handle(
        CreateLandAndBuildingPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Execute domain operation via aggregate
        var property = appraisal.AddLandAndBuildingProperty(
            command.OwnerName,
            command.OwnershipType,
            command.Description);

        // 3. Build value objects if provided
        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        AdministrativeAddress? address = null;
        if (!string.IsNullOrEmpty(command.SubDistrict) || !string.IsNullOrEmpty(command.District) ||
            !string.IsNullOrEmpty(command.Province) || !string.IsNullOrEmpty(command.LandOffice))
            address = AdministrativeAddress.Create(command.SubDistrict, command.District, command.Province, command.LandOffice);

        LandArea? area = null;
        if (command.Rai.HasValue || command.Ngan.HasValue || command.SquareWa.HasValue)
            area = LandArea.Create(command.Rai, command.Ngan, command.SquareWa);

        // 4. Update detail with additional fields
        property.LandAndBuildingDetail!.Update(
            // Property Identification
            propertyName: command.PropertyName,
            landDescription: command.LandDescription,
            coordinates: coordinates,
            address: address,
            // Owner Fields
            ownershipDocument: command.OwnershipDocument,
            ownershipPercentage: command.OwnershipPercentage,
            isOwnerVerified: command.IsOwnerVerified,
            hasObligation: command.HasObligation,
            obligationDetails: command.ObligationDetails,
            propertyUsage: command.PropertyUsage,
            occupancyStatus: command.OccupancyStatus,
            // Land - Title Deed
            titleDeedType: command.TitleDeedType,
            titleDeedNumber: command.TitleDeedNumber,
            landNumber: command.LandNumber,
            surveyPageNumber: command.SurveyPageNumber,
            area: area,
            // Land - Document Verification
            landLocationVerification: command.LandLocationVerification,
            landCheckMethod: command.LandCheckMethod,
            landCheckMethodOther: command.LandCheckMethodOther,
            // Land - Location Details
            street: command.Street,
            soi: command.Soi,
            distanceFromMainRoad: command.DistanceFromMainRoad,
            village: command.Village,
            addressLocation: command.AddressLocation,
            // Land - Characteristics
            landShape: command.LandShape,
            urbanPlanningType: command.UrbanPlanningType,
            plotLocation: command.PlotLocation,
            plotLocationOther: command.PlotLocationOther,
            landFillStatus: command.LandFillStatus,
            landFillStatusOther: command.LandFillStatusOther,
            landFillPercent: command.LandFillPercent,
            terrainType: command.TerrainType,
            soilCondition: command.SoilCondition,
            soilLevel: command.SoilLevel,
            floodRisk: command.FloodRisk,
            landUseZoning: command.LandUseZoning,
            landUseZoningOther: command.LandUseZoningOther,
            // Land - Road Access
            accessRoadType: command.AccessRoadType,
            accessRoadWidth: command.AccessRoadWidth,
            rightOfWay: command.RightOfWay,
            roadFrontage: command.RoadFrontage,
            numberOfSidesFacingRoad: command.NumberOfSidesFacingRoad,
            roadPassInFrontOfLand: command.RoadPassInFrontOfLand,
            landAccessibility: command.LandAccessibility,
            landAccessibilityDescription: command.LandAccessibilityDescription,
            roadSurfaceType: command.RoadSurfaceType,
            roadSurfaceTypeOther: command.RoadSurfaceTypeOther,
            // Land - Utilities
            electricityAvailable: command.ElectricityAvailable,
            electricityDistance: command.ElectricityDistance,
            waterSupplyAvailable: command.WaterSupplyAvailable,
            sewerageAvailable: command.SewerageAvailable,
            publicUtilities: command.PublicUtilities,
            publicUtilitiesOther: command.PublicUtilitiesOther,
            landEntranceExit: command.LandEntranceExit,
            landEntranceExitOther: command.LandEntranceExitOther,
            transportationAccess: command.TransportationAccess,
            transportationAccessOther: command.TransportationAccessOther,
            propertyAnticipation: command.PropertyAnticipation,
            // Land - Legal
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
            evictionStatus: command.EvictionStatus,
            evictionStatusOther: command.EvictionStatusOther,
            allocationStatus: command.AllocationStatus,
            // Land - Boundaries
            northAdjacentArea: command.NorthAdjacentArea,
            northBoundaryLength: command.NorthBoundaryLength,
            southAdjacentArea: command.SouthAdjacentArea,
            southBoundaryLength: command.SouthBoundaryLength,
            eastAdjacentArea: command.EastAdjacentArea,
            eastBoundaryLength: command.EastBoundaryLength,
            westAdjacentArea: command.WestAdjacentArea,
            westBoundaryLength: command.WestBoundaryLength,
            // Land - Other
            pondArea: command.PondArea,
            pondDepth: command.PondDepth,
            // Building - Identification
            buildingNumber: command.BuildingNumber,
            modelName: command.ModelName,
            builtOnTitleNumber: command.BuiltOnTitleNumber,
            houseNumber: command.HouseNumber,
            // Building - Info
            buildingType: command.BuildingType,
            buildingTypeOther: command.BuildingTypeOther,
            numberOfBuildings: command.NumberOfBuildings,
            buildingAge: command.BuildingAge,
            constructionYear: command.ConstructionYear,
            isResidentialRemark: command.IsResidentialRemark,
            // Building - Status
            buildingCondition: command.BuildingCondition,
            isUnderConstruction: command.IsUnderConstruction,
            constructionCompletionPercent: command.ConstructionCompletionPercent,
            constructionLicenseExpirationDate: command.ConstructionLicenseExpirationDate,
            isAppraisable: command.IsAppraisable,
            maintenanceStatus: command.MaintenanceStatus,
            renovationHistory: command.RenovationHistory,
            // Building - Area
            totalBuildingArea: command.TotalBuildingArea,
            buildingAreaUnit: command.BuildingAreaUnit,
            usableArea: command.UsableArea,
            // Building - Structure
            numberOfFloors: command.NumberOfFloors,
            numberOfUnits: command.NumberOfUnits,
            numberOfBedrooms: command.NumberOfBedrooms,
            numberOfBathrooms: command.NumberOfBathrooms,
            // Building - Style
            buildingMaterial: command.BuildingMaterial,
            buildingStyle: command.BuildingStyle,
            isResidential: command.IsResidential,
            constructionStyleType: command.ConstructionStyleType,
            constructionStyleRemark: command.ConstructionStyleRemark,
            constructionType: command.ConstructionType,
            constructionTypeOther: command.ConstructionTypeOther,
            // Building - Components
            structureType: command.StructureType,
            structureTypeOther: command.StructureTypeOther,
            foundationType: command.FoundationType,
            roofFrameType: command.RoofFrameType,
            roofFrameTypeOther: command.RoofFrameTypeOther,
            roofType: command.RoofType,
            roofTypeOther: command.RoofTypeOther,
            roofMaterial: command.RoofMaterial,
            ceilingType: command.CeilingType,
            ceilingTypeOther: command.CeilingTypeOther,
            interiorWallType: command.InteriorWallType,
            interiorWallTypeOther: command.InteriorWallTypeOther,
            exteriorWallType: command.ExteriorWallType,
            exteriorWallTypeOther: command.ExteriorWallTypeOther,
            wallMaterial: command.WallMaterial,
            floorMaterial: command.FloorMaterial,
            fenceType: command.FenceType,
            fenceTypeOther: command.FenceTypeOther,
            // Building - Decoration
            decorationType: command.DecorationType,
            decorationTypeOther: command.DecorationTypeOther,
            // Building - Utilization
            utilizationType: command.UtilizationType,
            otherPurposeUsage: command.OtherPurposeUsage,
            // Building - Permits
            buildingPermitNumber: command.BuildingPermitNumber,
            buildingPermitDate: command.BuildingPermitDate,
            hasOccupancyPermit: command.HasOccupancyPermit,
            // Building - Pricing
            buildingInsurancePrice: command.BuildingInsurancePrice,
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            // Remarks
            landRemark: command.LandRemark,
            buildingRemark: command.BuildingRemark);

        // 5. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        // 6. Return both IDs
        return new CreateLandAndBuildingPropertyResult(property.Id, property.LandAndBuildingDetail.Id);
    }
}
