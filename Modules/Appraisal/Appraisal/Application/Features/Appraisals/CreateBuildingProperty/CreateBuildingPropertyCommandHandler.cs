using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;

namespace Appraisal.Application.Features.Appraisals.CreateBuildingProperty;

/// <summary>
/// Handler for creating a building property with its appraisal detail
/// </summary>
public class CreateBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<CreateBuildingPropertyCommand, CreateBuildingPropertyResult>
{
    public async Task<CreateBuildingPropertyResult> Handle(
        CreateBuildingPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.AddBuildingProperty();

        property.BuildingDetail!.Update(
            command.PropertyName,
            command.BuildingNumber,
            command.ModelName,
            command.BuiltOnTitleNumber,
            command.HouseNumber,
            command.OwnerName,
            command.IsOwnerVerified,
            command.HasObligation,
            command.ObligationDetails,
            command.BuildingConditionType,
            command.IsUnderConstruction,
            command.ConstructionCompletionPercent,
            command.ConstructionLicenseExpirationDate,
            command.IsAppraisable,
            command.BuildingType,
            command.BuildingTypeOther,
            command.NumberOfFloors,
            command.DecorationType,
            command.DecorationTypeOther,
            command.IsEncroachingOthers,
            command.EncroachingOthersRemark,
            command.EncroachingOthersArea,
            command.BuildingMaterialType,
            command.BuildingStyleType,
            command.IsResidential,
            command.BuildingAge,
            command.ConstructionYear,
            command.ResidentialRemark,
            command.ConstructionStyleType,
            command.ConstructionStyleRemark,
            command.StructureType,
            command.StructureTypeOther,
            command.RoofFrameType,
            command.RoofFrameTypeOther,
            command.RoofType,
            command.RoofTypeOther,
            command.CeilingType,
            command.CeilingTypeOther,
            command.InteriorWallType,
            command.InteriorWallTypeOther,
            command.ExteriorWallType,
            command.ExteriorWallTypeOther,
            command.FenceType,
            command.FenceTypeOther,
            command.ConstructionType,
            command.ConstructionTypeOther,
            command.UtilizationType,
            command.UtilizationTypeOther,
            command.TotalBuildingArea,
            command.BuildingInsurancePrice,
            command.SellingPrice,
            command.ForcedSalePrice,
            command.Remark);

        // Add depreciation details if provided
        if (command.DepreciationDetails is { Count: > 0 })
            AddDepreciationDetails(property.BuildingDetail, command.DepreciationDetails);

        // Add surfaces if provided
        if (command.Surfaces is { Count: > 0 })
            AddSurfaces(property.BuildingDetail, command.Surfaces);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (command.GroupId.HasValue) appraisal.AddPropertyToGroup(command.GroupId.Value, property.Id);

        return new CreateBuildingPropertyResult(property.Id, property.BuildingDetail.Id);
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
}