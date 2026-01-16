namespace Appraisal.Application.Features.Appraisals.UpdateBuildingProperty;

/// <summary>
/// Handler for updating a building property detail
/// </summary>
public class UpdateBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateBuildingPropertyCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateBuildingPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);
        
        var property = appraisal.GetProperty(command.PropertyId)
            ?? throw new PropertyNotFoundException(command.PropertyId);
        
        if (property.PropertyType != PropertyType.Building)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a building property");
        
        var detail = property.BuildingDetail
            ?? throw new InvalidOperationException($"Building detail not found for property {command.PropertyId}");

        detail.Update(
            propertyName: command.PropertyName,
            buildingNumber: command.BuildingNumber,
            modelName: command.ModelName,
            builtOnTitleNumber: command.BuiltOnTitleNumber,
            houseNumber: command.HouseNumber,
            ownerName: command.OwnerName,
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

        return MediatR.Unit.Value;
    }
}
