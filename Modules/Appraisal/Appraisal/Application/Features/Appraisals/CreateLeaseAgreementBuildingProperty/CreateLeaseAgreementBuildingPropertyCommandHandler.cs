using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;

namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementBuildingProperty;

/// <summary>
/// Handler for creating a lease agreement building property with its appraisal detail
/// </summary>
public class CreateLeaseAgreementBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<CreateLeaseAgreementBuildingPropertyCommand, CreateLeaseAgreementBuildingPropertyResult>
{
    public async Task<CreateLeaseAgreementBuildingPropertyResult> Handle(
        CreateLeaseAgreementBuildingPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.AddLeaseAgreementBuildingProperty();

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
            command.BuildingConditionTypeOther,
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

        // Add depreciation details if provided
        if (command.DepreciationDetails is { Count: > 0 })
            AddDepreciationDetails(property.BuildingDetail, command.DepreciationDetails);

        // Add surfaces if provided
        if (command.Surfaces is { Count: > 0 })
            AddSurfaces(property.BuildingDetail, command.Surfaces);

        // Add construction inspection if provided and building is under construction
        if (command.ConstructionInspection is { } ci && command.IsUnderConstruction != false)
            SetConstructionInspection(property, ci);

        // Update lease agreement detail if provided
        if (command.LeaseAgreement is not null)
        {
            property.LeaseAgreementDetail!.Update(
                command.LeaseAgreement.LesseeName, command.LeaseAgreement.TenantName,
                command.LeaseAgreement.LeasePeriodAsContract, command.LeaseAgreement.RemainingLeaseAsAppraisalDate,
                command.LeaseAgreement.ContractNo, command.LeaseAgreement.LeaseStartDate, command.LeaseAgreement.LeaseEndDate,
                command.LeaseAgreement.LeaseRentFee, command.LeaseAgreement.RentAdjust,
                command.LeaseAgreement.Sublease, command.LeaseAgreement.AdditionalExpenses,
                command.LeaseAgreement.LeaseTimestamp, command.LeaseAgreement.ContractRenewal,
                command.LeaseAgreement.RentalTermsImpactingPropertyUse, command.LeaseAgreement.TerminationOfLease,
                command.LeaseAgreement.Remark, command.LeaseAgreement.Banking);
        }

        // Update rental info if provided
        if (command.RentalInfo is not null)
        {
            var rentalInfo = property.RentalInfo!;
            rentalInfo.Update(
                command.RentalInfo.NumberOfYears, command.RentalInfo.FirstYearStartDate,
                command.RentalInfo.ContractRentalFeePerYear, command.RentalInfo.UpFrontTotalAmount,
                command.RentalInfo.GrowthRateType, command.RentalInfo.GrowthRatePercent,
                command.RentalInfo.GrowthIntervalYears);

            if (command.RentalInfo.UpFrontEntries is not null)
            {
                rentalInfo.ClearUpFrontEntries();
                foreach (var entry in command.RentalInfo.UpFrontEntries)
                    rentalInfo.AddUpFrontEntry(entry.AtYear, entry.UpFrontAmount);
            }

            if (command.RentalInfo.GrowthPeriodEntries is not null)
            {
                rentalInfo.ClearGrowthPeriodEntries();
                foreach (var entry in command.RentalInfo.GrowthPeriodEntries)
                    rentalInfo.AddGrowthPeriodEntry(entry.FromYear, entry.ToYear, entry.GrowthRate, entry.GrowthAmount, entry.TotalAmount);
            }

            // Compute schedule server-side from rental info fields, apply overrides
            Appraisal.Application.Features.Appraisals.Shared.RentalScheduleComputer.ComputeAndSave(rentalInfo, command.RentalInfo.ScheduleOverrides);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (command.GroupId.HasValue) appraisal.AddPropertyToGroup(command.GroupId.Value, property.Id);

        return new CreateLeaseAgreementBuildingPropertyResult(property.Id, property.BuildingDetail.Id);
    }

    private static void AddDepreciationDetails(
        BuildingAppraisalDetail buildingDetail,
        List<DepreciationItemData> items)
    {
        foreach (var item in items)
        {
            var dep = buildingDetail.AddDepreciationDetail(
                item.DepreciationMethod, item.AreaDescription, item.Area, item.Year,
                item.IsBuilding, item.PricePerSqMBeforeDepreciation, item.PriceBeforeDepreciation,
                item.PricePerSqMAfterDepreciation, item.PriceAfterDepreciation,
                item.DepreciationYearPct, item.TotalDepreciationPct, item.PriceDepreciation);

            if (item.DepreciationPeriods is { Count: > 0 })
                foreach (var p in item.DepreciationPeriods)
                    dep.AddPeriod(p.AtYear, p.ToYear, p.DepreciationPerYear,
                        p.TotalDepreciationPct, p.PriceDepreciation);
        }
    }

    private static void AddSurfaces(
        BuildingAppraisalDetail buildingDetail,
        List<SurfaceItemData> items)
    {
        foreach (var item in items)
            buildingDetail.AddSurface(
                item.FromFloorNumber, item.ToFloorNumber, item.FloorType,
                item.FloorStructureType, item.FloorStructureTypeOther,
                item.FloorSurfaceType, item.FloorSurfaceTypeOther);
    }

    private static void SetConstructionInspection(
        AppraisalProperty property,
        ConstructionInspectionData ci)
    {
        ConstructionInspection inspection;
        if (ci.IsFullDetail)
        {
            inspection = ConstructionInspection.CreateFullDetail(property.Id, ci.TotalValue);
            if (ci.WorkDetails is { Count: > 0 })
            {
                foreach (var wd in ci.WorkDetails)
                    inspection.AddWorkDetail(wd.ConstructionWorkGroupId, wd.WorkItemName,
                        wd.DisplayOrder, wd.ProportionPct, wd.PreviousProgressPct,
                        wd.CurrentProgressPct, wd.ConstructionWorkItemId);
                inspection.ComputeAllValues();
            }
        }
        else
        {
            inspection = ConstructionInspection.CreateSummary(property.Id, ci.TotalValue,
                ci.SummaryDetail, ci.SummaryPreviousProgressPct, ci.SummaryPreviousValue,
                ci.SummaryCurrentProgressPct, ci.SummaryCurrentValue, ci.Remark);
            if (ci.DocumentId.HasValue)
                inspection.SetDocument(ci.DocumentId.Value, ci.FileName, ci.FilePath, ci.FileExtension, ci.MimeType, ci.FileSizeBytes);
        }

        property.SetConstructionInspection(inspection);
    }
}
