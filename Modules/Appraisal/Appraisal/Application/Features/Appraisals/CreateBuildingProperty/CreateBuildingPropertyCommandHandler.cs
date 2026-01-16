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
            command.OwnerName,
            command.Description);

        property.BuildingDetail!.Update(
            command.PropertyName,
            command.BuildingNumber,
            command.ModelName,
            command.BuiltOnTitleNumber,
            command.HouseNumber,
            isOwnerVerified: command.IsOwnerVerified,
            hasObligation: command.HasObligation,
            obligationDetails: command.ObligationDetails,
            buildingCondition: command.BuildingCondition,
            isUnderConstruction: command.IsUnderConstruction,
            constructionCompletionPercent: command.ConstructionCompletionPercent,
            constructionLicenseExpirationDate: command.ConstructionLicenseExpirationDate,
            isAppraisable: command.IsAppraisable,
            buildingType: command.BuildingType,
            buildingTypeOther: command.BuildingTypeOther,
            numberOfFloors: command.NumberOfFloors,
            decorationType: command.DecorationType,
            decorationTypeOther: command.DecorationTypeOther,
            isEncroached: command.IsEncroached,
            encroachmentRemark: command.EncroachmentRemark,
            encroachmentArea: command.EncroachmentArea,
            buildingMaterial: command.BuildingMaterial,
            buildingStyle: command.BuildingStyle,
            isResidential: command.IsResidential,
            buildingAge: command.BuildingAge,
            constructionYear: command.ConstructionYear,
            isResidentialRemark: command.IsResidentialRemark,
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
            otherPurposeUsage: command.OtherPurposeUsage,
            totalBuildingArea: command.TotalBuildingArea,
            buildingInsurancePrice: command.BuildingInsurancePrice,
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            remark: command.Remark);

        return new CreateBuildingPropertyResult(property.Id, property.BuildingDetail.Id);
    }
}