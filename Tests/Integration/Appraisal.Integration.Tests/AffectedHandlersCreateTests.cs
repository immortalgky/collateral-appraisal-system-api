using Appraisal.Application.Features.Appraisals.CreateBuildingProperty;
using Appraisal.Application.Features.Appraisals.CreateCondoPMAProperty;
using Appraisal.Application.Features.Appraisals.CreateCondoProperty;
using Appraisal.Application.Features.Appraisals.CreateLandAndBuildingProperty;
using Appraisal.Application.Features.Appraisals.CreateLandPMAProperty;
using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.CreateLeaseAgreementBuildingProperty;
using Appraisal.Application.Features.Appraisals.CreateLeaseAgreementCondoProperty;
using Appraisal.Application.Features.Appraisals.CreateLeaseAgreementLandAndBuildingProperty;
using Appraisal.Application.Features.Appraisals.CreateLeaseAgreementLandProperty;
using Appraisal.Application.Features.Appraisals.CreateMachineryProperty;
using Appraisal.Application.Features.Appraisals.CreateVehicleProperty;
using Appraisal.Application.Features.Appraisals.CreateVesselProperty;
using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;
using Appraisal.Contracts.Appraisals.Dto;
using Appraisal.Domain.Appraisals;
using Integration.Fixtures;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Every Create*Property command, through the real pipeline: the property row, its detail, its child
/// collections and its group membership are persisted, and (for the families that recompute) the
/// appraisal-level InsuranceValue is the expected non-zero total. The recompute now sums the properties
/// the handler loaded instead of reloading them, so a regression would show up here as 0.
/// </summary>
[Collection("Integration")]
public class AffectedHandlersCreateTests(IntegrationTestFixture fixture) : AffectedHandlersBase(fixture)
{
    private static DepreciationItemData Dep(decimal price, bool isBuilding = true, bool withPeriod = false)
        => new(null, "Gross", PriceAfterDepreciation: price, IsBuilding: isBuilding,
            DepreciationPeriods: withPeriod ? [new DepreciationPeriodItemData(1, 5, 2m, 10m, 1_000m)] : null);

    private static readonly List<SurfaceItemData> Surfaces = [new(null, 1, 2, "F")];
    private static readonly LeaseAgreementData Lease = new(LesseeName: "Lessee QA", ContractNo: "C-1");
    private static readonly RentalInfoData Rental = new(NumberOfYears: 3, ContractRentalFeePerYear: 1_000m);

    private async Task<(Guid AppraisalId, Guid GroupId)> SeedGroupAsync()
    {
        var (aid, groups, _) = await SeedGroupedAsync(_ => []);
        return (aid, groups[0]);
    }

    private async Task AssertBuildingCreatedAsync(Guid aid, Guid gid, Guid propertyId)
    {
        var loaded = await LoadAsync(aid);
        var building = loaded.GetProperty(propertyId)!.BuildingDetail!;
        Assert.Equal("Building owner", building.OwnerName);
        Assert.Equal(2, building.DepreciationDetails.Count);
        Assert.Single(building.DepreciationDetails.Single(d => d.IsBuilding).DepreciationPeriods);
        Assert.Equal(1, building.Surfaces.Count);
        AssertInGroup(loaded, gid, propertyId, 1);
        // Only the IsBuilding row counts, rounded to the nearest 1,000.
        Assert.Equal(RoundTo1000(120_400m), await InsuranceAsync(aid));
    }

    [Fact]
    public async Task CreateLandProperty_persists_land_titles_deductions_and_group_membership()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateLandPropertyCommand(aid, gid, OwnerNameLand: "Land owner",
            Titles: [NewTitle("T1", 1m), NewTitle("T2", 2m)],
            LandAreaDeductions: [new LandAreaDeductionData(null, "99", "canal", 4m)]));

        var loaded = await LoadAsync(aid);
        var land = loaded.GetProperty(result.PropertyId)!.LandDetail!;
        Assert.Equal("Land owner", land.OwnerName);
        Assert.Equal(new[] { "T1", "T2" }, land.Titles.Select(t => t.TitleNumber).Order());
        Assert.Equal("canal", Assert.Single(land.Deductions).ReasonOther);
        AssertInGroup(loaded, gid, result.PropertyId, 1);
    }

    [Fact]
    public async Task CreateBuildingProperty_persists_building_depreciation_surfaces_group_and_insurance()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateBuildingPropertyCommand(aid, gid, OwnerNameBuilding: "Building owner",
            DepreciationDetails: [Dep(120_400m, withPeriod: true), Dep(999_999m, isBuilding: false)],
            Surfaces: Surfaces));
        await AssertBuildingCreatedAsync(aid, gid, result.PropertyId);
    }

    [Fact]
    public async Task CreateLandAndBuildingProperty_persists_both_details_children_group_and_insurance()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateLandAndBuildingPropertyCommand(aid, gid,
            OwnerNameLand: "Land owner", OwnerNameBuilding: "Building owner",
            Titles: [NewTitle("LB1")],
            DepreciationDetails: [Dep(120_400m, withPeriod: true), Dep(999_999m, isBuilding: false)],
            Surfaces: Surfaces));

        var land = (await PropAsync(aid, result.PropertyId)).LandDetail!;
        Assert.Equal("Land owner", land.OwnerName);
        Assert.Equal("LB1", Assert.Single(land.Titles).TitleNumber);
        await AssertBuildingCreatedAsync(aid, gid, result.PropertyId);
    }

    [Fact]
    public async Task CreateCondoProperty_persists_condo_area_details_group_and_insurance()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateCondoPropertyCommand(aid, gid, OwnerName: "Condo owner",
            CondoName: "QA Tower", BuildingInsurancePriceOverride: 250_000m,
            AreaDetails: [new CondoAppraisalAreaDetailDto(null, 1, "living", 30m), new(null, 2, "bed", 12m)]));

        var loaded = await LoadAsync(aid);
        var condo = loaded.GetProperty(result.PropertyId)!.CondoDetail!;
        Assert.Equal("Condo owner", condo.OwnerName);
        Assert.Equal("QA Tower", condo.CondoName);
        Assert.Equal(2, condo.AreaDetails.Count);
        AssertInGroup(loaded, gid, result.PropertyId, 1);
        Assert.Equal(250_000m, await InsuranceAsync(aid));
    }

    [Fact]
    public async Task CreateLeaseAgreementLandProperty_persists_land_titles_lease_rental_and_group()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateLeaseAgreementLandPropertyCommand(aid, gid, OwnerNameLand: "Lease land owner",
            Titles: [NewTitle("LL1"), NewTitle("LL2")], LeaseAgreement: Lease, RentalInfo: Rental));

        var loaded = await LoadAsync(aid);
        var p = loaded.GetProperty(result.PropertyId)!;
        Assert.Equal("Lease land owner", p.LandDetail!.OwnerName);
        Assert.Equal(2, p.LandDetail.Titles.Count);
        Assert.Equal("Lessee QA", p.LeaseAgreementDetail!.LesseeName);
        Assert.Equal(3, p.RentalInfo!.NumberOfYears);
        AssertInGroup(loaded, gid, result.PropertyId, 1);
    }

    [Fact]
    public async Task CreateLeaseAgreementBuildingProperty_persists_building_children_lease_group_and_insurance()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateLeaseAgreementBuildingPropertyCommand(aid, gid,
            OwnerNameBuilding: "Building owner",
            DepreciationDetails: [Dep(120_400m, withPeriod: true), Dep(999_999m, isBuilding: false)],
            Surfaces: Surfaces, LeaseAgreement: Lease, RentalInfo: Rental));

        Assert.Equal("Lessee QA", (await PropAsync(aid, result.PropertyId)).LeaseAgreementDetail!.LesseeName);
        await AssertBuildingCreatedAsync(aid, gid, result.PropertyId);
    }

    [Fact]
    public async Task CreateLeaseAgreementLandAndBuildingProperty_persists_everything_and_insurance()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateLeaseAgreementLandAndBuildingPropertyCommand(aid, gid,
            OwnerNameLand: "Land owner", OwnerNameBuilding: "Building owner", Titles: [NewTitle("LLB1")],
            DepreciationDetails: [Dep(120_400m, withPeriod: true), Dep(999_999m, isBuilding: false)],
            Surfaces: Surfaces, LeaseAgreement: Lease, RentalInfo: Rental));

        var p = await PropAsync(aid, result.PropertyId);
        Assert.Equal("LLB1", Assert.Single(p.LandDetail!.Titles).TitleNumber);
        Assert.Equal("Lessee QA", p.LeaseAgreementDetail!.LesseeName);
        await AssertBuildingCreatedAsync(aid, gid, result.PropertyId);
    }

    [Fact]
    public async Task CreateLeaseAgreementCondoProperty_persists_condo_area_details_lease_group_and_insurance()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateLeaseAgreementCondoPropertyCommand(aid, gid, OwnerName: "Condo owner",
            BuildingInsurancePriceOverride: 250_000m, LeaseAgreement: Lease, RentalInfo: Rental,
            AreaDetails: [new CondoAppraisalAreaDetailDto(null, 1, "living", 30m)]));

        var loaded = await LoadAsync(aid);
        var p = loaded.GetProperty(result.PropertyId)!;
        Assert.Equal("Condo owner", p.CondoDetail!.OwnerName);
        Assert.Single(p.CondoDetail.AreaDetails);
        Assert.Equal("Lessee QA", p.LeaseAgreementDetail!.LesseeName);
        AssertInGroup(loaded, gid, result.PropertyId, 1);
        Assert.Equal(250_000m, await InsuranceAsync(aid));
    }

    [Fact]
    public async Task CreateLandPMAProperty_persists_prices_titles_and_group()
    {
        var (aid, groups, _) = await SeedGroupedAsync(_ => [], pma: true);
        var result = await SendAsync(new CreateLandPMAPropertyCommand(aid, groups[0], 1_000_000m, 700_000m, 300_000m,
            Titles: [NewTitle("PMA1"), NewTitle("PMA2")], Province: "Bangkok"));

        var loaded = await LoadAsync(aid);
        var p = loaded.GetProperty(result.PropertyId)!;
        Assert.Equal(1_000_000m, p.SellingPrice);
        Assert.Equal(700_000m, p.ForcedSalePrice);
        Assert.Equal(300_000m, p.BuildingInsurancePrice);
        Assert.Equal(2, p.LandDetail!.Titles.Count);
        Assert.Equal("Bangkok", p.LandDetail.Address!.Province);
        AssertInGroup(loaded, groups[0], result.PropertyId, 1);
    }

    [Fact]
    public async Task CreateCondoPMAProperty_persists_prices_condo_fields_and_group()
    {
        var (aid, groups, _) = await SeedGroupedAsync(_ => [], pma: true);
        var result = await SendAsync(new CreateCondoPMAPropertyCommand(aid, groups[0], 2_000_000m, 1_400_000m, 500_000m,
            CondoName: "PMA Tower", RoomNumber: "12A", Province: "Bangkok"));

        var loaded = await LoadAsync(aid);
        var p = loaded.GetProperty(result.PropertyId)!;
        Assert.Equal(2_000_000m, p.SellingPrice);
        Assert.Equal(500_000m, p.BuildingInsurancePrice);
        Assert.Equal("PMA Tower", p.CondoDetail!.CondoName);
        Assert.Equal("12A", p.CondoDetail.RoomNumber);
        AssertInGroup(loaded, groups[0], result.PropertyId, 1);
    }

    [Fact]
    public async Task CreateMachineryProperty_persists_detail_and_group()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateMachineryPropertyCommand(aid, gid, "Machine owner", MachineName: "Press 9"));

        var loaded = await LoadAsync(aid);
        var m = loaded.GetProperty(result.PropertyId)!.MachineryDetail!;
        Assert.Equal("Machine owner", m.OwnerName);
        Assert.Equal("Press 9", m.MachineName);
        AssertInGroup(loaded, gid, result.PropertyId, 1);
    }

    [Fact]
    public async Task CreateVehicleProperty_persists_detail_and_group()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateVehiclePropertyCommand(aid, gid, "Vehicle owner", VehicleName: "Truck 7"));

        var loaded = await LoadAsync(aid);
        var v = loaded.GetProperty(result.PropertyId)!.VehicleDetail!;
        Assert.Equal("Truck 7", v.VehicleName);
        AssertInGroup(loaded, gid, result.PropertyId, 1);
    }

    [Fact]
    public async Task CreateVehicleProperty_persists_the_command_OwnerName()
    {
        // The required owner typed on the create form must reach VehicleDetail.
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateVehiclePropertyCommand(aid, gid, "Vehicle owner"));
        Assert.Equal("Vehicle owner", (await PropAsync(aid, result.PropertyId)).VehicleDetail!.OwnerName);
    }

    [Fact]
    public async Task CreateVesselProperty_persists_detail_and_group()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateVesselPropertyCommand(aid, gid, "Vessel owner", VesselName: "Ship 3"));

        var loaded = await LoadAsync(aid);
        var v = loaded.GetProperty(result.PropertyId)!.VesselDetail!;
        Assert.Equal("Ship 3", v.VesselName);
        AssertInGroup(loaded, gid, result.PropertyId, 1);
    }

    [Fact]
    public async Task CreateVesselProperty_persists_the_command_OwnerName()
    {
        var (aid, gid) = await SeedGroupAsync();
        var result = await SendAsync(new CreateVesselPropertyCommand(aid, gid, "Vessel owner"));
        Assert.Equal("Vessel owner", (await PropAsync(aid, result.PropertyId)).VesselDetail!.OwnerName);
    }
}
