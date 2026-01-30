namespace Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;

/// <summary>
/// Handler for updating a land and building property detail
/// </summary>
public class UpdateLandAndBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateLandAndBuildingPropertyCommand>
{
    public async Task<Unit> Handle(
        UpdateLandAndBuildingPropertyCommand command,
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
        if (property.PropertyType != PropertyType.LandAndBuilding)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a land and building property");

        // 4. Get the detail records
        var landDetail = property.LandDetail
                         ?? throw new InvalidOperationException(
                             $"Land detail not found for property {command.PropertyId}");
        var buildingDetail = property.BuildingDetail
                             ?? throw new InvalidOperationException(
                                 $"Building detail not found for property {command.PropertyId}");

        // 5. Build value objects if provided
        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        AdministrativeAddress? address = null;
        if (!string.IsNullOrEmpty(command.SubDistrict) || !string.IsNullOrEmpty(command.District) ||
            !string.IsNullOrEmpty(command.Province) || !string.IsNullOrEmpty(command.LandOffice))
            address = AdministrativeAddress.Create(command.SubDistrict, command.District, command.Province,
                command.LandOffice);

        // 6. Update Land detail via domain method
        landDetail.Update(
            // Property Identification
            propertyName: command.PropertyName,
            landDescription: command.LandDescription,
            coordinates: coordinates,
            address: address,
            ownerName: command.OwnerName,
            isOwnerVerified: command.IsOwnerVerified,
            hasObligation: command.HasObligation,
            obligationDetails: command.ObligationDetails,
            // Land - Document Verification
            isLandLocationVerified: command.IsLandLocationVerified,
            landCheckMethodType: command.LandCheckMethodType,
            landCheckMethodTypeOther: command.LandCheckMethodTypeOther,
            // Land - Location Details
            street: command.Street,
            soi: command.Soi,
            distanceFromMainRoad: command.DistanceFromMainRoad,
            village: command.Village,
            addressLocation: command.AddressLocation,
            // Land - Characteristics
            landShapeType: command.LandShapeType,
            urbanPlanningType: command.UrbanPlanningType,
            landZoneType: command.LandZoneType,
            plotLocationType: command.PlotLocationType,
            plotLocationTypeOther: command.PlotLocationTypeOther,
            landFillType: command.LandFillType,
            landFillTypeOther: command.LandFillTypeOther,
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
            // Land - Utilities
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
            allocationType: command.AllocationType,
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

        // 7. Update Building detail via domain method
        buildingDetail.Update(
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
            residentialRemark: command.ResidentialRemark,
            // Building - Status
            buildingConditionType: command.BuildingConditionType,
            isUnderConstruction: command.IsUnderConstruction,
            constructionCompletionPercent: command.ConstructionCompletionPercent,
            constructionLicenseExpirationDate: command.ConstructionLicenseExpirationDate,
            isAppraisable: command.IsAppraisable,
            // Building - Area
            totalBuildingArea: command.TotalBuildingArea,
            // Building - Structure
            numberOfFloors: command.NumberOfFloors,
            // Building - Style
            buildingMaterialType: command.BuildingMaterialType,
            buildingStyleType: command.BuildingStyleType,
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
            isEncroachingOthers: command.IsEncroachingOthers,
            encroachingOthersArea: command.EncroachingOthersArea,
            encroachingOthersRemark: command.EncroachingOthersRemark,
            // Building - Utilization
            utilizationType: command.UtilizationType,
            utilizationTypeOther: command.UtilizationTypeOther,
            // Building - Pricing
            buildingInsurancePrice: command.BuildingInsurancePrice,
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            remark: command.BuildingRemark);

        // 8. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return Unit.Value;
    }
}