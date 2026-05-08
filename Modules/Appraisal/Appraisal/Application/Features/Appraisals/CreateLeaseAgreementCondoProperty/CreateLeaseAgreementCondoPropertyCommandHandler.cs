using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementCondoProperty;

/// <summary>
/// Handler for creating a lease agreement condo property with its appraisal detail
/// </summary>
public class CreateLeaseAgreementCondoPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<CreateLeaseAgreementCondoPropertyCommand, CreateLeaseAgreementCondoPropertyResult>
{
    public async Task<CreateLeaseAgreementCondoPropertyResult> Handle(
        CreateLeaseAgreementCondoPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Execute domain operation via aggregate
        var property = appraisal.AddLeaseAgreementCondoProperty();

        // 3. Create value objects if provided
        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null ||
            command.Province is not null || command.LandOffice is not null)
            address = AdministrativeAddress.Create(
                command.SubDistrict,
                command.District,
                command.Province,
                command.LandOffice);

        // 4. Update condo detail with additional fields
        property.CondoDetail!.Update(
            command.PropertyName,
            command.CondoName,
            command.BuildingNumber,
            command.ModelName,
            command.BuiltOnTitleNumber,
            command.CondoRegistrationNumber,
            command.RoomNumber,
            command.FloorNumber,
            command.UsableArea,
            coordinates,
            address,
            command.OwnerName,
            command.IsOwnerVerified,
            command.BuildingConditionType,
            command.BuildingConditionTypeOther,
            command.HasObligation,
            command.ObligationDetails,
            command.DocumentValidationResultType,
            command.LocationType,
            command.Street,
            command.Soi,
            command.DistanceFromMainRoad,
            command.AccessRoadWidth,
            command.RightOfWay,
            command.RoadSurfaceType,
            command.RoadSurfaceTypeOther,
            command.PublicUtilityType,
            command.PublicUtilityTypeOther,
            command.DecorationType,
            command.DecorationTypeOther,
            command.BuildingAge,
            command.ConstructionYear,
            command.NumberOfFloors,
            command.BuildingFormType,
            command.ConstructionMaterialType,
            command.RoomLayoutType,
            command.RoomLayoutTypeOther,
            command.LocationViewType,
            command.GroundFloorMaterialType,
            command.GroundFloorMaterialTypeOther,
            command.UpperFloorMaterialType,
            command.UpperFloorMaterialTypeOther,
            command.BathroomFloorMaterialType,
            command.BathroomFloorMaterialTypeOther,
            command.RoofType,
            command.RoofTypeOther,
            command.TotalBuildingArea,
            command.IsExpropriated,
            command.ExpropriationRemark,
            command.IsInExpropriationLine,
            command.ExpropriationLineRemark,
            command.RoyalDecree,
            command.IsForestBoundary,
            command.ForestBoundaryRemark,
            command.FacilityType,
            command.FacilityTypeOther,
            command.EnvironmentType,
            command.BuildingInsurancePrice,
            command.SellingPrice,
            command.ForcedSalePrice,
            command.Remark);

        // 5. Create CondoAreaDetails if provided
        if (command.AreaDetails is { Count: > 0 })
            foreach (var dto in command.AreaDetails)
                property.CondoDetail.AddCondoAreaDetail(CondoAppraisalAreaDetail.Create(dto.AreaDescription, dto.AreaSize));

        // 6. Update lease agreement detail if provided
        if (command.LeaseAgreement is not null)
        {
            property.LeaseAgreementDetail!.Update(
                command.LeaseAgreement.LesseeName, command.LeaseAgreement.LessorName,
                command.LeaseAgreement.LeasePeriodAsContract, command.LeaseAgreement.RemainingLeaseAsAppraisalDate,
                command.LeaseAgreement.ContractNo, command.LeaseAgreement.LeaseStartDate, command.LeaseAgreement.LeaseEndDate,
                command.LeaseAgreement.LeaseRentFee, command.LeaseAgreement.RentAdjust,
                command.LeaseAgreement.Sublease, command.LeaseAgreement.AdditionalExpenses,
                command.LeaseAgreement.LeaseTerminate, command.LeaseAgreement.ContractRenewal,
                command.LeaseAgreement.RentalTermsImpactingPropertyUse, command.LeaseAgreement.TerminationOfLease,
                command.LeaseAgreement.Remark);
        }

        // 7. Update rental info if provided
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

            RentalScheduleComputer.ComputeAndSave(rentalInfo, command.RentalInfo.ScheduleOverrides);
        }

        // 8. Save aggregate
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (command.GroupId.HasValue)
            appraisal.AddPropertyToGroup(command.GroupId.Value, property.Id);

        return new CreateLeaseAgreementCondoPropertyResult(property.Id, property.CondoDetail.Id);
    }
}
