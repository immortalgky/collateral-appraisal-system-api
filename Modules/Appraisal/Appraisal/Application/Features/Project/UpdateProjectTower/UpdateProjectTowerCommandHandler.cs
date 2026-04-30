namespace Appraisal.Application.Features.Project.UpdateProjectTower;

/// <summary>Handler for updating a project tower.</summary>
public class UpdateProjectTowerCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IProjectRepository projectRepository
) : ICommandHandler<UpdateProjectTowerCommand>
{
    public async Task<Unit> Handle(
        UpdateProjectTowerCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        // Domain guard: UpdateTower throws if ProjectType != Condo
        var tower = project.UpdateTower(command.TowerId);

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
            remark: command.Remark);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
