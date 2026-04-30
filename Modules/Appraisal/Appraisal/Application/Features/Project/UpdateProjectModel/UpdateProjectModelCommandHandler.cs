using Appraisal.Application.Features.Project.CreateProjectModel;
using Appraisal.Application.Features.Project.GetProjectModels;

namespace Appraisal.Application.Features.Project.UpdateProjectModel;

/// <summary>Handler for updating an existing project model.</summary>
public class UpdateProjectModelCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IProjectRepository projectRepository
) : ICommandHandler<UpdateProjectModelCommand>
{
    public async Task<Unit> Handle(
        UpdateProjectModelCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        var model = project.Models.FirstOrDefault(m => m.Id == command.ModelId)
                    ?? throw new InvalidOperationException($"Project model {command.ModelId} not found");

        model.Update(
            modelName: command.ModelName,
            modelDescription: command.ModelDescription,
            buildingNumber: command.BuildingNumber,
            numberOfHouse: command.NumberOfHouse,
            startingPrice: command.StartingPrice,
            startingPriceMin: command.StartingPriceMin,
            startingPriceMax: command.StartingPriceMax,
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

        CreateProjectModelCommandHandler.ApplyAreaDetails(model, command.AreaDetails);
        CreateProjectModelCommandHandler.ApplySurfaces(model, command.Surfaces);
        CreateProjectModelCommandHandler.ApplyDepreciationDetails(model, command.DepreciationDetails);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
