using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;

namespace Appraisal.Application.Features.Appraisals.CreateLandAndBuildingProperty;

/// <summary>
/// Handler for creating a land and building property with its appraisal detail
/// </summary>
public class CreateLandAndBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
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
        var property = appraisal.AddLandAndBuildingProperty();

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
            command.PropertyName,
            command.LandDescription,
            coordinates,
            address,
            command.OwnerName,
            command.IsOwnerVerified,
            command.HasObligation,
            command.ObligationDetails,
            // Land - Document Verification
            command.IsLandLocationVerified,
            command.LandCheckMethodType,
            command.LandCheckMethodTypeOther,
            // Land - Location Details
            command.Street,
            command.Soi,
            command.DistanceFromMainRoad,
            command.Village,
            command.AddressLocation,
            // Land - Characteristics
            command.LandShapeType,
            command.UrbanPlanningType,
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

        // Add land titles if provided
        if (command.Titles is { Count: > 0 })
            foreach (var titleData in command.Titles)
            {
                var title = LandTitle.Create(
                    property.LandDetail.Id,
                    titleData.TitleNumber,
                    titleData.TitleType);

                LandArea? area = null;
                if (titleData.Rai.HasValue || titleData.Ngan.HasValue || titleData.SquareWa.HasValue)
                    area = LandArea.Create(titleData.Rai, titleData.Ngan, titleData.SquareWa);

                title.Update(
                    titleData.BookNumber,
                    titleData.PageNumber,
                    titleData.LandParcelNumber,
                    titleData.SurveyNumber,
                    titleData.MapSheetNumber,
                    titleData.Rawang,
                    titleData.AerialMapName,
                    titleData.AerialMapNumber,
                    area,
                    titleData.BoundaryMarkerType,
                    titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType,
                    titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa,
                    titleData.GovernmentPrice,
                    titleData.Remark);

                property.LandDetail.AddTitle(title);
            }

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
            // Building - Utilization
            utilizationType: command.UtilizationType,
            utilizationTypeOther: command.UtilizationTypeOther,
            // Building - Pricing
            buildingInsurancePrice: command.BuildingInsurancePrice,
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            remark: command.BuildingRemark);

        // 6. Add depreciation details if provided
        if (command.DepreciationDetails is { Count: > 0 })
            AddDepreciationDetails(property.BuildingDetail, command.DepreciationDetails);

        // 6b. Add surfaces if provided
        if (command.Surfaces is { Count: > 0 })
            AddSurfaces(property.BuildingDetail, command.Surfaces);

        // 6c. Add construction inspection if provided and building is under construction
        if (command.ConstructionInspection is { } ci && command.IsUnderConstruction != false)
            SetConstructionInspection(property, ci);

        // 7. Save aggregate
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (command.GroupId.HasValue)
            appraisal.AddPropertyToGroup(command.GroupId.Value, property.Id);

        // 8. Return property ID and both detail IDs
        return new CreateLandAndBuildingPropertyResult(property.Id, property.LandDetail.Id);
    }

    private static void AddDepreciationDetails(
        BuildingAppraisalDetail buildingDetail,
        List<DepreciationItemData> items)
    {
        foreach (var item in items)
        {
            var dep = buildingDetail.AddDepreciationDetail(
                item.DepreciationMethod, item.AreaDescription, item.Area, item.Year,
                item.IsBuilding, item.PricePerSqMBeforeDepreciation, item.PriceBeforeDepreciation,
                item.PricePerSqMAfterDepreciation, item.PriceAfterDepreciation,
                item.DepreciationYearPct, item.TotalDepreciationPct, item.PriceDepreciation);

            if (item.DepreciationPeriods is { Count: > 0 })
                foreach (var p in item.DepreciationPeriods)
                    dep.AddPeriod(p.AtYear, p.ToYear, p.DepreciationPerYear,
                        p.TotalDepreciationPct, p.PriceDepreciation);
        }
    }

    private static void AddSurfaces(
        BuildingAppraisalDetail buildingDetail,
        List<SurfaceItemData> items)
    {
        foreach (var item in items)
            buildingDetail.AddSurface(
                item.FromFloorNumber, item.ToFloorNumber, item.FloorType,
                item.FloorStructureType, item.FloorStructureTypeOther,
                item.FloorSurfaceType, item.FloorSurfaceTypeOther);
    }

    private static void SetConstructionInspection(
        AppraisalProperty property,
        ConstructionInspectionData ci)
    {
        ConstructionInspection inspection;
        if (ci.IsFullDetail)
        {
            inspection = ConstructionInspection.CreateFullDetail(property.Id, ci.TotalValue);
            if (ci.WorkDetails is { Count: > 0 })
            {
                foreach (var wd in ci.WorkDetails)
                    inspection.AddWorkDetail(wd.ConstructionWorkGroupId, wd.WorkItemName,
                        wd.DisplayOrder, wd.ProportionPct, wd.PreviousProgressPct,
                        wd.CurrentProgressPct, wd.ConstructionWorkItemId);
                inspection.ComputeAllValues();
            }
        }
        else
        {
            inspection = ConstructionInspection.CreateSummary(property.Id, ci.TotalValue,
                ci.SummaryDetail, ci.SummaryPreviousProgressPct, ci.SummaryPreviousValue,
                ci.SummaryCurrentProgressPct, ci.SummaryCurrentValue, ci.Remark);
            if (ci.DocumentId.HasValue)
                inspection.SetDocument(ci.DocumentId.Value, ci.FileName, ci.FilePath, ci.FileExtension, ci.MimeType, ci.FileSizeBytes);
        }

        property.SetConstructionInspection(inspection);
    }
}