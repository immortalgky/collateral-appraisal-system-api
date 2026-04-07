namespace Appraisal.Application.Features.BlockCondo.GetCondoTowers;

public class GetCondoTowersQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetCondoTowersQuery, GetCondoTowersResult>
{
    public async Task<GetCondoTowersResult> Handle(
        GetCondoTowersQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var towers = appraisal.CondoTowers
            .Select(t => new CondoTowerDto(
                t.Id, t.AppraisalId,
                t.TowerName, t.NumberOfUnits, t.NumberOfFloors,
                t.CondoRegistrationNumber, t.ModelTypeIds,
                t.ConditionType, t.HasObligation, t.ObligationDetails, t.DocumentValidationType,
                t.IsLocationCorrect, t.Distance, t.RoadWidth, t.RightOfWay,
                t.RoadSurfaceType, t.RoadSurfaceTypeOther,
                t.DecorationType, t.DecorationTypeOther,
                t.ConstructionYear, t.TotalNumberOfFloors, t.BuildingFormType, t.ConstructionMaterialType,
                t.GroundFloorMaterialType, t.GroundFloorMaterialTypeOther,
                t.UpperFloorMaterialType, t.UpperFloorMaterialTypeOther,
                t.BathroomFloorMaterialType, t.BathroomFloorMaterialTypeOther,
                t.RoofType, t.RoofTypeOther,
                t.IsExpropriated, t.ExpropriationRemark,
                t.IsInExpropriationLine, t.RoyalDecree,
                t.IsForestBoundary, t.ForestBoundaryRemark,
                t.Remark, t.ImageDocumentIds))
            .ToList();

        return new GetCondoTowersResult(towers);
    }
}
