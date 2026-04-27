namespace Appraisal.Application.Features.Project.CreateProjectTower;

/// <summary>
/// Handler for creating a tower within a project.
/// Delegates type enforcement to the domain — Project.AddTower() throws if ProjectType != Condo.
/// </summary>
public class CreateProjectTowerCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IProjectRepository projectRepository
) : ICommandHandler<CreateProjectTowerCommand, CreateProjectTowerResult>
{
    public async Task<CreateProjectTowerResult> Handle(
        CreateProjectTowerCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        // Domain guard: throws InvalidOperationException if ProjectType != Condo
        var tower = project.AddTower();

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

        return new CreateProjectTowerResult(tower.Id);
    }
}
