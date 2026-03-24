using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;

namespace Appraisal.Application.Features.Appraisals.UpdateBuildingProperty;

/// <summary>
/// Handler for updating a building property detail
/// </summary>
public class UpdateBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateBuildingPropertyCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateBuildingPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);
        
        var property = appraisal.GetProperty(command.PropertyId)
            ?? throw new PropertyNotFoundException(command.PropertyId);
        
        if (property.PropertyType != PropertyType.Building)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a building property");
        
        var detail = property.BuildingDetail
            ?? throw new InvalidOperationException($"Building detail not found for property {command.PropertyId}");

        detail.Update(
            propertyName: command.PropertyName,
            buildingNumber: command.BuildingNumber,
            modelName: command.ModelName,
            builtOnTitleNumber: command.BuiltOnTitleNumber,
            houseNumber: command.HouseNumber,
            ownerName: command.OwnerName,
            isOwnerVerified: command.IsOwnerVerified,
            hasObligation: command.HasObligation,
            obligationDetails: command.ObligationDetails,
            buildingConditionType: command.BuildingConditionType,
            buildingConditionTypeOther: command.BuildingConditionTypeOther,
            isUnderConstruction: command.IsUnderConstruction,
            constructionCompletionPercent: command.ConstructionCompletionPercent,
            constructionLicenseExpirationDate: command.ConstructionLicenseExpirationDate,
            isAppraisable: command.IsAppraisable,
            buildingType: command.BuildingType,
            buildingTypeOther: command.BuildingTypeOther,
            numberOfFloors: command.NumberOfFloors,
            decorationType: command.DecorationType,
            decorationTypeOther: command.DecorationTypeOther,
            isEncroachingOthers: command.IsEncroachingOthers,
            encroachingOthersRemark: command.EncroachingOthersRemark,
            encroachingOthersArea: command.EncroachingOthersArea,
            buildingMaterialType: command.BuildingMaterialType,
            buildingStyleType: command.BuildingStyleType,
            isResidential: command.IsResidential,
            buildingAge: command.BuildingAge,
            constructionYear: command.ConstructionYear,
            residentialRemark: command.ResidentialRemark,
            constructionStyleType: command.ConstructionStyleType,
            constructionStyleRemark: command.ConstructionStyleRemark,
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
            constructionType: command.ConstructionType,
            constructionTypeOther: command.ConstructionTypeOther,
            utilizationType: command.UtilizationType,
            utilizationTypeOther: command.UtilizationTypeOther,
            totalBuildingArea: command.TotalBuildingArea,
            buildingInsurancePrice: command.BuildingInsurancePrice,
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            remark: command.Remark);

        // Sync depreciation details (null = no-op, list = sync)
        if (command.DepreciationDetails is not null)
            SyncDepreciationDetails(detail, command.DepreciationDetails);

        // Sync surfaces (null = no-op, list = sync)
        if (command.Surfaces is not null)
            SyncSurfaces(detail, command.Surfaces);

        // Sync construction inspection (null = clear, provided = upsert)
        // Also clear if building is not under construction
        if (command.ConstructionInspection is null || command.IsUnderConstruction == false)
            ClearConstructionInspection(property);
        else
            SyncConstructionInspection(property, command.ConstructionInspection);

        return MediatR.Unit.Value;
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
                var existing = buildingDetail.DepreciationDetails
                    .FirstOrDefault(d => d.Id == item.Id.Value);
                if (existing is null) continue;

                existing.Update(
                    item.DepreciationMethod, item.AreaDescription, item.Area, item.Year,
                    item.IsBuilding, item.PricePerSqMBeforeDepreciation, item.PriceBeforeDepreciation,
                    item.PricePerSqMAfterDepreciation, item.PriceAfterDepreciation,
                    item.DepreciationYearPct, item.TotalDepreciationPct, item.PriceDepreciation);

                existing.ClearPeriods();
                if (item.DepreciationPeriods is { Count: > 0 })
                    foreach (var p in item.DepreciationPeriods)
                        existing.AddPeriod(p.AtYear, p.ToYear, p.DepreciationPerYear,
                            p.TotalDepreciationPct, p.PriceDepreciation);
            }
            else
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
            // Update existing
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
            // Create new
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
