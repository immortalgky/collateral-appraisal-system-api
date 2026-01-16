namespace Appraisal.Application.Features.Appraisals.GetBuildingProperty;

/// <summary>
/// Handler for getting a building property with its detail
/// </summary>
public class GetBuildingPropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetBuildingPropertyQuery, GetBuildingPropertyResult>
{
    public async Task<GetBuildingPropertyResult> Handle(
        GetBuildingPropertyQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);
        
        var property = appraisal.GetProperty(query.PropertyId)
                       ?? throw new PropertyNotFoundException(query.PropertyId);

        if (property.PropertyType != PropertyType.Building)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a building property");
        
        var detail = property.BuildingDetail
                     ?? throw new InvalidOperationException(
                         $"Building detail not found for property {query.PropertyId}");
        
        return new GetBuildingPropertyResult(
            PropertyId: property.Id,
            AppraisalId: property.AppraisalId,
            SequenceNumber: property.SequenceNumber,
            PropertyType: property.PropertyType.ToString(),
            Description: property.Description,
            DetailId: detail.Id,
            PropertyName: detail.PropertyName,
            BuildingNumber: detail.BuildingNumber,
            ModelName: detail.ModelName,
            BuiltOnTitleNumber: detail.BuiltOnTitleNumber,
            HouseNumber: detail.HouseNumber,
            OwnerName: detail.OwnerName,
            IsOwnerVerified: detail.IsOwnerVerified,
            HasObligation: detail.HasObligation,
            ObligationDetails: detail.ObligationDetails,
            BuildingCondition: detail.BuildingConditionType,
            IsUnderConstruction: detail.IsUnderConstruction,
            ConstructionCompletionPercent: detail.ConstructionCompletionPercent,
            ConstructionLicenseExpirationDate: detail.ConstructionLicenseExpirationDate,
            IsAppraisable: detail.IsAppraisable,
            BuildingType: detail.BuildingType,
            BuildingTypeOther: detail.BuildingTypeOther,
            NumberOfFloors: detail.NumberOfFloors,
            DecorationType: detail.DecorationType,
            DecorationTypeOther: detail.DecorationTypeOther,
            IsEncroached: detail.IsEncroached,
            EncroachmentRemark: detail.EncroachmentRemark,
            EncroachmentArea: detail.EncroachmentArea,
            BuildingMaterial: detail.BuildingMaterialType,
            BuildingStyle: detail.BuildingStyleType,
            IsResidential: detail.IsResidential ?? false,
            BuildingAge: detail.BuildingAge,
            ConstructionYear: detail.ConstructionYear,
            IsResidentialRemark: detail.ResidentialRemark,
            ConstructionStyleType: detail.ConstructionStyleType,
            ConstructionStyleRemark: detail.ConstructionStyleRemark,
            StructureType: detail.StructureType,
            StructureTypeOther: detail.StructureTypeOther,
            RoofFrameType: detail.RoofFrameType,
            RoofFrameTypeOther: detail.RoofFrameTypeOther,
            RoofType: detail.RoofType,
            RoofTypeOther: detail.RoofTypeOther,
            CeilingType: detail.CeilingType,
            CeilingTypeOther: detail.CeilingTypeOther,
            InteriorWallType: detail.InteriorWallType,
            InteriorWallTypeOther: detail.InteriorWallTypeOther,
            ExteriorWallType: detail.ExteriorWallType,
            ExteriorWallTypeOther: detail.ExteriorWallTypeOther,
            FenceType: detail.FenceType,
            FenceTypeOther: detail.FenceTypeOther,
            ConstructionType: detail.ConstructionType,
            ConstructionTypeOther: detail.ConstructionTypeOther,
            UtilizationType: detail.UtilizationType,
            OtherPurposeUsage: detail.UtilizationTypeOther,
            TotalBuildingArea: detail.TotalBuildingArea,
            BuildingInsurancePrice: detail.BuildingInsurancePrice,
            SellingPrice: detail.SellingPrice,
            ForcedSalePrice: detail.ForcedSalePrice,
            Remark: detail.Remark);
    }
}