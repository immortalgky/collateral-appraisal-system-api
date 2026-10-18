using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;

/// <summary>
/// Writes an <see cref="UpdateLandAndBuildingPropertyCommand"/> payload into the property. Shared by the real page handler and
/// the data-correction command so both apply exactly the same rules, including the building's stored
/// insurance, re-resolved from its depreciation rows unless a figure was typed (so a correction that
/// edits a row moves it, while the approved ValuationAnalyses total stays as approved). The side effect
/// that belongs to the real page only — the valuation recompute — stays in the handler.
/// </summary>
public static class LandAndBuildingPropertyApplier
{
    public static void Apply(AppraisalProperty property, UpdateLandAndBuildingPropertyCommand command)
    {
        // 3. Validate property type
        if (property.PropertyType != PropertyType.LandAndBuilding && property.PropertyType != PropertyType.LeaseAgreementLandAndBuilding)
            throw new InvalidOperationException($"Property {property.Id} is not a land and building property");

        // 4. Get the detail records
        var landDetail = property.LandDetail
                         ?? throw new InvalidOperationException(
                             $"Land detail not found for property {property.Id}");
        var buildingDetail = property.BuildingDetail
                             ?? throw new InvalidOperationException(
                                 $"Building detail not found for property {property.Id}");

        // 5. Build value objects if provided
        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        Address? address = null;
        if (!string.IsNullOrEmpty(command.SubDistrict) || !string.IsNullOrEmpty(command.District) ||
            !string.IsNullOrEmpty(command.Province))
            address = Address.Create(command.SubDistrict, command.District, command.Province);
        Address? dopaAddress = null;
        if (command.DopaSubDistrict is not null || command.DopaDistrict is not null || command.DopaProvince is not null)
            dopaAddress = Address.Create(command.DopaSubDistrict, command.DopaDistrict, command.DopaProvince);

        // 6. Update Land detail via domain method
        landDetail.Update(
            // Property Identification
            propertyName: command.PropertyName,
            landDescription: command.LandDescription,
            coordinates: coordinates,
            address: address,
            ownerName: command.OwnerNameLand,
            isOwnerVerified: command.IsOwnerVerifiedLand,
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
            landShapeTypeOther: command.LandShapeTypeOther,
            urbanPlanningType: command.UrbanPlanningType,
            landZoneType: command.LandZoneType,
            landZoneTypeOther: command.LandZoneTypeOther,
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
            propertyAnticipationTypeOther: command.PropertyAnticipationTypeOther,
            // Land - Legal
            isExpropriated: command.IsExpropriated,
            expropriationRemark: command.ExpropriationRemark,
            isInExpropriationLine: command.IsInExpropriationLine,
            expropriationLineRemark: command.ExpropriationLineRemark,
            royalDecree: command.RoyalDecree,
            isEncroached: command.IsEncroached,
            encroachmentRemark: command.EncroachmentRemark,
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
            hasBuilding: command.HasBuilding,
            hasBuildingOther: command.HasBuildingOther,
            remark: command.Remark,
            isRentedOut: command.IsRentedOut,
            landOffice: command.LandOffice,
            dopaAddress: dopaAddress);

        // 6b. Sync land titles (null = no-op, empty list = clear all)
        if (command.Titles is not null)
            LandDetailSync.SyncTitles(landDetail, command.Titles);

        // Same contract for the area deductions that come off the appraised area.
        if (command.LandAreaDeductions is not null)
            LandDetailSync.SyncDeductions(landDetail, command.LandAreaDeductions);

        // The deed guard needs both lists final, so it runs once here rather than inside either
        // sync — and also when only the titles changed, since a smaller deed can fall under the
        // deductions already recorded.
        if (command.Titles is not null || command.LandAreaDeductions is not null)
            landDetail.RecalculateDeductedArea();

        // 7. Update Building detail via domain method
        buildingDetail.Update(
            // Building - Identification
            buildingNumber: command.BuildingNumber,
            modelName: command.ModelName,
            builtOnTitleNumber: command.BuiltOnTitleNumber,
            houseNumber: command.HouseNumber,
            noHouseNumber: command.NoHouseNumber,
            ownerName: command.OwnerNameBuilding,
            isOwnerVerified: command.IsOwnerVerifiedBuilding,
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
            buildingConditionTypeOther: command.BuildingConditionTypeOther,
            isUnderConstruction: command.IsUnderConstruction,
            constructionLicenseExpirationDate: command.ConstructionLicenseExpirationDate,
            isAppraisable: command.IsAppraisable,
            // Building - Area
            totalBuildingArea: command.TotalBuildingArea,
            // Building - Structure
            numberOfFloors: command.NumberOfFloors,
            // Building - Style
            buildingMaterialType: command.BuildingMaterialType,
            buildingStyleType: command.BuildingStyleType,
            buildingStyleTypeOther: command.BuildingStyleTypeOther,
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
            buildingCostValue: command.BuildingCostValue,
            buildingInsurancePrice: command.BuildingInsurancePrice,
            remark: command.Remark);

        // 8. Sync depreciation details (null = no-op, list = sync)
        if (command.DepreciationDetails is not null)
            SyncDepreciationDetails(buildingDetail, command.DepreciationDetails);

        // After the depreciation rows: a building with no typed coverage stores the value computed from them
        buildingDetail.ResolveDerivedValues();

        // 8b. Sync surfaces (null = no-op, list = sync)
        if (command.Surfaces is not null)
            SyncSurfaces(buildingDetail, command.Surfaces);

        // 8c. Sync construction inspection (null = clear, provided = upsert)
        // Also clear if building is not under construction
        if (command.ConstructionInspection is null || command.IsUnderConstruction == false)
            ClearConstructionInspection(property);
        else
            SyncConstructionInspection(property, command.ConstructionInspection);

        // 9a. Rental: if rented out, ensure lease/rental owned entities exist and apply updates;
        // otherwise clear them so owned rows are cascade-deleted.
        if (command.IsRentedOut == true)
        {
            if (property.LeaseAgreementDetail is null)
                property.SetLeaseAgreementDetail(LeaseAgreementDetail.Create(property.Id));
            if (property.RentalInfo is null)
                property.SetRentalInfo(RentalInfo.Create(property.Id));

            if (command.LeaseAgreement is not null)
            {
                property.LeaseAgreementDetail!.Update(
                    command.LeaseAgreement.LesseeName, command.LeaseAgreement.LessorName,
                    command.LeaseAgreement.LeasePeriodAsContract, command.LeaseAgreement.RemainingLeaseAsAppraisalDate,
                    command.LeaseAgreement.ContractNo, command.LeaseAgreement.LeaseStartDate, command.LeaseAgreement.LeaseEndDate,
                    command.LeaseAgreement.LeaseRentFee, command.LeaseAgreement.RentAdjust,
                    command.LeaseAgreement.Sublease, command.LeaseAgreement.AdditionalExpenses,
                    command.LeaseAgreement.LeaseTerminate, command.LeaseAgreement.ContractRenewal,
                    command.LeaseAgreement.RentalTermsImpactingPropertyUse, command.LeaseAgreement.TerminationOfLease,
                    command.LeaseAgreement.Remark);
            }

            if (command.RentalInfo is not null)
            {
                var rentalInfo = property.RentalInfo!;
                rentalInfo.Update(
                    command.RentalInfo.NumberOfYears, command.RentalInfo.FirstYearStartDate,
                    command.RentalInfo.ContractRentalFeePerYear, command.RentalInfo.UpFrontTotalAmount,
                    command.RentalInfo.GrowthRateType, command.RentalInfo.GrowthRatePercent,
                    command.RentalInfo.GrowthIntervalYears);

                if (command.RentalInfo.UpFrontEntries is not null)
                {
                    rentalInfo.ClearUpFrontEntries();
                    foreach (var entry in command.RentalInfo.UpFrontEntries)
                        rentalInfo.AddUpFrontEntry(entry.AtYear, entry.UpFrontAmount);
                }

                if (command.RentalInfo.GrowthPeriodEntries is not null)
                {
                    rentalInfo.ClearGrowthPeriodEntries();
                    foreach (var entry in command.RentalInfo.GrowthPeriodEntries)
                        rentalInfo.AddGrowthPeriodEntry(entry.FromYear, entry.ToYear, entry.GrowthRate, entry.GrowthAmount, entry.TotalAmount);
                }

                RentalScheduleComputer.ComputeAndSave(rentalInfo, command.RentalInfo.ScheduleOverrides);
            }
        }
        else
        {
            property.ClearLeaseAgreementDetail();
            property.ClearRentalInfo();
        }
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
                inspection.UpdateFullDetail(ci.TotalValue, ci.Remark);
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
                inspection = ConstructionInspection.CreateFullDetail(property.Id, ci.TotalValue, ci.Remark);
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
