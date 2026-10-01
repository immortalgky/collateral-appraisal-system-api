using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Application.Features.Appraisals.UpdateBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateCondoProperty;
using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateLandProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementCondoProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementLandAndBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementLandProperty;
using Appraisal.Application.Features.Appraisals.UpdateMachineryProperty;
using Appraisal.Application.Features.Appraisals.UpdateVehicleProperty;
using Appraisal.Application.Features.Appraisals.UpdateVesselProperty;
using Appraisal.Application.Features.FireInsuranceRates.GetFireInsuranceRates;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Contracts.Appraisals.Dto;
using MediatR;
using NSubstitute;
using Shared.Exceptions;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Pins what the real property-page update handlers do, so the write logic can move into shared
/// appliers (also used by the data-correction command) without the real pages changing behaviour:
/// which fields are written, how the child collections are synced, and the side effects that stay
/// with the handler (valuation recompute, condo insurance derivation, rental clearing).
/// </summary>
public class UpdatePropertyHandlerCharacterisationTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly IAppraisalRepository _repository = Substitute.For<IAppraisalRepository>();

    // Only Received()/DidNotReceive() are asserted on it; the primary constructor just stores its
    // arguments, so nulls never touch a database.
    private readonly AppraisalValuationSummaryService _valuationSummary =
        Substitute.For<AppraisalValuationSummaryService>(null, null, null, null, null);

    private readonly ISender _mediator = Substitute.For<ISender>();

    private (AppraisalAggregate Appraisal, AppraisalProperty Property) Seed(
        Func<AppraisalAggregate, AppraisalProperty> add)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", new DateTime(2026, 1, 1));
        var property = add(appraisal);
        property.Id = Guid.NewGuid();
        _repository
            .GetByIdWithPropertiesAsync(appraisal.Id, Arg.Any<CancellationToken>())
            .Returns(appraisal);
        return (appraisal, property);
    }

    private Task RecomputeReceived(Guid appraisalId, int times) =>
        _valuationSummary.Received(times).RecomputeAsync(appraisalId, Arg.Any<CancellationToken>());

    private void GivenCondoRate(string code, decimal ratePerSqm) =>
        _mediator
            .Send(Arg.Any<GetFireInsuranceRatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetFireInsuranceRatesResult(
                [new FireInsuranceRateDto(code, "cond", "Condo", ratePerSqm, 1)]));

    private static RentalInfoData ThreeYearRental() => new(
        NumberOfYears: 3,
        FirstYearStartDate: new DateTime(2026, 1, 1),
        ContractRentalFeePerYear: 1200m,
        UpFrontTotalAmount: 0m,
        GrowthRateType: "Property",
        GrowthRatePercent: 0m,
        GrowthIntervalYears: 1,
        UpFrontEntries: [new UpFrontEntryData(new DateTime(2026, 1, 1), 500m)]);

    private static Guid NewId() => Guid.NewGuid();

    // ───────────────────────────── Land ─────────────────────────────

    private UpdateLandPropertyCommandHandler LandHandler() => new(_repository);

    [Fact]
    public async Task Land_overwrites_scalars_builds_value_objects_and_nulls_what_is_omitted()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        property.LandDetail!.Update(ownerName: "Owner A", street: "Old street", landOffice: "Old office");

        await LandHandler().Handle(new UpdateLandPropertyCommand(
            appraisal.Id, property.Id,
            OwnerNameLand: "Owner B",
            Latitude: 13.5m, Longitude: 100.5m,
            SubDistrict: "S", District: "D", Province: "P",
            DopaProvince: "DP",
            LandOffice: "New office",
            Village: "Moo 3"), Ct);

        var land = property.LandDetail!;
        Assert.Equal("Owner B", land.OwnerName);
        Assert.Equal(13.5m, land.Coordinates!.Latitude);
        Assert.Equal("P", land.Address!.Province);
        Assert.Equal("DP", land.DopaAddress!.Province);
        Assert.Equal("New office", land.LandOffice);
        Assert.Equal("Moo 3", land.Village);
        Assert.Null(land.Street); // full overwrite: omitted scalar becomes null
    }

    [Fact]
    public async Task Land_null_titles_and_deductions_leave_the_collections_alone()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var land = property.LandDetail!;
        land.AddTitle(LandTitle.Create(land.Id, "111", "DEED"));
        land.AddDeduction(LandAreaDeduction.Create(land.Id, "01"));

        await LandHandler().Handle(new UpdateLandPropertyCommand(appraisal.Id, property.Id), Ct);

        Assert.Single(land.Titles);
        Assert.Single(land.Deductions);
    }

    [Fact]
    public async Task Land_syncs_titles_by_id_updating_adding_and_removing()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var land = property.LandDetail!;
        var keep = LandTitle.Create(land.Id, "111", "DEED"); keep.Id = NewId();
        var drop = LandTitle.Create(land.Id, "222", "DEED"); drop.Id = NewId();
        land.AddTitle(keep);
        land.AddTitle(drop);

        await LandHandler().Handle(new UpdateLandPropertyCommand(
            appraisal.Id, property.Id,
            Titles:
            [
                new LandTitleItemData(keep.Id, "111", "DEED", BookNumber: "B1", Rai: 2m, Ngan: 1m, SquareWa: 30m),
                new LandTitleItemData(null, "333", "NS3", Remark: "new"),
            ]), Ct);

        Assert.Equal(2, land.Titles.Count);
        Assert.DoesNotContain(land.Titles, t => t.Id == drop.Id);
        Assert.Equal("B1", keep.BookNumber);
        Assert.Equal(2m, keep.Area!.Rai);
        var added = land.Titles.Single(t => t.TitleNumber == "333");
        Assert.Equal("NS3", added.TitleType);
        Assert.Equal("new", added.Remark);
        Assert.Null(added.Area); // no rai/ngan/wa supplied
    }

    [Fact]
    public async Task Land_syncs_deductions_and_keeps_the_stored_total_in_step()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var land = property.LandDetail!;
        var first = LandAreaDeduction.Create(land.Id, "01"); first.Id = NewId();
        first.Update(null, 5m, null);
        var gone = LandAreaDeduction.Create(land.Id, "02"); gone.Id = NewId();
        gone.Update(null, 7m, null);
        land.AddDeduction(first);
        land.AddDeduction(gone);

        await LandHandler().Handle(new UpdateLandPropertyCommand(
            appraisal.Id, property.Id,
            LandAreaDeductions:
            [
                new LandAreaDeductionData(first.Id, "03", null, 10m, "edited"),
                new LandAreaDeductionData(null, "99", "other", 4m, null),
            ]), Ct);

        Assert.Equal(2, land.Deductions.Count);
        Assert.Equal("03", first.ReasonCode);
        Assert.Equal(10m, first.AreaInSqWa);
        Assert.Equal(14m, land.DeductedAreaInSqWa);
    }

    [Fact]
    public async Task Land_rejects_deductions_larger_than_the_deed()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());

        await Assert.ThrowsAsync<DomainException>(() =>
            LandHandler().Handle(new UpdateLandPropertyCommand(
                appraisal.Id, property.Id,
                Titles: [new LandTitleItemData(null, "111", "DEED", Rai: 0m, Ngan: 0m, SquareWa: 100m)],
                LandAreaDeductions: [new LandAreaDeductionData(null, "01", null, 150m, null)]), Ct));
    }

    [Fact]
    public async Task Land_rented_out_creates_lease_and_rental_and_computes_the_schedule()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        Assert.Null(property.RentalInfo);

        await LandHandler().Handle(new UpdateLandPropertyCommand(
            appraisal.Id, property.Id,
            IsRentedOut: true,
            LeaseAgreement: new LeaseAgreementData(LesseeName: "Lessee"),
            RentalInfo: ThreeYearRental()), Ct);

        Assert.Equal("Lessee", property.LeaseAgreementDetail!.LesseeName);
        Assert.Equal(3, property.RentalInfo!.NumberOfYears);
        Assert.Single(property.RentalInfo.UpFrontEntries);
        Assert.Equal(3, property.RentalInfo.ScheduleEntries.Count);
    }

    [Fact]
    public async Task Land_not_rented_out_clears_lease_and_rental()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        property.SetLeaseAgreementDetail(LeaseAgreementDetail.Create(property.Id));
        property.SetRentalInfo(RentalInfo.Create(property.Id));

        await LandHandler().Handle(new UpdateLandPropertyCommand(
            appraisal.Id, property.Id, IsRentedOut: false), Ct);

        Assert.Null(property.LeaseAgreementDetail);
        Assert.Null(property.RentalInfo);
    }

    [Fact]
    public async Task Land_handler_rejects_a_property_of_another_type()
    {
        var (appraisal, property) = Seed(a => a.AddBuildingProperty());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LandHandler().Handle(new UpdateLandPropertyCommand(appraisal.Id, property.Id), Ct));
    }

    // The one deliberate behaviour change of the applier refactor: an existing title's number/type used to be
    // frozen after creation (LandTitle.Update never touched them). The shared title sync now applies them.

    [Fact]
    public async Task Land_changes_the_number_and_type_of_an_existing_title()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var land = property.LandDetail!;
        var title = LandTitle.Create(land.Id, "111", "DEED"); title.Id = NewId();
        land.AddTitle(title);

        await LandHandler().Handle(new UpdateLandPropertyCommand(
            appraisal.Id, property.Id, Titles: [new LandTitleItemData(title.Id, "112", "NS3")]), Ct);

        Assert.Equal("112", title.TitleNumber);
        Assert.Equal("NS3", title.TitleType);
    }

    [Fact]
    public async Task Land_keeps_the_number_and_type_when_the_payload_leaves_them_blank()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var land = property.LandDetail!;
        var title = LandTitle.Create(land.Id, "111", "DEED"); title.Id = NewId();
        land.AddTitle(title);

        await LandHandler().Handle(new UpdateLandPropertyCommand(
            appraisal.Id, property.Id, Titles: [new LandTitleItemData(title.Id, "", null!, Remark: "r")]), Ct);

        Assert.Equal("111", title.TitleNumber);
        Assert.Equal("DEED", title.TitleType);
        Assert.Equal("r", title.Remark);
    }

    [Fact]
    public async Task LeaseLandAndBuilding_changes_the_number_of_an_existing_title_through_the_same_sync()
    {
        var (appraisal, property) = Seed(a => a.AddLeaseAgreementLandAndBuildingProperty());
        var land = property.LandDetail!;
        var title = LandTitle.Create(land.Id, "111", "DEED"); title.Id = NewId();
        land.AddTitle(title);

        await new UpdateLeaseAgreementLandAndBuildingPropertyCommandHandler(_repository, _valuationSummary).Handle(
            new UpdateLeaseAgreementLandAndBuildingPropertyCommand(
                appraisal.Id, property.Id, Titles: [new LandTitleItemData(title.Id, "112", "DEED")]), Ct);

        Assert.Equal("112", title.TitleNumber);
    }

    // ───────────────────────────── Building ─────────────────────────────

    private UpdateBuildingPropertyCommandHandler BuildingHandler() => new(_repository, _valuationSummary);

    [Fact]
    public async Task Building_writes_fields_syncs_children_clears_construction_and_recomputes()
    {
        var (appraisal, property) = Seed(a => a.AddBuildingProperty());
        var building = property.BuildingDetail!;
        var dep = building.AddDepreciationDetail("Period", "old", 10m, 2020, true, 1m, 1m, 1m, 1m, 1m, 1m, 1m);
        dep.Id = NewId();
        dep.AddPeriod(1, 2, 1m, 1m, 1m);
        var gone = building.AddDepreciationDetail("Period", "gone", 1m, 2020, true, 1m, 1m, 1m, 1m, 1m, 1m, 1m);
        gone.Id = NewId();
        var surface = building.AddSurface(1, 2, "F", null, null, null, null);
        surface.Id = NewId();
        property.SetConstructionInspection(ConstructionInspection.CreateSummary(property.Id, 100m, null, null, null, null, null, null));

        await BuildingHandler().Handle(new UpdateBuildingPropertyCommand(
            appraisal.Id, property.Id,
            OwnerNameBuilding: "Owner",
            TotalBuildingArea: 250m,
            SellingPrice: 900m,
            DepreciationDetails:
            [
                new DepreciationItemData(dep.Id, "Period", "edited", 12m, 2021,
                    DepreciationPeriods: [new DepreciationPeriodItemData(1, 3, 2m, 6m, 60m)]),
                new DepreciationItemData(null, "Gross", "new", 5m, 2022),
            ],
            Surfaces:
            [
                new SurfaceItemData(surface.Id, 1, 3, "G"),
                new SurfaceItemData(null, 4, 5, "H"),
            ]), Ct);

        Assert.Equal("Owner", building.OwnerName);
        Assert.Equal(250m, building.TotalBuildingArea);
        Assert.Equal(900m, building.SellingPrice);

        Assert.Equal(2, building.DepreciationDetails.Count);
        Assert.DoesNotContain(building.DepreciationDetails, d => d.Id == gone.Id);
        Assert.Equal("edited", dep.AreaDescription);
        var period = Assert.Single(dep.DepreciationPeriods); // periods are rebuilt, not appended
        Assert.Equal(3, period.ToYear);
        Assert.Contains(building.DepreciationDetails, d => d.AreaDescription == "new");

        Assert.Equal(2, building.Surfaces.Count);
        Assert.Equal(3, surface.ToFloorNumber);

        // ConstructionInspection omitted => cleared
        Assert.Null(property.ConstructionInspection);

        await RecomputeReceived(appraisal.Id, 1);
    }

    [Fact]
    public async Task Building_null_children_are_left_alone()
    {
        var (appraisal, property) = Seed(a => a.AddBuildingProperty());
        var building = property.BuildingDetail!;
        building.AddDepreciationDetail("Period", "keep", 1m, 2020, true, 1m, 1m, 1m, 1m, 1m, 1m, 1m);
        building.AddSurface(1, 2, "F", null, null, null, null);

        await BuildingHandler().Handle(new UpdateBuildingPropertyCommand(appraisal.Id, property.Id), Ct);

        Assert.Single(building.DepreciationDetails);
        Assert.Single(building.Surfaces);
    }

    [Fact]
    public async Task Building_construction_inspection_is_created_when_under_construction()
    {
        var (appraisal, property) = Seed(a => a.AddBuildingProperty());

        await BuildingHandler().Handle(new UpdateBuildingPropertyCommand(
            appraisal.Id, property.Id,
            IsUnderConstruction: true,
            ConstructionInspection: new ConstructionInspectionData(
                IsFullDetail: false, TotalValue: 500m, SummaryDetail: "half built")), Ct);

        Assert.Equal(500m, property.ConstructionInspection!.TotalValue);
    }

    [Fact]
    public async Task Building_construction_inspection_is_cleared_when_not_under_construction()
    {
        var (appraisal, property) = Seed(a => a.AddBuildingProperty());
        property.SetConstructionInspection(ConstructionInspection.CreateSummary(property.Id, 100m, null, null, null, null, null, null));

        await BuildingHandler().Handle(new UpdateBuildingPropertyCommand(
            appraisal.Id, property.Id,
            IsUnderConstruction: false,
            ConstructionInspection: new ConstructionInspectionData(false, 500m)), Ct);

        Assert.Null(property.ConstructionInspection);
    }

    // ───────────────────────────── Land and building ─────────────────────────────

    private UpdateLandAndBuildingPropertyCommandHandler LandAndBuildingHandler() =>
        new(_repository, _valuationSummary);

    [Fact]
    public async Task LandAndBuilding_writes_both_details_syncs_titles_rental_and_recomputes()
    {
        var (appraisal, property) = Seed(a => a.AddLandAndBuildingProperty());
        var land = property.LandDetail!;
        var keep = LandTitle.Create(land.Id, "111", "DEED"); keep.Id = NewId();
        land.AddTitle(keep);

        await LandAndBuildingHandler().Handle(new UpdateLandAndBuildingPropertyCommand(
            appraisal.Id, property.Id,
            OwnerNameLand: "Land owner",
            OwnerNameBuilding: "Building owner",
            SubDistrict: "S", Province: "P",
            IsRentedOut: true,
            LeaseAgreement: new LeaseAgreementData(LesseeName: "Lessee"),
            RentalInfo: ThreeYearRental(),
            Titles: [new LandTitleItemData(keep.Id, "111", "DEED", Rai: 1m, Ngan: 0m, SquareWa: 0m)],
            Surfaces: [new SurfaceItemData(null, 1, 1, "F")]), Ct);

        Assert.Equal("Land owner", land.OwnerName);
        Assert.Equal("P", land.Address!.Province);
        Assert.Equal("Building owner", property.BuildingDetail!.OwnerName);
        Assert.Equal(1m, keep.Area!.Rai);
        Assert.Single(property.BuildingDetail.Surfaces);
        Assert.Equal("Lessee", property.LeaseAgreementDetail!.LesseeName);
        Assert.Equal(3, property.RentalInfo!.ScheduleEntries.Count);
        await RecomputeReceived(appraisal.Id, 1);
    }

    [Fact]
    public async Task LandAndBuilding_not_rented_out_clears_rental()
    {
        var (appraisal, property) = Seed(a => a.AddLandAndBuildingProperty());
        property.SetLeaseAgreementDetail(LeaseAgreementDetail.Create(property.Id));
        property.SetRentalInfo(RentalInfo.Create(property.Id));

        await LandAndBuildingHandler().Handle(
            new UpdateLandAndBuildingPropertyCommand(appraisal.Id, property.Id), Ct);

        Assert.Null(property.LeaseAgreementDetail);
        Assert.Null(property.RentalInfo);
    }

    // ───────────────────────────── Condo ─────────────────────────────

    private UpdateCondoPropertyCommandHandler CondoHandler() => new(_repository, _mediator, _valuationSummary);

    [Fact]
    public async Task Condo_derives_the_insurance_price_from_the_rate_and_syncs_areas_then_recomputes()
    {
        var (appraisal, property) = Seed(a => a.AddCondoProperty());
        var condo = property.CondoDetail!;
        var gone = CondoAppraisalAreaDetail.Create(1, "old", 1m); gone.Id = NewId();
        condo.AddCondoAreaDetail(gone);
        GivenCondoRate("C1", 100m);

        await CondoHandler().Handle(new UpdateCondoPropertyCommand(
            appraisal.Id, property.Id,
            OwnerName: "Owner",
            UsableArea: 50m,
            FireInsuranceCode: "C1",
            AreaDetails: [new CondoAppraisalAreaDetailDto(null, 1, "living", 30m)]), Ct);

        Assert.Equal("Owner", condo.OwnerName);
        Assert.Equal(5000m, condo.BuildingInsurancePrice);
        Assert.Equal("C1", condo.FireInsuranceCode);
        var area = Assert.Single(condo.AreaDetails);
        Assert.Equal("living", area.AreaDescription);
        await RecomputeReceived(appraisal.Id, 1);
    }

    [Fact]
    public async Task Condo_without_a_fire_insurance_code_stores_a_null_insurance_price()
    {
        var (appraisal, property) = Seed(a => a.AddCondoProperty());

        await CondoHandler().Handle(new UpdateCondoPropertyCommand(
            appraisal.Id, property.Id, UsableArea: 50m), Ct);

        Assert.Null(property.CondoDetail!.BuildingInsurancePrice);
    }

    [Fact]
    public async Task Condo_clears_the_construction_inspection_when_none_is_sent()
    {
        var (appraisal, property) = Seed(a => a.AddCondoProperty());
        property.SetConstructionInspection(ConstructionInspection.CreateSummary(property.Id, 100m, null, null, null, null, null, null));

        await CondoHandler().Handle(new UpdateCondoPropertyCommand(appraisal.Id, property.Id), Ct);

        Assert.Null(property.ConstructionInspection);
    }

    // ───────────────────────────── Machinery / Vehicle / Vessel ─────────────────────────────

    [Fact]
    public async Task Machinery_overwrites_the_detail()
    {
        var (appraisal, property) = Seed(a => a.AddMachineryProperty());
        property.MachineryDetail!.Update(ownerName: "Old", brand: "Old brand");

        await new UpdateMachineryPropertyCommandHandler(_repository).Handle(
            new UpdateMachineryPropertyCommand(appraisal.Id, property.Id, MachineName: "Press", OwnerName: "New"), Ct);

        Assert.Equal("Press", property.MachineryDetail!.MachineName);
        Assert.Equal("New", property.MachineryDetail.OwnerName);
        // Machinery.Update guards most optional fields with "if (x is not null)": null keeps the stored value.
        Assert.Equal("Old brand", property.MachineryDetail.Brand);
    }

    [Fact]
    public async Task Vehicle_overwrites_the_detail()
    {
        var (appraisal, property) = Seed(a => a.AddVehicleProperty());

        await new UpdateVehiclePropertyCommandHandler(_repository).Handle(
            new UpdateVehiclePropertyCommand(appraisal.Id, property.Id, VehicleName: "Truck", OwnerName: "New"), Ct);

        Assert.Equal("Truck", property.VehicleDetail!.VehicleName);
        Assert.Equal("New", property.VehicleDetail.OwnerName);
    }

    [Fact]
    public async Task Vessel_overwrites_the_detail()
    {
        var (appraisal, property) = Seed(a => a.AddVesselProperty());

        await new UpdateVesselPropertyCommandHandler(_repository).Handle(
            new UpdateVesselPropertyCommand(appraisal.Id, property.Id, VesselName: "Boat", OwnerName: "New"), Ct);

        Assert.Equal("Boat", property.VesselDetail!.VesselName);
        Assert.Equal("New", property.VesselDetail.OwnerName);
    }

    [Fact]
    public async Task Vehicle_handler_rejects_a_property_of_another_type()
    {
        var (appraisal, property) = Seed(a => a.AddVesselProperty());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateVehiclePropertyCommandHandler(_repository).Handle(
                new UpdateVehiclePropertyCommand(appraisal.Id, property.Id), Ct));
    }

    // ───────────────────────────── Lease agreement variants ─────────────────────────────

    [Fact]
    public async Task LeaseLand_writes_land_and_lease_and_rental_only_when_they_are_sent()
    {
        var (appraisal, property) = Seed(a => a.AddLeaseAgreementLandProperty());
        property.LeaseAgreementDetail!.Update(
            "Old lessee", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        var handler = new UpdateLeaseAgreementLandPropertyCommandHandler(_repository);

        // Nothing sent for lease/rental: they are untouched.
        await handler.Handle(new UpdateLeaseAgreementLandPropertyCommand(
            appraisal.Id, property.Id, OwnerNameLand: "Owner"), Ct);
        Assert.Equal("Owner", property.LandDetail!.OwnerName);
        Assert.Equal("Old lessee", property.LeaseAgreementDetail!.LesseeName);

        await handler.Handle(new UpdateLeaseAgreementLandPropertyCommand(
            appraisal.Id, property.Id,
            Titles: [new LandTitleItemData(null, "111", "DEED")],
            LeaseAgreement: new LeaseAgreementData(LesseeName: "New lessee"),
            RentalInfo: ThreeYearRental()), Ct);

        Assert.Equal("New lessee", property.LeaseAgreementDetail.LesseeName);
        Assert.Equal(3, property.RentalInfo!.ScheduleEntries.Count);
        Assert.Single(property.LandDetail.Titles);
    }

    [Fact]
    public async Task LeaseBuilding_writes_the_building_and_recomputes()
    {
        var (appraisal, property) = Seed(a => a.AddLeaseAgreementBuildingProperty());

        await new UpdateLeaseAgreementBuildingPropertyCommandHandler(_repository, _valuationSummary).Handle(
            new UpdateLeaseAgreementBuildingPropertyCommand(
                appraisal.Id, property.Id,
                OwnerNameBuilding: "Owner",
                Surfaces: [new SurfaceItemData(null, 1, 2, "F")],
                LeaseAgreement: new LeaseAgreementData(LesseeName: "Lessee"),
                RentalInfo: ThreeYearRental()), Ct);

        Assert.Equal("Owner", property.BuildingDetail!.OwnerName);
        Assert.Single(property.BuildingDetail.Surfaces);
        Assert.Equal("Lessee", property.LeaseAgreementDetail!.LesseeName);
        Assert.Equal(3, property.RentalInfo!.ScheduleEntries.Count);
        await RecomputeReceived(appraisal.Id, 1);
    }

    [Fact]
    public async Task LeaseCondo_derives_insurance_writes_the_condo_and_recomputes()
    {
        var (appraisal, property) = Seed(a => a.AddLeaseAgreementCondoProperty());
        GivenCondoRate("C1", 100m);

        await new UpdateLeaseAgreementCondoPropertyCommandHandler(_repository, _mediator, _valuationSummary).Handle(
            new UpdateLeaseAgreementCondoPropertyCommand(
                appraisal.Id, property.Id,
                OwnerName: "Owner", UsableArea: 50m, FireInsuranceCode: "C1",
                LeaseAgreement: new LeaseAgreementData(LesseeName: "Lessee")), Ct);

        Assert.Equal("Owner", property.CondoDetail!.OwnerName);
        Assert.Equal(5000m, property.CondoDetail.BuildingInsurancePrice);
        Assert.Equal("Lessee", property.LeaseAgreementDetail!.LesseeName);
        await RecomputeReceived(appraisal.Id, 1);
    }

    [Fact]
    public async Task LeaseLandAndBuilding_writes_both_details_titles_and_recomputes()
    {
        var (appraisal, property) = Seed(a => a.AddLeaseAgreementLandAndBuildingProperty());

        await new UpdateLeaseAgreementLandAndBuildingPropertyCommandHandler(_repository, _valuationSummary).Handle(
            new UpdateLeaseAgreementLandAndBuildingPropertyCommand(
                appraisal.Id, property.Id,
                OwnerNameLand: "Land owner", OwnerNameBuilding: "Building owner",
                Titles: [new LandTitleItemData(null, "111", "DEED", Rai: 1m)],
                LeaseAgreement: new LeaseAgreementData(LesseeName: "Lessee"),
                RentalInfo: ThreeYearRental()), Ct);

        Assert.Equal("Land owner", property.LandDetail!.OwnerName);
        Assert.Equal("Building owner", property.BuildingDetail!.OwnerName);
        Assert.Single(property.LandDetail.Titles);
        Assert.Equal("Lessee", property.LeaseAgreementDetail!.LesseeName);
        Assert.Equal(3, property.RentalInfo!.ScheduleEntries.Count);
        await RecomputeReceived(appraisal.Id, 1);
    }
}
