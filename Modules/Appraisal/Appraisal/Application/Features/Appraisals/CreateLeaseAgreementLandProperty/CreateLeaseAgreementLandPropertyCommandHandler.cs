using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementLandProperty;

/// <summary>
/// Handler for creating a new lease agreement land property with detail
/// </summary>
public class CreateLeaseAgreementLandPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<CreateLeaseAgreementLandPropertyCommand, CreateLeaseAgreementLandPropertyResult>
{
    public async Task<CreateLeaseAgreementLandPropertyResult> Handle(
        CreateLeaseAgreementLandPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.AddLeaseAgreementLandProperty();

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

        var landDetail = property.LandDetail!;

        landDetail.Update(
            command.PropertyName,
            command.LandDescription,
            coordinates,
            address,
            command.OwnerName,
            command.IsOwnerVerified,
            command.HasObligation,
            command.ObligationDetails,
            command.IsLandLocationVerified,
            command.LandCheckMethodType,
            command.LandCheckMethodTypeOther,
            command.Street,
            command.Soi,
            command.DistanceFromMainRoad,
            command.Village,
            command.AddressLocation,
            command.LandShapeType,
            command.UrbanPlanningType,
            command.LandZoneType,
            command.LandZoneTypeOther,
            command.PlotLocationType,
            command.PlotLocationTypeOther,
            command.LandFillType,
            command.LandFillTypeOther,
            command.LandFillPercent,
            command.SoilLevel,
            command.AccessRoadWidth,
            command.RightOfWay,
            command.RoadFrontage,
            command.NumberOfSidesFacingRoad,
            command.RoadPassInFrontOfLand,
            command.LandAccessibilityType,
            command.LandAccessibilityRemark,
            command.RoadSurfaceType,
            command.RoadSurfaceTypeOther,
            command.HasElectricity,
            command.ElectricityDistance,
            command.PublicUtilityType,
            command.PublicUtilityTypeOther,
            command.LandUseType,
            command.LandUseTypeOther,
            command.LandEntranceExitType,
            command.LandEntranceExitTypeOther,
            command.TransportationAccessType,
            command.TransportationAccessTypeOther,
            command.PropertyAnticipationType,
            command.PropertyAnticipationTypeOther,
            command.IsExpropriated,
            command.ExpropriationRemark,
            command.IsInExpropriationLine,
            command.ExpropriationLineRemark,
            command.RoyalDecree,
            command.IsEncroached,
            command.EncroachmentRemark,
            command.EncroachmentArea,
            command.IsLandlocked,
            command.LandlockedRemark,
            command.IsForestBoundary,
            command.ForestBoundaryRemark,
            command.OtherLegalLimitations,
            command.EvictionType,
            command.EvictionTypeOther,
            command.AllocationType,
            command.NorthAdjacentArea,
            command.NorthBoundaryLength,
            command.SouthAdjacentArea,
            command.SouthBoundaryLength,
            command.EastAdjacentArea,
            command.EastBoundaryLength,
            command.WestAdjacentArea,
            command.WestBoundaryLength,
            command.PondArea,
            command.PondDepth,
            command.HasBuilding,
            command.HasBuildingOther,
            command.Remark);

        // Add land titles if provided
        if (command.Titles is { Count: > 0 })
            foreach (var titleData in command.Titles)
            {
                var title = LandTitle.Create(
                    landDetail.Id,
                    titleData.TitleNumber,
                    titleData.TitleType);

                LandArea? area = null;
                if (titleData.Rai.HasValue || titleData.Ngan.HasValue || titleData.SquareWa.HasValue)
                    area = LandArea.Create(titleData.Rai, titleData.Ngan, titleData.SquareWa);

                title.Update(
                    titleData.BookNumber,
                    titleData.PageNumber,
                    titleData.LandParcelNumber,
                    titleData.SurveyNumber,
                    titleData.MapSheetNumber,
                    titleData.Rawang,
                    titleData.AerialMapName,
                    titleData.AerialMapNumber,
                    area,
                    titleData.BoundaryMarkerType,
                    titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType,
                    titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa,
                    titleData.GovernmentPrice,
                    titleData.Remark);

                landDetail.AddTitle(title);
            }

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

            if (command.RentalInfo.ScheduleEntries is not null)
            {
                rentalInfo.ClearScheduleEntries();
                foreach (var entry in command.RentalInfo.ScheduleEntries)
                    rentalInfo.AddScheduleEntry(entry.Year, entry.ContractStart, entry.ContractEnd,
                        entry.UpFront, entry.ContractRentalFee, entry.TotalAmount, entry.ContractRentalFeeGrowthRatePercent);
            }

            if (command.RentalInfo.ScheduleOverrides is not null)
            {
                rentalInfo.ClearScheduleOverrides();
                foreach (var o in command.RentalInfo.ScheduleOverrides)
                    rentalInfo.SetScheduleOverride(o.Year, o.UpFront, o.ContractRentalFee);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (command.GroupId.HasValue) appraisal.AddPropertyToGroup(command.GroupId.Value, property.Id);

        return new CreateLeaseAgreementLandPropertyResult(property.Id, landDetail.Id);
    }
}
