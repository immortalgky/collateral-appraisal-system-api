using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockCondo.UpdateCondoTower;

public class UpdateCondoTowerCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<UpdateCondoTowerCommand>
{
    public async Task<Unit> Handle(
        UpdateCondoTowerCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithCondoDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var tower = appraisal.CondoTowers.FirstOrDefault(t => t.Id == command.TowerId)
                    ?? throw new InvalidOperationException($"Condo tower {command.TowerId} not found");

        tower.Update(
            towerName: command.TowerName,
            numberOfUnits: command.NumberOfUnits,
            numberOfFloors: command.NumberOfFloors,
            condoRegistrationNumber: command.CondoRegistrationNumber,
            modelTypeIds: command.ModelTypeIds,
            conditionType: command.ConditionType,
            hasObligation: command.HasObligation,
            obligationDetails: command.ObligationDetails,
            documentValidationType: command.DocumentValidationType,
            isLocationCorrect: command.IsLocationCorrect,
            distance: command.Distance,
            roadWidth: command.RoadWidth,
            rightOfWay: command.RightOfWay,
            roadSurfaceType: command.RoadSurfaceType,
            roadSurfaceTypeOther: command.RoadSurfaceTypeOther,
            decorationType: command.DecorationType,
            decorationTypeOther: command.DecorationTypeOther,
            constructionYear: command.ConstructionYear,
            totalNumberOfFloors: command.TotalNumberOfFloors,
            buildingFormType: command.BuildingFormType,
            constructionMaterialType: command.ConstructionMaterialType,
            groundFloorMaterialType: command.GroundFloorMaterialType,
            groundFloorMaterialTypeOther: command.GroundFloorMaterialTypeOther,
            upperFloorMaterialType: command.UpperFloorMaterialType,
            upperFloorMaterialTypeOther: command.UpperFloorMaterialTypeOther,
            bathroomFloorMaterialType: command.BathroomFloorMaterialType,
            bathroomFloorMaterialTypeOther: command.BathroomFloorMaterialTypeOther,
            roofType: command.RoofType,
            roofTypeOther: command.RoofTypeOther,
            isExpropriated: command.IsExpropriated,
            expropriationRemark: command.ExpropriationRemark,
            isInExpropriationLine: command.IsInExpropriationLine,
            royalDecree: command.RoyalDecree,
            isForestBoundary: command.IsForestBoundary,
            forestBoundaryRemark: command.ForestBoundaryRemark,
            remark: command.Remark,
            imageDocumentIds: command.ImageDocumentIds);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
