namespace Appraisal.Application.Features.Project.GetProjectModels;

/// <summary>Handler for getting all models for a project.</summary>
public class GetProjectModelsQueryHandler(
    IProjectRepository projectRepository
) : IQueryHandler<GetProjectModelsQuery, GetProjectModelsResult>
{
    public async Task<GetProjectModelsResult> Handle(
        GetProjectModelsQuery query,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(query.AppraisalId, cancellationToken);

        if (project is null)
            return new GetProjectModelsResult([]);

        var models = project.Models.Select(MapToDto).ToList();

        return new GetProjectModelsResult(models);
    }

    internal static ProjectModelDto MapToDto(Domain.Projects.ProjectModel m) =>
        new(
            Id: m.Id,
            ProjectId: m.ProjectId,
            ModelName: m.ModelName,
            ModelDescription: m.ModelDescription,
            BuildingNumber: m.BuildingNumber,
            NumberOfHouse: m.NumberOfHouse,
            StartingPrice: m.StartingPrice,
            StartingPriceMin: m.StartingPriceMin,
            StartingPriceMax: m.StartingPriceMax,
            HasMezzanine: m.HasMezzanine,
            UsableAreaMin: m.UsableAreaMin,
            UsableAreaMax: m.UsableAreaMax,
            StandardUsableArea: m.StandardUsableArea,
            FireInsuranceCondition: m.FireInsuranceCondition,
            RoomLayoutType: m.RoomLayoutType,
            RoomLayoutTypeOther: m.RoomLayoutTypeOther,
            GroundFloorMaterialType: m.GroundFloorMaterialType,
            GroundFloorMaterialTypeOther: m.GroundFloorMaterialTypeOther,
            UpperFloorMaterialType: m.UpperFloorMaterialType,
            UpperFloorMaterialTypeOther: m.UpperFloorMaterialTypeOther,
            BathroomFloorMaterialType: m.BathroomFloorMaterialType,
            BathroomFloorMaterialTypeOther: m.BathroomFloorMaterialTypeOther,
            Remark: m.Remark,
            LandAreaRai: m.LandAreaRai,
            LandAreaNgan: m.LandAreaNgan,
            LandAreaWa: m.LandAreaWa,
            StandardLandArea: m.StandardLandArea,
            BuildingType: m.BuildingType,
            BuildingTypeOther: m.BuildingTypeOther,
            NumberOfFloors: m.NumberOfFloors,
            DecorationType: m.DecorationType,
            DecorationTypeOther: m.DecorationTypeOther,
            IsEncroachingOthers: m.IsEncroachingOthers,
            EncroachingOthersRemark: m.EncroachingOthersRemark,
            EncroachingOthersArea: m.EncroachingOthersArea,
            BuildingMaterialType: m.BuildingMaterialType,
            BuildingStyleType: m.BuildingStyleType,
            IsResidential: m.IsResidential,
            BuildingAge: m.BuildingAge,
            ConstructionYear: m.ConstructionYear,
            ResidentialRemark: m.ResidentialRemark,
            ConstructionStyleType: m.ConstructionStyleType,
            ConstructionStyleRemark: m.ConstructionStyleRemark,
            StructureType: m.StructureType,
            StructureTypeOther: m.StructureTypeOther,
            RoofFrameType: m.RoofFrameType,
            RoofFrameTypeOther: m.RoofFrameTypeOther,
            RoofType: m.RoofType,
            RoofTypeOther: m.RoofTypeOther,
            CeilingType: m.CeilingType,
            CeilingTypeOther: m.CeilingTypeOther,
            InteriorWallType: m.InteriorWallType,
            InteriorWallTypeOther: m.InteriorWallTypeOther,
            ExteriorWallType: m.ExteriorWallType,
            ExteriorWallTypeOther: m.ExteriorWallTypeOther,
            FenceType: m.FenceType,
            FenceTypeOther: m.FenceTypeOther,
            ConstructionType: m.ConstructionType,
            ConstructionTypeOther: m.ConstructionTypeOther,
            UtilizationType: m.UtilizationType,
            UtilizationTypeOther: m.UtilizationTypeOther,
            PricingAnalysisId: m.PricingAnalysis?.Id,
            PricingAnalysisStatus: m.PricingAnalysis?.Status,
            FinalAppraisedValue: m.PricingAnalysis?.FinalAppraisedValue,
            AreaDetails: m.AreaDetails
                .Select(a => new ProjectModelAreaDetailDto(a.Id, a.AreaDescription, a.AreaSize))
                .ToList(),
            Surfaces: m.Surfaces
                .Select(s => new ProjectModelSurfaceDto(
                    s.FromFloorNumber, s.ToFloorNumber, s.FloorType,
                    s.FloorStructureType, s.FloorStructureTypeOther,
                    s.FloorSurfaceType, s.FloorSurfaceTypeOther))
                .ToList(),
            DepreciationDetails: m.DepreciationDetails
                .Select(d => new ProjectModelDepreciationDetailDto(
                    d.DepreciationMethod, d.AreaDescription, d.Area, d.Year, d.IsBuilding,
                    d.PricePerSqMBeforeDepreciation, d.PriceBeforeDepreciation,
                    d.PricePerSqMAfterDepreciation, d.PriceAfterDepreciation,
                    d.DepreciationYearPct, d.TotalDepreciationPct, d.PriceDepreciation,
                    d.DepreciationPeriods
                        .Select(p => new ProjectModelDepreciationPeriodDto(
                            p.AtYear, p.ToYear, p.DepreciationPerYear,
                            p.TotalDepreciationPct, p.PriceDepreciation))
                        .ToList()))
                .ToList(),
            Images: m.Images
                .OrderBy(i => i.DisplaySequence)
                .Select(i => new ProjectModelImageDto(
                    i.Id, i.GalleryPhotoId, i.DisplaySequence, i.Title, i.Description, i.IsThumbnail))
                .ToList()
        );
}
