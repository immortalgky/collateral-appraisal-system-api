namespace Appraisal.Application.Features.Project.GetProjectTowers;

/// <summary>Handler for getting all towers for a project.</summary>
public class GetProjectTowersQueryHandler(
    IProjectRepository projectRepository
) : IQueryHandler<GetProjectTowersQuery, GetProjectTowersResult>
{
    public async Task<GetProjectTowersResult> Handle(
        GetProjectTowersQuery query,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(query.AppraisalId, cancellationToken);

        if (project is null)
            return new GetProjectTowersResult([]);

        var towers = project.Towers.Select(MapToDto).ToList();

        return new GetProjectTowersResult(towers);
    }

    internal static ProjectTowerDto MapToDto(Domain.Projects.ProjectTower t) =>
        new(
            Id: t.Id,
            ProjectId: t.ProjectId,
            TowerName: t.TowerName,
            NumberOfUnits: t.NumberOfUnits,
            NumberOfFloors: t.NumberOfFloors,
            CondoRegistrationNumber: t.CondoRegistrationNumber,
            ModelTypeIds: t.ModelTypeIds,
            ConditionType: t.ConditionType,
            HasObligation: t.HasObligation,
            ObligationDetails: t.ObligationDetails,
            DocumentValidationType: t.DocumentValidationType,
            IsLocationCorrect: t.IsLocationCorrect,
            Distance: t.Distance,
            RoadWidth: t.RoadWidth,
            RightOfWay: t.RightOfWay,
            RoadSurfaceType: t.RoadSurfaceType,
            RoadSurfaceTypeOther: t.RoadSurfaceTypeOther,
            DecorationType: t.DecorationType,
            DecorationTypeOther: t.DecorationTypeOther,
            ConstructionYear: t.ConstructionYear,
            TotalNumberOfFloors: t.TotalNumberOfFloors,
            BuildingFormType: t.BuildingFormType,
            ConstructionMaterialType: t.ConstructionMaterialType,
            GroundFloorMaterialType: t.GroundFloorMaterialType,
            GroundFloorMaterialTypeOther: t.GroundFloorMaterialTypeOther,
            UpperFloorMaterialType: t.UpperFloorMaterialType,
            UpperFloorMaterialTypeOther: t.UpperFloorMaterialTypeOther,
            BathroomFloorMaterialType: t.BathroomFloorMaterialType,
            BathroomFloorMaterialTypeOther: t.BathroomFloorMaterialTypeOther,
            RoofType: t.RoofType,
            RoofTypeOther: t.RoofTypeOther,
            IsExpropriated: t.IsExpropriated,
            ExpropriationRemark: t.ExpropriationRemark,
            IsInExpropriationLine: t.IsInExpropriationLine,
            RoyalDecree: t.RoyalDecree,
            IsForestBoundary: t.IsForestBoundary,
            ForestBoundaryRemark: t.ForestBoundaryRemark,
            Remark: t.Remark,
            Images: t.Images
                .OrderBy(i => i.DisplaySequence)
                .Select(i => new ProjectTowerImageDto(
                    i.Id, i.GalleryPhotoId, i.DisplaySequence, i.Title, i.Description, i.IsThumbnail))
                .ToList()
        );
}
