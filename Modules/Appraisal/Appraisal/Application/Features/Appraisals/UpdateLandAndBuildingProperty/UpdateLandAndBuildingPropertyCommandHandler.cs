using Appraisal.Application.Features.Appraisals.CreateLandProperty;

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

        // 6b. Sync land titles (null = no-op, empty list = clear all)
        if (command.Titles is not null)
            SyncTitles(landDetail, command.Titles);

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

        // 8. Sync depreciation details (null = no-op, list = sync)
        if (command.DepreciationDetails is not null)
            SyncDepreciationDetails(buildingDetail, command.DepreciationDetails);

        // 8b. Sync surfaces (null = no-op, list = sync)
        if (command.Surfaces is not null)
            SyncSurfaces(buildingDetail, command.Surfaces);

        // 8c. Sync construction inspection (null = clear, provided = upsert)
        // Also clear if building is not under construction
        if (command.ConstructionInspection is null || command.IsUnderConstruction == false)
            ClearConstructionInspection(property);
        else
            SyncConstructionInspection(property, command.ConstructionInspection);

        // 9. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return Unit.Value;
    }

    private static void SyncDepreciationDetails(
        BuildingAppraisalDetail buildingDetail,
        List<DepreciationItemData> incoming)
    {
        var incomingIds = incoming
            .Where(d => d.Id.HasValue)
            .Select(d => d.Id!.Value)
            .ToHashSet();

        // Delete items not in the incoming list
        var toRemove = buildingDetail.DepreciationDetails
            .Where(d => !incomingIds.Contains(d.Id))
            .Select(d => d.Id)
            .ToList();
        foreach (var id in toRemove)
            buildingDetail.RemoveDepreciationDetail(id);

        // Add or update
        foreach (var item in incoming)
        {
            if (item.Id.HasValue)
            {
                // Update existing
                var existing = buildingDetail.DepreciationDetails
                    .FirstOrDefault(d => d.Id == item.Id.Value);
                if (existing is null) continue;

                existing.Update(
                    item.DepreciationMethod, item.AreaDescription, item.Area, item.Year,
                    item.IsBuilding, item.PricePerSqMBeforeDepreciation, item.PriceBeforeDepreciation,
                    item.PricePerSqMAfterDepreciation, item.PriceAfterDepreciation,
                    item.DepreciationYearPct, item.TotalDepreciationPct, item.PriceDepreciation);

                // Replace periods
                existing.ClearPeriods();
                if (item.DepreciationPeriods is { Count: > 0 })
                    foreach (var p in item.DepreciationPeriods)
                        existing.AddPeriod(p.AtYear, p.ToYear, p.DepreciationPerYear,
                            p.TotalDepreciationPct, p.PriceDepreciation);
            }
            else
            {
                // Create new
                var detail = buildingDetail.AddDepreciationDetail(
                    item.DepreciationMethod, item.AreaDescription, item.Area, item.Year,
                    item.IsBuilding, item.PricePerSqMBeforeDepreciation, item.PriceBeforeDepreciation,
                    item.PricePerSqMAfterDepreciation, item.PriceAfterDepreciation,
                    item.DepreciationYearPct, item.TotalDepreciationPct, item.PriceDepreciation);

                if (item.DepreciationPeriods is { Count: > 0 })
                    foreach (var p in item.DepreciationPeriods)
                        detail.AddPeriod(p.AtYear, p.ToYear, p.DepreciationPerYear,
                            p.TotalDepreciationPct, p.PriceDepreciation);
            }
        }
    }

    private static void SyncTitles(LandAppraisalDetail landDetail, List<LandTitleItemData> incomingTitles)
    {
        var incomingIds = incomingTitles
            .Where(t => t.Id.HasValue)
            .Select(t => t.Id!.Value)
            .ToHashSet();

        // Delete titles not in the incoming list
        var titlesToRemove = landDetail.Titles
            .Where(t => !incomingIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToList();
        foreach (var id in titlesToRemove)
            landDetail.RemoveTitle(id);

        // Add or update
        foreach (var titleData in incomingTitles)
        {
            LandArea? area = null;
            if (titleData.Rai.HasValue || titleData.Ngan.HasValue || titleData.SquareWa.HasValue)
                area = LandArea.Create(titleData.Rai, titleData.Ngan, titleData.SquareWa);

            if (titleData.Id.HasValue)
            {
                // Update existing
                var existing = landDetail.Titles.FirstOrDefault(t => t.Id == titleData.Id.Value);
                existing?.Update(
                    titleData.BookNumber, titleData.PageNumber,
                    titleData.LandParcelNumber, titleData.SurveyNumber,
                    titleData.MapSheetNumber, titleData.Rawang,
                    titleData.AerialMapName, titleData.AerialMapNumber,
                    area, titleData.BoundaryMarkerType, titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType, titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa, titleData.GovernmentPrice,
                    titleData.Remark);
            }
            else
            {
                // Create new
                var title = LandTitle.Create(landDetail.Id, titleData.TitleNumber, titleData.TitleType);
                title.Update(
                    titleData.BookNumber, titleData.PageNumber,
                    titleData.LandParcelNumber, titleData.SurveyNumber,
                    titleData.MapSheetNumber, titleData.Rawang,
                    titleData.AerialMapName, titleData.AerialMapNumber,
                    area, titleData.BoundaryMarkerType, titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType, titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa, titleData.GovernmentPrice,
                    titleData.Remark);
                landDetail.AddTitle(title);
            }
        }
    }

    private static void SyncSurfaces(
        BuildingAppraisalDetail buildingDetail,
        List<SurfaceItemData> incoming)
    {
        var incomingIds = incoming
            .Where(s => s.Id.HasValue)
            .Select(s => s.Id!.Value)
            .ToHashSet();

        // Delete surfaces not in the incoming list
        var toRemove = buildingDetail.Surfaces
            .Where(s => !incomingIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToList();
        foreach (var id in toRemove)
            buildingDetail.RemoveSurface(id);

        // Add or update
        foreach (var item in incoming)
        {
            if (item.Id.HasValue)
            {
                var existing = buildingDetail.Surfaces
                    .FirstOrDefault(s => s.Id == item.Id.Value);
                existing?.Update(
                    item.FromFloorNumber, item.ToFloorNumber, item.FloorType,
                    item.FloorStructureType, item.FloorStructureTypeOther,
                    item.FloorSurfaceType, item.FloorSurfaceTypeOther);
            }
            else
            {
                buildingDetail.AddSurface(
                    item.FromFloorNumber, item.ToFloorNumber, item.FloorType,
                    item.FloorStructureType, item.FloorStructureTypeOther,
                    item.FloorSurfaceType, item.FloorSurfaceTypeOther);
            }
        }
    }

    private static void ClearConstructionInspection(AppraisalProperty property)
    {
        if (property.ConstructionInspection is not null)
            property.ClearConstructionInspection();
    }

    private static void SyncConstructionInspection(
        AppraisalProperty property,
        ConstructionInspectionData ci)
    {
        if (property.ConstructionInspection is not null)
        {
            var inspection = property.ConstructionInspection;
            if (ci.IsFullDetail)
            {
                inspection.UpdateFullDetail(ci.TotalValue);
                inspection.ClearWorkDetails();
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
                inspection.UpdateSummary(ci.TotalValue, ci.SummaryDetail,
                    ci.SummaryPreviousProgressPct, ci.SummaryPreviousValue,
                    ci.SummaryCurrentProgressPct, ci.SummaryCurrentValue, ci.Remark);
                if (ci.DocumentId.HasValue)
                    inspection.SetDocument(ci.DocumentId.Value, ci.FileName, ci.FilePath, ci.FileExtension, ci.MimeType, ci.FileSizeBytes);
                else
                    inspection.ClearDocument();
            }
        }
        else
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
}