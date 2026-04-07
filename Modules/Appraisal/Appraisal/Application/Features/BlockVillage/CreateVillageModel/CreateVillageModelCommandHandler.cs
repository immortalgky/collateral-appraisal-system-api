namespace Appraisal.Application.Features.BlockVillage.CreateVillageModel;

public class CreateVillageModelCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreateVillageModelCommand, CreateVillageModelResult>
{
    public async Task<CreateVillageModelResult> Handle(
        CreateVillageModelCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var model = appraisal.AddVillageModel();

        model.Update(
            command.ModelName, command.ModelDescription, command.NumberOfHouse, command.StartingPrice,
            command.UsableAreaMin, command.UsableAreaMax, command.StandardUsableArea,
            command.LandAreaRai, command.LandAreaNgan, command.LandAreaWa, command.StandardLandArea,
            command.FireInsuranceCondition, command.ImageDocumentIds,
            command.BuildingType, command.BuildingTypeOther, command.NumberOfFloors,
            command.DecorationType, command.DecorationTypeOther,
            command.IsEncroachingOthers, command.EncroachingOthersRemark, command.EncroachingOthersArea,
            command.BuildingMaterialType, command.BuildingStyleType,
            command.IsResidential, command.BuildingAge, command.ConstructionYear, command.ResidentialRemark,
            command.ConstructionStyleType, command.ConstructionStyleRemark,
            command.StructureType, command.StructureTypeOther,
            command.RoofFrameType, command.RoofFrameTypeOther,
            command.RoofType, command.RoofTypeOther,
            command.CeilingType, command.CeilingTypeOther,
            command.InteriorWallType, command.InteriorWallTypeOther,
            command.ExteriorWallType, command.ExteriorWallTypeOther,
            command.FenceType, command.FenceTypeOther,
            command.ConstructionType, command.ConstructionTypeOther,
            command.UtilizationType, command.UtilizationTypeOther,
            command.Remark);

        ApplyAreaDetails(model, command.AreaDetails);
        ApplySurfaces(model, command.Surfaces);
        ApplyDepreciationDetails(model, command.DepreciationDetails);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateVillageModelResult(model.Id);
    }

    internal static void ApplyAreaDetails(VillageModel model, List<VillageModelAreaDetailDto>? dtos)
    {
        model.ClearAreaDetails();
        if (dtos is not { Count: > 0 }) return;
        foreach (var dto in dtos)
            model.AddAreaDetail(VillageModelAreaDetail.Create(dto.AreaDescription, dto.AreaSize));
    }

    internal static void ApplySurfaces(VillageModel model, List<VillageModelSurfaceDto>? dtos)
    {
        // Remove all existing surfaces
        foreach (var s in model.Surfaces.ToList())
            model.RemoveSurface(s.Id);

        if (dtos is not { Count: > 0 }) return;
        foreach (var dto in dtos)
            model.AddSurface(dto.FromFloorNumber, dto.ToFloorNumber, dto.FloorType,
                dto.FloorStructureType, dto.FloorStructureTypeOther,
                dto.FloorSurfaceType, dto.FloorSurfaceTypeOther);
    }

    internal static void ApplyDepreciationDetails(VillageModel model, List<VillageModelDepreciationDetailDto>? dtos)
    {
        // Remove all existing depreciation details
        foreach (var d in model.DepreciationDetails.ToList())
            model.RemoveDepreciationDetail(d.Id);

        if (dtos is not { Count: > 0 }) return;
        foreach (var dto in dtos)
        {
            var detail = model.AddDepreciationDetail(
                dto.DepreciationMethod, dto.AreaDescription, dto.Area, dto.Year, dto.IsBuilding,
                dto.PricePerSqMBeforeDepreciation, dto.PriceBeforeDepreciation,
                dto.PricePerSqMAfterDepreciation, dto.PriceAfterDepreciation,
                dto.DepreciationYearPct, dto.TotalDepreciationPct, dto.PriceDepreciation);

            if (dto.Periods is { Count: > 0 })
            {
                foreach (var p in dto.Periods)
                    detail.AddPeriod(p.AtYear, p.ToYear, p.DepreciationPerYear, p.TotalDepreciationPct, p.PriceDepreciation);
            }
        }
    }
}
