using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

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
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Execute domain operation via aggregate (creates BOTH property + detail)
        var property = appraisal.AddBuildingProperty(
            command.OwnerName,
            command.Description);

        // 3. Update detail with additional fields
        property.BuildingDetail!.Update(
            propertyName: command.PropertyName,
            buildingNumber: command.BuildingNumber,
            modelName: command.ModelName,
            builtOnTitleNumber: command.BuiltOnTitleNumber,
            houseNumber: command.HouseNumber,
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

        // 4. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        // 5. Return both IDs
        return new CreateBuildingPropertyResult(property.Id, property.BuildingDetail.Id);
    }
}
