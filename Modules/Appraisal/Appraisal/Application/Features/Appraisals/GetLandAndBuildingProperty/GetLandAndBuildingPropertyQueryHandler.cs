using Appraisal.Application.Features.Appraisals.CreateLandProperty;

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

        // 4. Get the detail records
        var landDetail = property.LandDetail
                         ?? throw new InvalidOperationException(
                             $"Land detail not found for property {query.PropertyId}");
        var buildingDetail = property.BuildingDetail
                             ?? throw new InvalidOperationException(
                                 $"Building detail not found for property {query.PropertyId}");

        // 5. Map surfaces
        var surfaceDtos = buildingDetail.Surfaces
            .OrderBy(s => s.FromFloorNumber).ThenBy(s => s.ToFloorNumber)
            .Select(s => new BuildingAppraisalSurfaceDto(
                s.Id, s.FromFloorNumber, s.ToFloorNumber, s.FloorType,
                s.FloorStructureType, s.FloorStructureTypeOther,
                s.FloorSurfaceType, s.FloorSurfaceTypeOther
            )).ToList();

        // 5b. Map depreciation details
        var depreciationDtos = buildingDetail.DepreciationDetails
            .OrderBy(d => d.CreatedAt)
            .Select(d => new BuildingAppraisalDepreciationDetailDto(
                d.Id,
                d.AreaDescription,
                d.Area,
                d.PricePerSqMBeforeDepreciation,
                d.PriceBeforeDepreciation,
                d.Year,
                d.IsBuilding,
                d.DepreciationMethod,
                d.DepreciationYearPct,
                d.TotalDepreciationPct,
                d.PriceDepreciation,
                d.PricePerSqMAfterDepreciation,
                d.PriceAfterDepreciation,
                d.DepreciationPeriods
                    .OrderBy(p => p.AtYear).ThenBy(p => p.ToYear)
                    .Select(p => new BuildingAppraisalDepreciationPeriodDto(
                        p.Id, p.AtYear, p.ToYear, p.DepreciationPerYear,
                        p.TotalDepreciationPct, p.PriceDepreciation
                    )).ToList()
            )).ToList();

        var constructionDto = property.ConstructionInspection is { } ci
            ? new ConstructionInspectionDto(
                ci.Id, ci.AppraisalPropertyId, ci.IsFullDetail, ci.TotalValue,
                ci.SummaryDetail, ci.SummaryPreviousProgressPct, ci.SummaryPreviousValue,
                ci.SummaryCurrentProgressPct, ci.SummaryCurrentValue, ci.Remark,
                ci.DocumentId, ci.FileName, ci.FilePath,
                ci.FileExtension, ci.MimeType, ci.FileSizeBytes,
                ci.WorkDetails.OrderBy(w => w.DisplayOrder).Select(w => new ConstructionWorkDetailDto(
                    w.Id, w.ConstructionWorkGroupId, w.ConstructionWorkItemId, w.WorkItemName,
                    w.DisplayOrder, w.ConstructionValue, w.PreviousProgressPct, w.CurrentProgressPct,
                    w.ProportionPct, w.CurrentProportionPct, w.PreviousPropertyValue, w.CurrentPropertyValue
                )).ToList())
            : null;

        // 6. Map to result (combining data from both Land and Building details)
        return new GetLandAndBuildingPropertyResult(
            // Property
            property.Id,
            property.AppraisalId,
            property.SequenceNumber,
            property.PropertyType.ToString(),
            property.Description,
            landDetail.Id,
            // Property Identification (from Land)
            landDetail.PropertyName,
            landDetail.LandDescription,
            landDetail.Coordinates?.Latitude,
            landDetail.Coordinates?.Longitude,
            landDetail.Address?.SubDistrict,
            landDetail.Address?.District,
            landDetail.Address?.Province,
            landDetail.Address?.LandOffice,
            // Owner Fields (from Land)
            landDetail.OwnerName,
            landDetail.IsOwnerVerified,
            landDetail.HasObligation,
            landDetail.ObligationDetails,
            // Land - Document Verification
            landDetail.IsLandLocationVerified,
            landDetail.LandCheckMethodType,
            landDetail.LandCheckMethodTypeOther,
            // Land - Location Details
            landDetail.Street,
            landDetail.Soi,
            landDetail.DistanceFromMainRoad,
            landDetail.Village,
            landDetail.AddressLocation,
            // Land - Characteristics
            landDetail.LandShapeType,
            landDetail.UrbanPlanningType,
            landDetail.LandZoneType,
            landDetail.PlotLocationType,
            landDetail.PlotLocationTypeOther,
            landDetail.LandFillType,
            landDetail.LandFillTypeOther,
            landDetail.LandFillPercent,
            landDetail.SoilLevel,
            // Land - Road Access
            landDetail.AccessRoadWidth,
            landDetail.RightOfWay,
            landDetail.RoadFrontage,
            landDetail.NumberOfSidesFacingRoad,
            landDetail.RoadPassInFrontOfLand,
            landDetail.LandAccessibilityType,
            landDetail.LandAccessibilityRemark,
            landDetail.RoadSurfaceType,
            landDetail.RoadSurfaceTypeOther,
            // Land - Utilities
            landDetail.HasElectricity,
            landDetail.ElectricityDistance,
            landDetail.PublicUtilityType,
            landDetail.PublicUtilityTypeOther,
            landDetail.LandUseType,
            landDetail.LandUseTypeOther,
            landDetail.LandEntranceExitType,
            landDetail.LandEntranceExitTypeOther,
            landDetail.TransportationAccessType,
            landDetail.TransportationAccessTypeOther,
            landDetail.PropertyAnticipationType,
            // Land - Legal
            landDetail.IsExpropriated,
            landDetail.ExpropriationRemark,
            landDetail.IsInExpropriationLine,
            landDetail.ExpropriationLineRemark,
            landDetail.RoyalDecree,
            landDetail.IsEncroached,
            landDetail.EncroachmentRemark,
            landDetail.EncroachmentArea,
            landDetail.IsLandlocked,
            landDetail.LandlockedRemark,
            landDetail.IsForestBoundary,
            landDetail.ForestBoundaryRemark,
            landDetail.OtherLegalLimitations,
            landDetail.EvictionType,
            landDetail.EvictionTypeOther,
            landDetail.AllocationType,
            // Land - Boundaries
            landDetail.NorthAdjacentArea,
            landDetail.NorthBoundaryLength,
            landDetail.SouthAdjacentArea,
            landDetail.SouthBoundaryLength,
            landDetail.EastAdjacentArea,
            landDetail.EastBoundaryLength,
            landDetail.WestAdjacentArea,
            landDetail.WestBoundaryLength,
            // Land - Other
            landDetail.PondArea,
            landDetail.PondDepth,
            landDetail.HasBuilding,
            landDetail.HasBuildingOther,
            landDetail.TotalLandAreaInSqWa,
            // Land titles
            landDetail.Titles.Select(title => new LandTitleItemData(
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

            // Building - Identification (from Building)
            buildingDetail.BuildingNumber,
            buildingDetail.ModelName,
            buildingDetail.BuiltOnTitleNumber,
            buildingDetail.HouseNumber,
            // Building - Status
            buildingDetail.BuildingConditionType,
            buildingDetail.IsUnderConstruction,
            buildingDetail.ConstructionCompletionPercent,
            buildingDetail.ConstructionLicenseExpirationDate,
            buildingDetail.IsAppraisable,
            // Building Info
            buildingDetail.BuildingType,
            buildingDetail.BuildingTypeOther,
            buildingDetail.NumberOfFloors,
            buildingDetail.DecorationType,
            buildingDetail.DecorationTypeOther,
            buildingDetail.IsEncroachingOthers,
            buildingDetail.EncroachingOthersRemark,
            buildingDetail.EncroachingOthersArea,
            // Construction Details
            buildingDetail.BuildingMaterialType,
            buildingDetail.BuildingStyleType,
            buildingDetail.IsResidential,
            buildingDetail.BuildingAge,
            buildingDetail.ConstructionYear,
            buildingDetail.ResidentialRemark,
            buildingDetail.ConstructionStyleType,
            buildingDetail.ConstructionStyleRemark,
            // Structure Components
            buildingDetail.StructureType,
            buildingDetail.StructureTypeOther,
            buildingDetail.RoofFrameType,
            buildingDetail.RoofFrameTypeOther,
            buildingDetail.RoofType,
            buildingDetail.RoofTypeOther,
            buildingDetail.CeilingType,
            buildingDetail.CeilingTypeOther,
            buildingDetail.InteriorWallType,
            buildingDetail.InteriorWallTypeOther,
            buildingDetail.ExteriorWallType,
            buildingDetail.ExteriorWallTypeOther,
            buildingDetail.FenceType,
            buildingDetail.FenceTypeOther,
            buildingDetail.ConstructionType,
            buildingDetail.ConstructionTypeOther,
            // Utilization
            buildingDetail.UtilizationType,
            buildingDetail.UtilizationTypeOther,
            // Area & Pricing
            buildingDetail.TotalBuildingArea,
            buildingDetail.BuildingInsurancePrice,
            buildingDetail.SellingPrice,
            buildingDetail.ForcedSalePrice,
            // Remarks
            landDetail.Remark,
            buildingDetail.Remark,
            // Depreciation Details
            depreciationDtos,
            // Surfaces
            surfaceDtos,
            // Construction Inspection
            constructionDto);
    }
}