using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreementBuildingProperty;

/// <summary>
/// Handler for getting a lease agreement building property with its detail
/// </summary>
public class GetLeaseAgreementBuildingPropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetLeaseAgreementBuildingPropertyQuery, GetLeaseAgreementBuildingPropertyResult>
{
    public async Task<GetLeaseAgreementBuildingPropertyResult> Handle(
        GetLeaseAgreementBuildingPropertyQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var property = appraisal.GetProperty(query.PropertyId)
                       ?? throw new PropertyNotFoundException(query.PropertyId);

        if (property.PropertyType != PropertyType.LeaseAgreementBuilding)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a lease agreement building property");

        var detail = property.BuildingDetail
                     ?? throw new InvalidOperationException(
                         $"Building detail not found for property {query.PropertyId}");

        var surfaceDtos = detail.Surfaces
            .OrderBy(s => s.FromFloorNumber).ThenBy(s => s.ToFloorNumber)
            .Select(s => new BuildingAppraisalSurfaceDto(
                s.Id, s.FromFloorNumber, s.ToFloorNumber, s.FloorType,
                s.FloorStructureType, s.FloorStructureTypeOther,
                s.FloorSurfaceType, s.FloorSurfaceTypeOther
            )).ToList();

        var depreciationDtos = detail.DepreciationDetails
            .OrderBy(d => d.CreatedAt)
            .Select(d => new BuildingAppraisalDepreciationDetailDto(
                d.Id,
                d.AreaDescription,
                d.Area,
                d.PricePerSqMBeforeDepreciation,
                d.PriceBeforeDepreciation,
                d.Year,
                d.IsBuilding,
                d.DepreciationMethod,
                d.DepreciationYearPct,
                d.TotalDepreciationPct,
                d.PriceDepreciation,
                d.PricePerSqMAfterDepreciation,
                d.PriceAfterDepreciation,
                d.DepreciationPeriods
                    .OrderBy(p => p.AtYear).ThenBy(p => p.ToYear)
                    .Select(p => new BuildingAppraisalDepreciationPeriodDto(
                        p.Id, p.AtYear, p.ToYear, p.DepreciationPerYear,
                        p.TotalDepreciationPct, p.PriceDepreciation
                    )).ToList()
            )).ToList();

        var constructionDto = property.ConstructionInspection is { } ci
            ? new ConstructionInspectionDto(
                ci.Id, ci.AppraisalPropertyId, ci.IsFullDetail, ci.TotalValue,
                ci.SummaryDetail, ci.SummaryPreviousProgressPct, ci.SummaryPreviousValue,
                ci.SummaryCurrentProgressPct, ci.SummaryCurrentValue, ci.Remark,
                ci.DocumentId, ci.FileName, ci.FilePath,
                ci.FileExtension, ci.MimeType, ci.FileSizeBytes,
                ci.WorkDetails.OrderBy(w => w.DisplayOrder).Select(w => new ConstructionWorkDetailDto(
                    w.Id, w.ConstructionWorkGroupId, w.ConstructionWorkItemId, w.WorkItemName,
                    w.DisplayOrder, w.ConstructionValue, w.PreviousProgressPct, w.CurrentProgressPct,
                    w.ProportionPct, w.CurrentProportionPct, w.PreviousPropertyValue, w.CurrentPropertyValue
                )).ToList())
            : null;

        return new GetLeaseAgreementBuildingPropertyResult(
            property.Id,
            property.AppraisalId,
            property.SequenceNumber,
            property.PropertyType.ToString(),
            property.Description,
            detail.Id,
            detail.PropertyName,
            detail.BuildingNumber,
            detail.ModelName,
            detail.BuiltOnTitleNumber,
            HouseNumber: detail.HouseNumber,
            OwnerName: detail.OwnerName,
            IsOwnerVerified: detail.IsOwnerVerified,
            HasObligation: detail.HasObligation,
            ObligationDetails: detail.ObligationDetails,
            BuildingConditionType: detail.BuildingConditionType,
            BuildingConditionTypeOther: detail.BuildingConditionTypeOther,
            IsUnderConstruction: detail.IsUnderConstruction,
            ConstructionCompletionPercent: detail.ConstructionCompletionPercent,
            ConstructionLicenseExpirationDate: detail.ConstructionLicenseExpirationDate,
            IsAppraisable: detail.IsAppraisable,
            BuildingType: detail.BuildingType,
            BuildingTypeOther: detail.BuildingTypeOther,
            NumberOfFloors: detail.NumberOfFloors,
            DecorationType: detail.DecorationType,
            DecorationTypeOther: detail.DecorationTypeOther,
            IsEncroachingOthers: detail.IsEncroachingOthers,
            EncroachingOthersRemark: detail.EncroachingOthersRemark,
            EncroachingOthersArea: detail.EncroachingOthersArea,
            BuildingMaterialType: detail.BuildingMaterialType,
            BuildingStyleType: detail.BuildingStyleType,
            IsResidential: detail.IsResidential ?? false,
            BuildingAge: detail.BuildingAge,
            ConstructionYear: detail.ConstructionYear,
            ResidentialRemark: detail.ResidentialRemark,
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
            UtilizationTypeOther: detail.UtilizationTypeOther,
            TotalBuildingArea: detail.TotalBuildingArea,
            BuildingInsurancePrice: detail.BuildingInsurancePrice,
            SellingPrice: detail.SellingPrice,
            ForcedSalePrice: detail.ForcedSalePrice,
            Remark: detail.Remark,
            DepreciationDetails: depreciationDtos,
            Surfaces: surfaceDtos,
            ConstructionInspection: constructionDto,
            // Lease Agreement & Rental Info
            LeaseAgreement: LeaseAgreementMapper.MapLeaseAgreement(property.LeaseAgreementDetail),
            RentalInfo: LeaseAgreementMapper.MapRentalInfo(property.RentalInfo));
    }
}
