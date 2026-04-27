using Appraisal.Application.Features.Project.GetProjectModels;

namespace Appraisal.Application.Features.Project.CreateProjectModel;

/// <summary>
/// Handler for creating a new model within a project.
/// Works for both Condo and LandAndBuilding projects.
/// </summary>
public class CreateProjectModelCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IProjectRepository projectRepository
) : ICommandHandler<CreateProjectModelCommand, CreateProjectModelResult>
{
    public async Task<CreateProjectModelResult> Handle(
        CreateProjectModelCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        var model = project.AddModel();

        model.Update(
            modelName: command.ModelName,
            modelDescription: command.ModelDescription,
            buildingNumber: command.BuildingNumber,
            numberOfHouse: command.NumberOfHouse,
            startingPrice: command.StartingPrice,
            startingPriceMin: command.StartingPriceMin,
            startingPriceMax: command.StartingPriceMax,
            standardPrice: command.StandardPrice,
            hasMezzanine: command.HasMezzanine,
            usableAreaMin: command.UsableAreaMin,
            usableAreaMax: command.UsableAreaMax,
            standardUsableArea: command.StandardUsableArea,
            fireInsuranceCondition: command.FireInsuranceCondition,
            roomLayoutType: command.RoomLayoutType,
            roomLayoutTypeOther: command.RoomLayoutTypeOther,
            groundFloorMaterialType: command.GroundFloorMaterialType,
            groundFloorMaterialTypeOther: command.GroundFloorMaterialTypeOther,
            upperFloorMaterialType: command.UpperFloorMaterialType,
            upperFloorMaterialTypeOther: command.UpperFloorMaterialTypeOther,
            bathroomFloorMaterialType: command.BathroomFloorMaterialType,
            bathroomFloorMaterialTypeOther: command.BathroomFloorMaterialTypeOther,
            imageDocumentIds: command.ImageDocumentIds,
            remark: command.Remark,
            landAreaRai: command.LandAreaRai,
            landAreaNgan: command.LandAreaNgan,
            landAreaWa: command.LandAreaWa,
            standardLandArea: command.StandardLandArea,
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
            utilizationTypeOther: command.UtilizationTypeOther);

        ApplyAreaDetails(model, command.AreaDetails);
        ApplySurfaces(model, command.Surfaces);
        ApplyDepreciationDetails(model, command.DepreciationDetails);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateProjectModelResult(model.Id);
    }

    internal static void ApplyAreaDetails(Domain.Projects.ProjectModel model, List<ProjectModelAreaDetailDto>? dtos)
    {
        model.ClearAreaDetails();
        if (dtos is not { Count: > 0 }) return;
        foreach (var dto in dtos)
            model.AddAreaDetail(ProjectModelAreaDetail.Create(dto.AreaDescription, dto.AreaSize));
    }

    internal static void ApplySurfaces(Domain.Projects.ProjectModel model, List<ProjectModelSurfaceDto>? dtos)
    {
        foreach (var s in model.Surfaces.ToList())
            model.RemoveSurface(s.Id);

        if (dtos is not { Count: > 0 }) return;
        foreach (var dto in dtos)
            model.AddSurface(dto.FromFloorNumber, dto.ToFloorNumber, dto.FloorType,
                dto.FloorStructureType, dto.FloorStructureTypeOther,
                dto.FloorSurfaceType, dto.FloorSurfaceTypeOther);
    }

    internal static void ApplyDepreciationDetails(Domain.Projects.ProjectModel model, List<ProjectModelDepreciationDetailDto>? dtos)
    {
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
