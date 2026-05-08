using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementCondoProperty;

/// <summary>
/// Handler for updating a lease agreement condo property detail
/// </summary>
public class UpdateLeaseAgreementCondoPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateLeaseAgreementCondoPropertyCommand>
{
    public async Task<Unit> Handle(
        UpdateLeaseAgreementCondoPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(command.PropertyId)
                       ?? throw new PropertyNotFoundException(command.PropertyId);

        // 3. Validate property type
        if (property.PropertyType != PropertyType.LeaseAgreementCondo)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a lease agreement condo property");

        // 4. Get the condo detail
        var detail = property.CondoDetail
                     ?? throw new InvalidOperationException($"Condo detail not found for property {command.PropertyId}");

        // 5. Create value objects if provided
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

        // 6. Update condo detail via domain method
        detail.Update(
            propertyName: command.PropertyName,
            condoName: command.CondoName,
            buildingNumber: command.BuildingNumber,
            modelName: command.ModelName,
            builtOnTitleNumber: command.BuiltOnTitleNumber,
            condoRegistrationNumber: command.CondoRegistrationNumber,
            roomNumber: command.RoomNumber,
            floorNumber: command.FloorNumber,
            usableArea: command.UsableArea,
            coordinates: coordinates,
            address: address,
            ownerName: command.OwnerName,
            isOwnerVerified: command.IsOwnerVerified,
            buildingConditionType: command.BuildingConditionType,
            buildingConditionTypeOther: command.BuildingConditionTypeOther,
            hasObligation: command.HasObligation,
            obligationDetails: command.ObligationDetails,
            documentValidationResultType: command.DocumentValidationResultType,
            locationType: command.LocationType,
            street: command.Street,
            soi: command.Soi,
            distanceFromMainRoad: command.DistanceFromMainRoad,
            accessRoadWidth: command.AccessRoadWidth,
            rightOfWay: command.RightOfWay,
            roadSurfaceType: command.RoadSurfaceType,
            publicUtilityType: command.PublicUtilityType,
            publicUtilityTypeOther: command.PublicUtilityTypeOther,
            decorationType: command.DecorationType,
            decorationTypeOther: command.DecorationTypeOther,
            buildingAge: command.BuildingAge,
            constructionYear: command.ConstructionYear,
            numberOfFloors: command.NumberOfFloors,
            buildingFormType: command.BuildingFormType,
            constructionMaterialType: command.ConstructionMaterialType,
            roomLayoutType: command.RoomLayoutType,
            roomLayoutTypeOther: command.RoomLayoutTypeOther,
            locationViewType: command.LocationViewType,
            groundFloorMaterialType: command.GroundFloorMaterialType,
            groundFloorMaterialTypeOther: command.GroundFloorMaterialTypeOther,
            upperFloorMaterialType: command.UpperFloorMaterialType,
            upperFloorMaterialTypeOther: command.UpperFloorMaterialTypeOther,
            bathroomFloorMaterialType: command.BathroomFloorMaterialType,
            bathroomFloorMaterialTypeOther: command.BathroomFloorMaterialTypeOther,
            roofType: command.RoofType,
            roofTypeOther: command.RoofTypeOther,
            totalBuildingArea: command.TotalBuildingArea,
            isExpropriated: command.IsExpropriated,
            expropriationRemark: command.ExpropriationRemark,
            isInExpropriationLine: command.IsInExpropriationLine,
            expropriationLineRemark: command.ExpropriationLineRemark,
            royalDecree: command.RoyalDecree,
            isForestBoundary: command.IsForestBoundary,
            forestBoundaryRemark: command.ForestBoundaryRemark,
            facilityType: command.FacilityType,
            facilityTypeOther: command.FacilityTypeOther,
            environmentType: command.EnvironmentType,
            buildingInsurancePrice: command.BuildingInsurancePrice,
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            remark: command.Remark);

        // 7. Sync area details (null = no-op, list = sync)
        if (command.AreaDetails is not null)
            SyncAreaDetails(detail, command.AreaDetails);

        // 8. Update lease agreement detail if provided
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

        // 9. Update rental info if provided
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

        // 10. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return Unit.Value;
    }

    private static void SyncAreaDetails(
        CondoAppraisalDetail condoDetail,
        List<CondoAppraisalAreaDetailDto> incomingAreaDetails)
    {
        var incomingIds = incomingAreaDetails
            .Where(a => a.Id.HasValue)
            .Select(a => a.Id!.Value)
            .ToHashSet();

        var idsToRemove = condoDetail.AreaDetails
            .Where(a => !incomingIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToList();

        foreach (var id in idsToRemove)
            condoDetail.RemoveCondoAreaDetail(id);

        foreach (var dto in incomingAreaDetails)
        {
            if (dto.Id.HasValue)
            {
                var existing = condoDetail.AreaDetails.FirstOrDefault(a => a.Id == dto.Id.Value);
                existing?.UpdateArea(dto.AreaDescription, dto.AreaSize);
            }
            else
            {
                condoDetail.AddCondoAreaDetail(CondoAppraisalAreaDetail.Create(dto.AreaDescription, dto.AreaSize));
            }
        }
    }
}
