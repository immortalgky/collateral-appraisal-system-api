namespace Appraisal.Application.Features.Appraisals.CreateBuildingProperty;

/// <summary>
/// Handler for creating a building property with its appraisal detail
/// </summary>
public class CreateBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreateBuildingPropertyCommand, CreateBuildingPropertyResult>
{
    public async Task<CreateBuildingPropertyResult> Handle(
        CreateBuildingPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.AddBuildingProperty(
            command.OwnerName);

        property.BuildingDetail!.Update(
            command.PropertyName,
            command.BuildingNumber,
            command.ModelName,
            command.BuiltOnTitleNumber,
            command.HouseNumber,
            command.OwnerName,
            command.IsOwnerVerified,
            command.HasObligation,
            command.ObligationDetails,
            command.BuildingConditionType,
            command.IsUnderConstruction,
            command.ConstructionCompletionPercent,
            command.ConstructionLicenseExpirationDate,
            command.IsAppraisable,
            command.BuildingType,
            command.BuildingTypeOther,
            command.NumberOfFloors,
            command.DecorationType,
            command.DecorationTypeOther,
            command.IsEncroachingOthers,
            command.EncroachingOthersRemark,
            command.EncroachingOthersArea,
            command.BuildingMaterialType,
            command.BuildingStyleType,
            command.IsResidential,
            command.BuildingAge,
            command.ConstructionYear,
            command.ResidentialRemark,
            command.ConstructionStyleType,
            command.ConstructionStyleRemark,
            command.StructureType,
            command.StructureTypeOther,
            command.RoofFrameType,
            command.RoofFrameTypeOther,
            command.RoofType,
            command.RoofTypeOther,
            command.CeilingType,
            command.CeilingTypeOther,
            command.InteriorWallType,
            command.InteriorWallTypeOther,
            command.ExteriorWallType,
            command.ExteriorWallTypeOther,
            command.FenceType,
            command.FenceTypeOther,
            command.ConstructionType,
            command.ConstructionTypeOther,
            command.UtilizationType,
            command.UtilizationTypeOther,
            command.TotalBuildingArea,
            command.BuildingInsurancePrice,
            command.SellingPrice,
            command.ForcedSalePrice,
            command.Remark);

        return new CreateBuildingPropertyResult(property.Id, property.BuildingDetail.Id);
    }
}