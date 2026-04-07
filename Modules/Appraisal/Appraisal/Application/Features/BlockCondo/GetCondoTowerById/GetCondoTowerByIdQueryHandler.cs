using Appraisal.Application.Features.BlockCondo.GetCondoTowers;

namespace Appraisal.Application.Features.BlockCondo.GetCondoTowerById;

public class GetCondoTowerByIdQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetCondoTowerByIdQuery, GetCondoTowerByIdResult>
{
    public async Task<GetCondoTowerByIdResult> Handle(
        GetCondoTowerByIdQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var tower = appraisal.CondoTowers.FirstOrDefault(t => t.Id == query.TowerId)
                    ?? throw new InvalidOperationException($"Condo tower {query.TowerId} not found");

        var dto = new CondoTowerDto(
            tower.Id, tower.AppraisalId,
            tower.TowerName, tower.NumberOfUnits, tower.NumberOfFloors,
            tower.CondoRegistrationNumber, tower.ModelTypeIds,
            tower.ConditionType, tower.HasObligation, tower.ObligationDetails, tower.DocumentValidationType,
            tower.IsLocationCorrect, tower.Distance, tower.RoadWidth, tower.RightOfWay,
            tower.RoadSurfaceType, tower.RoadSurfaceTypeOther,
            tower.DecorationType, tower.DecorationTypeOther,
            tower.ConstructionYear, tower.TotalNumberOfFloors, tower.BuildingFormType, tower.ConstructionMaterialType,
            tower.GroundFloorMaterialType, tower.GroundFloorMaterialTypeOther,
            tower.UpperFloorMaterialType, tower.UpperFloorMaterialTypeOther,
            tower.BathroomFloorMaterialType, tower.BathroomFloorMaterialTypeOther,
            tower.RoofType, tower.RoofTypeOther,
            tower.IsExpropriated, tower.ExpropriationRemark,
            tower.IsInExpropriationLine, tower.RoyalDecree,
            tower.IsForestBoundary, tower.ForestBoundaryRemark,
            tower.Remark, tower.ImageDocumentIds);

        return new GetCondoTowerByIdResult(dto);
    }
}
