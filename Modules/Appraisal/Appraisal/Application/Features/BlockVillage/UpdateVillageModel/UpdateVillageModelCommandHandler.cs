using Appraisal.Application.Features.BlockVillage.CreateVillageModel;

namespace Appraisal.Application.Features.BlockVillage.UpdateVillageModel;

public class UpdateVillageModelCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateVillageModelCommand>
{
    public async Task<Unit> Handle(
        UpdateVillageModelCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithVillageDataAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var model = appraisal.VillageModels.FirstOrDefault(m => m.Id == command.ModelId)
                    ?? throw new InvalidOperationException($"Village model {command.ModelId} not found");

        model.Update(
            command.ModelName, command.ModelDescription, command.NumberOfHouse, command.StartingPrice,
            command.UsableAreaMin, command.UsableAreaMax, command.StandardUsableArea,
            command.LandAreaRai, command.LandAreaNgan, command.LandAreaWa, command.StandardLandArea,
            command.FireInsuranceCondition, command.ImageDocumentIds,
            command.BuildingType, command.BuildingTypeOther, command.NumberOfFloors,
            command.DecorationType, command.DecorationTypeOther,
            command.IsEncroachingOthers, command.EncroachingOthersRemark, command.EncroachingOthersArea,
            command.BuildingMaterialType, command.BuildingStyleType,
            command.IsResidential, command.BuildingAge, command.ConstructionYear, command.ResidentialRemark,
            command.ConstructionStyleType, command.ConstructionStyleRemark,
            command.StructureType, command.StructureTypeOther,
            command.RoofFrameType, command.RoofFrameTypeOther,
            command.RoofType, command.RoofTypeOther,
            command.CeilingType, command.CeilingTypeOther,
            command.InteriorWallType, command.InteriorWallTypeOther,
            command.ExteriorWallType, command.ExteriorWallTypeOther,
            command.FenceType, command.FenceTypeOther,
            command.ConstructionType, command.ConstructionTypeOther,
            command.UtilizationType, command.UtilizationTypeOther,
            command.Remark);

        CreateVillageModelCommandHandler.ApplyAreaDetails(model, command.AreaDetails);
        CreateVillageModelCommandHandler.ApplySurfaces(model, command.Surfaces);
        CreateVillageModelCommandHandler.ApplyDepreciationDetails(model, command.DepreciationDetails);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
