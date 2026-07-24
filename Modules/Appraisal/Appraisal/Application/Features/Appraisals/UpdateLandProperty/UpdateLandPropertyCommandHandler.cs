using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Application.Features.Appraisals.UpdateLandProperty;

/// <summary>
/// Handler for updating a land property detail
/// </summary>
public class UpdateLandPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateLandPropertyCommand>
{
    public async Task<Unit> Handle(
        UpdateLandPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.GetProperty(command.PropertyId)
                       ?? throw new PropertyNotFoundException(command.PropertyId);

        if (property.PropertyType != PropertyType.Land && property.PropertyType != PropertyType.LeaseAgreementLand)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a land property");

        var landDetail = property.LandDetail
                         ?? throw new InvalidOperationException(
                             $"Land detail not found for property {command.PropertyId}");

        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        Address? address = null;
        if (command.SubDistrict is not null || command.District is not null || command.Province is not null)
            address = Address.Create(
                command.SubDistrict,
                command.District,
                command.Province);
        Address? dopaAddress = null;
        if (command.DopaSubDistrict is not null || command.DopaDistrict is not null || command.DopaProvince is not null)
            dopaAddress = Address.Create(command.DopaSubDistrict, command.DopaDistrict, command.DopaProvince);

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
            command.LandShapeTypeOther,
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
            command.Remark,
            command.IsRentedOut,
            landOffice: command.LandOffice,
            dopaAddress: dopaAddress);

        // Sync land titles (null = no-op, empty list = clear all)
        if (command.Titles is not null)
            SyncTitles(landDetail, command.Titles);

        // Rental: if rented out, ensure lease/rental owned entities exist and apply updates;
        // otherwise clear them so owned rows are cascade-deleted.
        if (command.IsRentedOut == true)
        {
            if (property.LeaseAgreementDetail is null)
                property.SetLeaseAgreementDetail(LeaseAgreementDetail.Create(property.Id));
            if (property.RentalInfo is null)
                property.SetRentalInfo(RentalInfo.Create(property.Id));

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
        }
        else
        {
            property.ClearLeaseAgreementDetail();
            property.ClearRentalInfo();
        }

        return Unit.Value;
    }

    private static void SyncTitles(LandAppraisalDetail landDetail, List<LandTitleItemData> incomingTitles)
    {
        var incomingIds = incomingTitles
            .Where(t => t.Id.HasValue)
            .Select(t => t.Id!.Value)
            .ToHashSet();

        // Delete titles not in the incoming list
        var titlesToRemove = landDetail.Titles
            .Where(t => !incomingIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToList();
        foreach (var id in titlesToRemove)
            landDetail.RemoveTitle(id);

        // Add or update
        foreach (var titleData in incomingTitles)
        {
            LandArea? area = null;
            if (titleData.Rai.HasValue || titleData.Ngan.HasValue || titleData.SquareWa.HasValue)
                area = LandArea.Create(titleData.Rai, titleData.Ngan, titleData.SquareWa);

            if (titleData.Id.HasValue)
            {
                // Update existing
                var existing = landDetail.Titles.FirstOrDefault(t => t.Id == titleData.Id.Value);
                existing?.Update(
                    titleData.BookNumber, titleData.PageNumber,
                    titleData.LandParcelNumber, titleData.SurveyNumber,
                    titleData.MapSheetNumber, titleData.Rawang,
                    titleData.AerialMapName, titleData.AerialMapNumber,
                    area, titleData.BoundaryMarkerType, titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType, titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa, titleData.GovernmentPrice,
                    titleData.Remark);
            }
            else
            {
                // Create new
                var title = LandTitle.Create(landDetail.Id, titleData.TitleNumber, titleData.TitleType);
                title.Update(
                    titleData.BookNumber, titleData.PageNumber,
                    titleData.LandParcelNumber, titleData.SurveyNumber,
                    titleData.MapSheetNumber, titleData.Rawang,
                    titleData.AerialMapName, titleData.AerialMapNumber,
                    area, titleData.BoundaryMarkerType, titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType, titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa, titleData.GovernmentPrice,
                    titleData.Remark);
                landDetail.AddTitle(title);
            }
        }
    }
}
