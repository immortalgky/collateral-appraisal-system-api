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
            address = AdministrativeAddress.Create(command.SubDistrict, command.District, command.Province,
                command.LandOffice);

        // 4. Update Land detail with additional fields
        property.LandDetail!.Update(
            // Property Identification
            propertyName: command.PropertyName,
            landDescription: command.LandDescription,
            coordinates: coordinates,
            address: address,
            isOwnerVerified: command.IsOwnerVerified,
            hasObligation: command.HasObligation,
            obligationDetails: command.ObligationDetails,
            // Land - Document Verification
            isLandLocationVerified: command.IsLandLocationVerified,
            landCheckMethodType: command.LandCheckMethod,
            landCheckMethodTypeOther: command.LandCheckMethodOther,
            // Land - Location Details
            street: command.Street,
            soi: command.Soi,
            distanceFromMainRoad: command.DistanceFromMainRoad,
            village: command.Village,
            addressLocation: command.AddressLocation,
            // Land - Characteristics
            landShapeType: command.LandShape,
            urbanPlanningType: command.UrbanPlanningType,
            plotLocationType: command.PlotLocationType,
            plotLocationTypeOther: command.PlotLocationTypeOther,
            landFillType: command.LandFillStatus,
            landFillTypeOther: command.LandFillStatusOther,
            landFillPercent: command.LandFillPercent,
            soilLevel: command.SoilLevel,
            accessRoadWidth: command.AccessRoadWidth,
            rightOfWay: command.RightOfWay,
            roadFrontage: command.RoadFrontage,
            numberOfSidesFacingRoad: command.NumberOfSidesFacingRoad,
            roadPassInFrontOfLand: command.RoadPassInFrontOfLand,
            landAccessibilityType: command.LandAccessibility,
            landAccessibilityRemark: command.LandAccessibilityDescription,
            roadSurfaceType: command.RoadSurfaceType,
            roadSurfaceTypeOther: command.RoadSurfaceTypeOther,
            // Land - Utilities
            hasElectricity: command.ElectricityAvailable,
            electricityDistance: command.ElectricityDistance,
            publicUtilityType: command.PublicUtility,
            publicUtilityTypeOther: command.PublicUtilityOther,
            landEntranceExitType: command.LandEntranceExitType,
            landEntranceExitTypeOther: command.LandEntranceExitTypeOther,
            transportationAccessType: command.TransportationAccessType,
            transportationAccessTypeOther: command.TransportationAccessTypeOther,
            propertyAnticipationType: command.PropertyAnticipation,
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
            evictionType: command.EvictionType,
            evictionTypeOther: command.EvictionTypeOther,
            allocationType: command.AllocationStatus,
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
            remark: command.LandRemark);

        // 5. Update Building detail with additional fields
        property.BuildingDetail!.Update(
            // Building - Identification
            buildingNumber: command.BuildingNumber,
            modelName: command.ModelName,
            builtOnTitleNumber: command.BuiltOnTitleNumber,
            houseNumber: command.HouseNumber,
            isOwnerVerified: command.IsOwnerVerified,
            hasObligation: command.HasObligation,
            obligationDetails: command.ObligationDetails,
            // Building - Info
            buildingType: command.BuildingType,
            buildingTypeOther: command.BuildingTypeOther,
            buildingAge: command.BuildingAge,
            constructionYear: command.ConstructionYear,
            isResidentialRemark: command.IsResidentialRemark,
            // Building - Status
            buildingCondition: command.BuildingCondition,
            isUnderConstruction: command.IsUnderConstruction,
            constructionCompletionPercent: command.ConstructionCompletionPercent,
            constructionLicenseExpirationDate: command.ConstructionLicenseExpirationDate,
            isAppraisable: command.IsAppraisable,
            // Building - Area
            totalBuildingArea: command.TotalBuildingArea,
            // Building - Structure
            numberOfFloors: command.NumberOfFloors,
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
            roofFrameType: command.RoofFrameType,
            roofFrameTypeOther: command.RoofFrameTypeOther,
            roofType: command.RoofType,
            roofTypeOther: command.RoofTypeOther,
            ceilingType: command.CeilingType,
            ceilingTypeOther: command.CeilingTypeOther,
            interiorWallType: command.InteriorWallType,
            interiorWallTypeOther: command.InteriorWallTypeOther,
            exteriorWallType: command.ExteriorWallType,
            exteriorWallTypeOther: command.ExteriorWallTypeOther,
            fenceType: command.FenceType,
            fenceTypeOther: command.FenceTypeOther,
            // Building - Decoration
            decorationType: command.DecorationType,
            decorationTypeOther: command.DecorationTypeOther,
            // Building - Utilization
            utilizationType: command.UtilizationType,
            otherPurposeUsage: command.OtherPurposeUsage,
            // Building - Pricing
            buildingInsurancePrice: command.BuildingInsurancePrice,
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            remark: command.BuildingRemark);

        // 6. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        // 7. Return property ID and both detail IDs
        return new CreateLandAndBuildingPropertyResult(property.Id, property.LandDetail.Id);
    }
}