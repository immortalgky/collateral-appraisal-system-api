using Shared.Exceptions;

namespace Appraisal.Application.Features.Project.GetProjectModelPricingContext;

/// <summary>
/// Returns a flat pricing context for a specific project model.
/// Validates that the project belongs to the given appraisal and that the model belongs
/// to the given project. No caching — live master-data edits must reflect immediately.
/// </summary>
public class GetProjectModelPricingContextQueryHandler(
    IProjectRepository projectRepository
) : IQueryHandler<GetProjectModelPricingContextQuery, ProjectModelPricingContextDto>
{
    public async Task<ProjectModelPricingContextDto> Handle(
        GetProjectModelPricingContextQuery query,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(query.AppraisalId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Domain.Projects.Project), query.AppraisalId);

        if (project.Id != query.ProjectId)
            throw new UnauthorizedAccessException();

        var model = project.Models.FirstOrDefault(m => m.Id == query.ModelId)
                    ?? throw new NotFoundException(nameof(ProjectModel), query.ModelId);

        var projectDto = BuildProjectContext(project);

        TowerContextDto? towerDto = null;
        if (project.ProjectType == Domain.Projects.ProjectType.Condo && model.ProjectTowerId.HasValue)
        {
            var tower = project.Towers.FirstOrDefault(t => t.Id == model.ProjectTowerId.Value);
            if (tower is not null)
                towerDto = BuildTowerContext(tower);
        }

        var modelDto = BuildModelContext(model, project.ProjectType);

        return new ProjectModelPricingContextDto(projectDto, towerDto, modelDto);
    }

    private static ProjectContextDto BuildProjectContext(Domain.Projects.Project project) =>
        new(
            Latitude: project.Coordinates?.Latitude,
            Longitude: project.Coordinates?.Longitude,
            Province: project.Address?.Province,
            District: project.Address?.District,
            SubDistrict: project.Address?.SubDistrict,
            Road: project.Road,
            Developer: project.Developer,
            ProjectName: project.ProjectName,
            LandOffice: project.LandOffice,
            ProjectLandAreaSquareWa: project.LandAreaSquareWa
        );

    private static TowerContextDto BuildTowerContext(ProjectTower tower) =>
        new(
            BuildingAge: tower.BuildingAge,
            NumberOfFloors: tower.NumberOfFloors,
            DecorationType: tower.DecorationType,
            RoofType: tower.RoofType,
            StructureType: tower.ConstructionMaterialType,
            RoadWidth: tower.RoadWidth,
            Distance: tower.Distance,
            RightOfWay: tower.RightOfWay,
            RoadSurfaceType: tower.RoadSurfaceType
        );

    private static ModelContextDto BuildModelContext(ProjectModel model, Domain.Projects.ProjectType projectType)
    {
        // Building-level fields live on the Model for LandAndBuilding-like projects (LB and Land).
        // For Condo, these fields come from the Tower — set them null on the Model side.
        // TODO(Land): Land follows LB model-field logic in v1.
        bool isLb = projectType.IsLandAndBuildingLike();

        return new ModelContextDto(
            ModelName: model.ModelName,
            UsableAreaMin: model.UsableAreaMin,
            UsableAreaMax: model.UsableAreaMax,
            StandardUsableArea: model.StandardUsableArea,
            HasMezzanine: model.HasMezzanine,
            RoomLayoutType: model.RoomLayoutType,
            FireInsuranceCondition: model.FireInsuranceCondition,
            GroundFloorMaterialType: model.GroundFloorMaterialType,
            UpperFloorMaterialType: model.UpperFloorMaterialType,
            BathroomFloorMaterialType: model.BathroomFloorMaterialType,
            BuildingAge: model.BuildingAge,
            UtilizationType: model.UtilizationType,
            StartingPriceMin: model.StartingPriceMin,
            StartingPriceMax: model.StartingPriceMax,
            // Per-model representative land area for pricing — uses StandardLandArea
            // (the canonical "typical plot" sq.wa). Null when project is not LB.
            LandAreaSquareWa: isLb ? model.StandardLandArea : null,
            // Building-level fields: LandAndBuilding only
            ConstructionYear: isLb ? model.ConstructionYear : null,
            NumberOfFloors: isLb ? model.NumberOfFloors : null,
            DecorationType: isLb ? model.DecorationType : null,
            RoofType: isLb ? model.RoofType : null,
            StructureType: isLb ? model.StructureType : null,
            RoadWidth: null,      // LB models don't carry road-access fields (per plan §5)
            Distance: null,
            RightOfWay: null,
            RoadSurfaceType: null
        );
    }
}
