using System.Text.Json;
using Appraisal.Application.Features.Appraisals.CopyPropertyToGroup;
using Appraisal.Application.Features.Appraisals.CorrectPropertyData;
using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.DeleteProperty;
using Appraisal.Application.Features.Appraisals.SaveCondoPMAPropertyDraft;
using Appraisal.Application.Features.Appraisals.SaveLandPMAPropertyDraft;
using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Application.Features.Appraisals.UpdateBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateCondoPMAProperty;
using Appraisal.Application.Features.Appraisals.UpdateCondoProperty;
using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateLandPMAProperty;
using Appraisal.Application.Features.Appraisals.UpdateLandProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreement;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementCondoProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementLandAndBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementLandProperty;
using Appraisal.Application.Features.Appraisals.UpdateMachineryProperty;
using Appraisal.Application.Features.Appraisals.UpdateRentalInfo;
using Appraisal.Application.Features.Appraisals.UpdateVehicleProperty;
using Appraisal.Application.Features.Appraisals.UpdateVesselProperty;
using Appraisal.Contracts.Appraisals.Dto;
using Appraisal.Domain.Appraisals;
using Integration.Fixtures;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Every Update*/Save*/Delete/Copy/Correct property handler: the edit is committed by change tracking plus
/// the pipeline's SaveChanges alone (no DbSet.Update). Child collections are exercised with add + update +
/// remove in ONE save, and handlers that recompute assert the appraisal-level InsuranceValue
/// (edited property + an untouched second building worth 300,000).
/// </summary>
[Collection("Integration")]
public class AffectedHandlersUpdateTests(IntegrationTestFixture fixture) : AffectedHandlersBase(fixture)
{
    private const decimal OtherInsurance = 300_000m;

    // ── seeds ───────────────────────────────────────────────────────────────────────────────

    private static void SeedBuildingRows(BuildingAppraisalDetail b)
    {
        b.Update(ownerName: "old");
        b.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 100_000m);   // kept + edited
        b.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 50_000m);    // removed
        b.AddSurface(1, 1, "F");                                                                // replaced
    }

    private static AppraisalProperty AddOther(AppraisalAggregate a)
    {
        var other = a.AddBuildingProperty();
        other.BuildingDetail!.Update(ownerName: "Other owner");
        other.BuildingDetail.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: OtherInsurance);
        return other;
    }

    /// <summary>Appraisal holding the property built by <paramref name="addEdited"/> plus an untouched building.</summary>
    private async Task<(Guid AppraisalId, Guid EditedId, Guid OtherId)> SeedWithOtherAsync(
        Func<AppraisalAggregate, AppraisalProperty> addEdited)
    {
        var (aid, _, ids) = await SeedGroupedAsync(a => [addEdited(a), AddOther(a)], groups: 0);
        return (aid, ids[0], ids[1]);
    }

    private static void SeedCondoRows(CondoAppraisalDetail c)
    {
        c.Update(ownerName: "old", address: Address.Create("Sub", "Dist", "Prov"), landOffice: "LO");
        c.AddCondoAreaDetail(CondoAppraisalAreaDetail.Create(1, "keep", 10m));
        c.AddCondoAreaDetail(CondoAppraisalAreaDetail.Create(2, "drop", 20m));
    }

    private static DepreciationItemData EditedDep(Guid keepId) => new(keepId, "Gross", PriceAfterDepreciation: 120_400m,
        DepreciationPeriods: [new DepreciationPeriodItemData(1, 5, 2m, 10m, 1_000m)]);

    private static DepreciationItemData AddedDep => new(null, "Gross", IsBuilding: false, PriceAfterDepreciation: 999_999m);

    private static readonly List<SurfaceItemData> NewSurface = [new(null, 2, 3, "T")];

    /// <summary>Building rows after <see cref="SeedBuildingRows"/> + the edit: updated, added, removed, replaced.</summary>
    private static void AssertBuildingEdited(BuildingAppraisalDetail b, Guid keptId, Guid droppedId, string owner)
    {
        Assert.Equal(owner, b.OwnerName);
        Assert.Equal(2, b.DepreciationDetails.Count);
        var kept = Assert.Single(b.DepreciationDetails, d => d.Id == keptId);
        Assert.Equal(120_400m, kept.PriceAfterDepreciation);
        Assert.Single(kept.DepreciationPeriods);
        Assert.DoesNotContain(b.DepreciationDetails, d => d.Id == droppedId);
        Assert.Single(b.DepreciationDetails, d => !d.IsBuilding && d.PriceAfterDepreciation == 999_999m);
        Assert.Equal(2, Assert.Single(b.Surfaces).FromFloorNumber);
    }

    private async Task<(Guid Kept, Guid Dropped)> BuildingRowIdsAsync(Guid aid, Guid pid)
    {
        var rows = (await PropAsync(aid, pid)).BuildingDetail!.DepreciationDetails;
        return (rows.Single(d => d.PriceAfterDepreciation == 100_000m).Id, rows.Single(d => d.PriceAfterDepreciation == 50_000m).Id);
    }

    private async Task AssertUntouchedOtherAsync(Guid aid, Guid otherId, DateTime? stamp)
        => Assert.Equal(stamp, (await PropAsync(aid, otherId)).UpdatedAt);

    // ── land-family updates ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateLandProperty_adds_updates_and_removes_titles_and_deductions()
    {
        var (aid, _, ids) = await SeedGroupedAsync(a =>
        {
            var p = a.AddLandProperty();
            p.LandDetail!.Update(ownerName: "old");
            SeedTitle(p.LandDetail, "T1", 1m);
            SeedTitle(p.LandDetail, "T2", 2m);
            return [p];
        }, groups: 0);
        var seeded = (await PropAsync(aid, ids[0])).LandDetail!.Titles;
        var (t1, t2) = (seeded.Single(t => t.TitleNumber == "T1").Id, seeded.Single(t => t.TitleNumber == "T2").Id);

        await SendAsync(new UpdateLandPropertyCommand(aid, ids[0], OwnerNameLand: "new",
            Titles: [NewTitle("T1x", 5m, t1), NewTitle("T3")],
            LandAreaDeductions: [new LandAreaDeductionData(null, "99", "canal", 4m)]));

        var land = (await PropAsync(aid, ids[0])).LandDetail!;
        Assert.Equal("new", land.OwnerName);
        Assert.Equal(new[] { "T1x", "T3" }, land.Titles.Select(t => t.TitleNumber).Order());
        Assert.Equal(5m, land.Titles.Single(t => t.Id == t1).Area!.Rai);
        Assert.DoesNotContain(land.Titles, t => t.Id == t2);
        Assert.Equal("canal", Assert.Single(land.Deductions).ReasonOther);

        // A second save drops the deduction again.
        await SendAsync(new UpdateLandPropertyCommand(aid, ids[0], Titles: [NewTitle("T1x", 5m, t1), NewTitle("T3")],
            LandAreaDeductions: []));
        Assert.Empty((await PropAsync(aid, ids[0])).LandDetail!.Deductions);
    }

    [Fact]
    public async Task UpdateBuildingProperty_adds_updates_removes_children_and_recomputes_insurance()
    {
        var (aid, edited, other) = await SeedWithOtherAsync(a => { var p = a.AddBuildingProperty(); SeedBuildingRows(p.BuildingDetail!); return p; });
        var (kept, dropped) = await BuildingRowIdsAsync(aid, edited);
        var otherStamp = (await PropAsync(aid, other)).UpdatedAt;

        await SendAsync(new UpdateBuildingPropertyCommand(aid, edited, OwnerNameBuilding: "new",
            DepreciationDetails: [EditedDep(kept), AddedDep], Surfaces: NewSurface));

        AssertBuildingEdited((await PropAsync(aid, edited)).BuildingDetail!, kept, dropped, "new");
        Assert.Equal(RoundTo1000(120_400m) + OtherInsurance, await InsuranceAsync(aid));
        await AssertUntouchedOtherAsync(aid, other, otherStamp);
    }

    [Fact]
    public async Task UpdateLandAndBuildingProperty_adds_updates_removes_children_and_recomputes_insurance()
    {
        var (aid, edited, _) = await SeedWithOtherAsync(a =>
        {
            var p = a.AddLandAndBuildingProperty();
            p.LandDetail!.Update(ownerName: "old");
            SeedTitle(p.LandDetail, "T1", 1m);
            SeedBuildingRows(p.BuildingDetail!);
            return p;
        });
        var (kept, dropped) = await BuildingRowIdsAsync(aid, edited);
        var t1 = (await PropAsync(aid, edited)).LandDetail!.Titles.Single().Id;

        await SendAsync(new UpdateLandAndBuildingPropertyCommand(aid, edited, OwnerNameLand: "new land",
            OwnerNameBuilding: "new",
            Titles: [NewTitle("T1x", 3m, t1), NewTitle("T2")],
            DepreciationDetails: [EditedDep(kept), AddedDep], Surfaces: NewSurface));

        var p = await PropAsync(aid, edited);
        Assert.Equal("new land", p.LandDetail!.OwnerName);
        Assert.Equal(new[] { "T1x", "T2" }, p.LandDetail.Titles.Select(t => t.TitleNumber).Order());
        AssertBuildingEdited(p.BuildingDetail!, kept, dropped, "new");
        Assert.Equal(RoundTo1000(120_400m) + OtherInsurance, await InsuranceAsync(aid));
    }

    [Fact]
    public async Task UpdateCondoProperty_adds_updates_removes_area_details_and_recomputes_insurance()
    {
        var (aid, edited, _) = await SeedWithOtherAsync(a => { var p = a.AddCondoProperty(); SeedCondoRows(p.CondoDetail!); return p; });
        var seeded = (await PropAsync(aid, edited)).CondoDetail!.AreaDetails;
        var (keep, drop) = (seeded.Single(x => x.AreaDescription == "keep").Id, seeded.Single(x => x.AreaDescription == "drop").Id);

        await SendAsync(new UpdateCondoPropertyCommand(aid, edited, OwnerName: "new", BuildingInsurancePriceOverride: 250_000m,
            AreaDetails: [new CondoAppraisalAreaDetailDto(keep, 1, "kept-edited", 11m), new(null, 3, "added", 33m)]));

        var condo = (await PropAsync(aid, edited)).CondoDetail!;
        Assert.Equal("new", condo.OwnerName);
        Assert.Equal(2, condo.AreaDetails.Count);
        Assert.Equal("kept-edited", condo.AreaDetails.Single(x => x.Id == keep).AreaDescription);
        Assert.DoesNotContain(condo.AreaDetails, x => x.Id == drop);
        Assert.Contains(condo.AreaDetails, x => x.AreaDescription == "added");
        Assert.Equal(250_000m + OtherInsurance, await InsuranceAsync(aid));
    }

    // ── lease-agreement family ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateLeaseAgreementLandProperty_adds_updates_and_removes_titles()
    {
        var (aid, _, ids) = await SeedGroupedAsync(a =>
        {
            var p = a.AddLeaseAgreementLandProperty();
            p.LandDetail!.Update(ownerName: "old");
            SeedTitle(p.LandDetail, "T1", 1m);
            SeedTitle(p.LandDetail, "T2", 2m);
            return [p];
        }, groups: 0);
        var t1 = (await PropAsync(aid, ids[0])).LandDetail!.Titles.Single(t => t.TitleNumber == "T1").Id;

        await SendAsync(new UpdateLeaseAgreementLandPropertyCommand(aid, ids[0], OwnerNameLand: "new",
            Titles: [NewTitle("T1x", 5m, t1), NewTitle("T3")]));

        var land = (await PropAsync(aid, ids[0])).LandDetail!;
        Assert.Equal("new", land.OwnerName);
        Assert.Equal(new[] { "T1x", "T3" }, land.Titles.Select(t => t.TitleNumber).Order());
        Assert.Equal(t1, land.Titles.Single(t => t.TitleNumber == "T1x").Id);
    }

    [Fact]
    public async Task UpdateLeaseAgreementBuildingProperty_adds_updates_removes_children_and_recomputes_insurance()
    {
        var (aid, edited, _) = await SeedWithOtherAsync(a => { var p = a.AddLeaseAgreementBuildingProperty(); SeedBuildingRows(p.BuildingDetail!); return p; });
        var (kept, dropped) = await BuildingRowIdsAsync(aid, edited);

        await SendAsync(new UpdateLeaseAgreementBuildingPropertyCommand(aid, edited, OwnerNameBuilding: "new",
            DepreciationDetails: [EditedDep(kept), AddedDep], Surfaces: NewSurface));

        AssertBuildingEdited((await PropAsync(aid, edited)).BuildingDetail!, kept, dropped, "new");
        Assert.Equal(RoundTo1000(120_400m) + OtherInsurance, await InsuranceAsync(aid));
    }

    [Fact]
    public async Task UpdateLeaseAgreementLandAndBuildingProperty_adds_updates_removes_children_and_recomputes_insurance()
    {
        var (aid, edited, _) = await SeedWithOtherAsync(a =>
        {
            var p = a.AddLeaseAgreementLandAndBuildingProperty();
            p.LandDetail!.Update(ownerName: "old");
            SeedTitle(p.LandDetail, "T1", 1m);
            SeedBuildingRows(p.BuildingDetail!);
            return p;
        });
        var (kept, dropped) = await BuildingRowIdsAsync(aid, edited);
        var t1 = (await PropAsync(aid, edited)).LandDetail!.Titles.Single().Id;

        await SendAsync(new UpdateLeaseAgreementLandAndBuildingPropertyCommand(aid, edited, OwnerNameLand: "new land",
            OwnerNameBuilding: "new", Titles: [NewTitle("T1x", 3m, t1), NewTitle("T2")],
            DepreciationDetails: [EditedDep(kept), AddedDep], Surfaces: NewSurface));

        var p = await PropAsync(aid, edited);
        Assert.Equal(new[] { "T1x", "T2" }, p.LandDetail!.Titles.Select(t => t.TitleNumber).Order());
        AssertBuildingEdited(p.BuildingDetail!, kept, dropped, "new");
        Assert.Equal(RoundTo1000(120_400m) + OtherInsurance, await InsuranceAsync(aid));
    }

    [Fact]
    public async Task UpdateLeaseAgreementCondoProperty_adds_updates_removes_area_details_and_recomputes_insurance()
    {
        var (aid, edited, _) = await SeedWithOtherAsync(a => { var p = a.AddLeaseAgreementCondoProperty(); SeedCondoRows(p.CondoDetail!); return p; });
        var seeded = (await PropAsync(aid, edited)).CondoDetail!.AreaDetails;
        var (keep, drop) = (seeded.Single(x => x.AreaDescription == "keep").Id, seeded.Single(x => x.AreaDescription == "drop").Id);

        await SendAsync(new UpdateLeaseAgreementCondoPropertyCommand(aid, edited, OwnerName: "new",
            BuildingInsurancePriceOverride: 250_000m,
            AreaDetails: [new CondoAppraisalAreaDetailDto(keep, 1, "kept-edited", 11m), new(null, 3, "added", 33m)]));

        var condo = (await PropAsync(aid, edited)).CondoDetail!;
        Assert.Equal("new", condo.OwnerName);
        Assert.Equal(2, condo.AreaDetails.Count);
        Assert.DoesNotContain(condo.AreaDetails, x => x.Id == drop);
        Assert.Contains(condo.AreaDetails, x => x.AreaDescription == "added");
        Assert.Equal(250_000m + OtherInsurance, await InsuranceAsync(aid));
    }

    [Fact]
    public async Task UpdateRentalInfo_adds_updates_and_removes_entries_across_saves()
    {
        var (aid, _, ids) = await SeedGroupedAsync(a => { var p = a.AddLeaseAgreementLandProperty(); p.LandDetail!.Update(ownerName: "o"); return [p]; }, groups: 0);
        var start = new DateTime(2026, 1, 1);

        await SendAsync(new UpdateRentalInfoCommand(aid, ids[0], NumberOfYears: 3, FirstYearStartDate: start,
            ContractRentalFeePerYear: 1_000m,
            UpFrontEntries: [new UpFrontEntryData(start, 100m), new UpFrontEntryData(start.AddYears(1), 200m)],
            GrowthPeriodEntries: [new GrowthPeriodEntryData(1, 3, 5m, 50m, 1_050m)]));

        var rental = (await PropAsync(aid, ids[0])).RentalInfo!;
        Assert.Equal(3, rental.NumberOfYears);
        Assert.Equal(2, rental.UpFrontEntries.Count);
        Assert.Single(rental.GrowthPeriodEntries);
        Assert.Equal(3, rental.ScheduleEntries.Count);

        // Second save: one up-front entry removed, growth entries cleared, years shortened.
        await SendAsync(new UpdateRentalInfoCommand(aid, ids[0], NumberOfYears: 2, FirstYearStartDate: start,
            ContractRentalFeePerYear: 1_000m, UpFrontEntries: [new UpFrontEntryData(start, 150m)],
            GrowthPeriodEntries: []));

        rental = (await PropAsync(aid, ids[0])).RentalInfo!;
        Assert.Equal(2, rental.NumberOfYears);
        Assert.Equal(150m, Assert.Single(rental.UpFrontEntries).UpFrontAmount);
        Assert.Empty(rental.GrowthPeriodEntries);
        Assert.Equal(2, rental.ScheduleEntries.Count);
    }

    [Fact]
    public async Task UpdateLeaseAgreement_persists_the_lease_fields()
    {
        var (aid, _, ids) = await SeedGroupedAsync(a => { var p = a.AddLeaseAgreementLandProperty(); p.LandDetail!.Update(ownerName: "o"); return [p]; }, groups: 0);

        await SendAsync(new UpdateLeaseAgreementCommand(aid, ids[0], LesseeName: "Lessee", ContractNo: "C-9", LeaseRentFee: 1_234m));

        var lease = (await PropAsync(aid, ids[0])).LeaseAgreementDetail!;
        Assert.Equal("Lessee", lease.LesseeName);
        Assert.Equal("C-9", lease.ContractNo);
        Assert.Equal(1_234m, lease.LeaseRentFee);
    }

    // ── PMA ────────────────────────────────────────────────────────────────────────────────

    private async Task<(Guid AppraisalId, Guid PropertyId, Guid T1, Guid T2)> SeedLandPmaAsync()
    {
        var (aid, _, ids) = await SeedGroupedAsync(a =>
        {
            var p = a.AddLandAndBuildingProperty(sellingPrice: 1m);
            p.LandDetail!.UpdatePmaFields(ownerName: "", address: Address.Create("S", "D", "Old"));
            SeedTitle(p.LandDetail, "T1", 1m);
            SeedTitle(p.LandDetail, "T2", 2m);
            return [p];
        }, groups: 0, pma: true);
        var titles = (await PropAsync(aid, ids[0])).LandDetail!.Titles;
        return (aid, ids[0], titles.Single(t => t.TitleNumber == "T1").Id, titles.Single(t => t.TitleNumber == "T2").Id);
    }

    private async Task AssertLandPmaAsync(Guid aid, Guid pid, Guid t1, Guid t2, decimal price)
    {
        var p = await PropAsync(aid, pid);
        Assert.Equal(price, p.SellingPrice);
        Assert.Equal(price - 1, p.ForcedSalePrice);
        Assert.Equal(price - 2, p.BuildingInsurancePrice);
        Assert.Equal("New", p.LandDetail!.Address!.Province);
        Assert.Equal(ExternalSyncStatuses.Pending, p.ExternalSyncStatus);
        // PMA sync keeps the stored title number of an existing row and updates its other fields.
        Assert.Equal(new[] { "T1", "T3" }, p.LandDetail.Titles.Select(t => t.TitleNumber).Order());
        Assert.Equal(9m, p.LandDetail.Titles.Single(t => t.Id == t1).Area!.Rai);
        Assert.DoesNotContain(p.LandDetail.Titles, t => t.Id == t2);
    }

    [Fact]
    public async Task UpdateLandPMAProperty_persists_prices_address_sync_status_and_title_changes()
    {
        var (aid, pid, t1, t2) = await SeedLandPmaAsync();
        await SendAsync(new UpdateLandPMAPropertyCommand(aid, pid, 500m, 499m, 498m,
            Titles: [NewTitle("T1", 9m, t1), NewTitle("T3")], SubDistrict: "S", District: "D", Province: "New"));
        await AssertLandPmaAsync(aid, pid, t1, t2, 500m);
    }

    [Fact]
    public async Task SaveLandPMAPropertyDraft_persists_prices_address_sync_status_and_title_changes()
    {
        var (aid, pid, t1, t2) = await SeedLandPmaAsync();
        await SendAsync(new SaveLandPMAPropertyDraftCommand(aid, pid, 600m, 599m, 598m,
            Titles: [NewTitle("T1", 9m, t1), NewTitle("T3")], SubDistrict: "S", District: "D", Province: "New"));
        await AssertLandPmaAsync(aid, pid, t1, t2, 600m);
    }

    private async Task<(Guid AppraisalId, Guid PropertyId)> SeedCondoPmaAsync()
    {
        var (aid, _, ids) = await SeedGroupedAsync(a =>
        {
            var p = a.AddCondoProperty(sellingPrice: 1m);
            p.CondoDetail!.UpdatePmaFields(condoName: "old", ownerName: "", address: Address.Create("S", "D", "Old"));
            return [p];
        }, groups: 0, pma: true);
        return (aid, ids[0]);
    }

    private async Task AssertCondoPmaAsync(Guid aid, Guid pid, decimal price)
    {
        var p = await PropAsync(aid, pid);
        Assert.Equal(price, p.SellingPrice);
        Assert.Equal(price - 1, p.ForcedSalePrice);
        Assert.Equal(price - 2, p.BuildingInsurancePrice);
        Assert.Equal("PMA Tower", p.CondoDetail!.CondoName);
        Assert.Equal("77", p.CondoDetail.RoomNumber);
        Assert.Equal("New", p.CondoDetail.Address!.Province);
        Assert.Equal(ExternalSyncStatuses.Pending, p.ExternalSyncStatus);
    }

    [Fact]
    public async Task UpdateCondoPMAProperty_persists_prices_condo_fields_and_sync_status()
    {
        var (aid, pid) = await SeedCondoPmaAsync();
        await SendAsync(new UpdateCondoPMAPropertyCommand(aid, pid, 700m, 699m, 698m, CondoName: "PMA Tower",
            RoomNumber: "77", SubDistrict: "S", District: "D", Province: "New"));
        await AssertCondoPmaAsync(aid, pid, 700m);
    }

    [Fact]
    public async Task SaveCondoPMAPropertyDraft_persists_prices_condo_fields_and_sync_status()
    {
        var (aid, pid) = await SeedCondoPmaAsync();
        await SendAsync(new SaveCondoPMAPropertyDraftCommand(aid, pid, 800m, 799m, 798m, CondoName: "PMA Tower",
            RoomNumber: "77", SubDistrict: "S", District: "D", Province: "New"));
        await AssertCondoPmaAsync(aid, pid, 800m);
    }

    // ── machinery / vehicle / vessel ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMachineryProperty_persists_the_edit()
    {
        var (aid, _, ids) = await SeedGroupedAsync(a => { var p = a.AddMachineryProperty(); p.MachineryDetail!.Update(ownerName: "old"); return [p]; }, groups: 0);
        await SendAsync(new UpdateMachineryPropertyCommand(aid, ids[0], OwnerName: "new", MachineName: "Press 2"));
        var m = (await PropAsync(aid, ids[0])).MachineryDetail!;
        Assert.Equal(("new", "Press 2"), (m.OwnerName, m.MachineName));
    }

    [Fact]
    public async Task UpdateVehicleProperty_persists_the_edit()
    {
        var (aid, _, ids) = await SeedGroupedAsync(a => { var p = a.AddVehicleProperty(); p.VehicleDetail!.Update(ownerName: "old"); return [p]; }, groups: 0);
        await SendAsync(new UpdateVehiclePropertyCommand(aid, ids[0], OwnerName: "new", VehicleName: "Truck 2"));
        var v = (await PropAsync(aid, ids[0])).VehicleDetail!;
        Assert.Equal(("new", "Truck 2"), (v.OwnerName, v.VehicleName));
    }

    [Fact]
    public async Task UpdateVesselProperty_persists_the_edit()
    {
        var (aid, _, ids) = await SeedGroupedAsync(a => { var p = a.AddVesselProperty(); p.VesselDetail!.Update(ownerName: "old"); return [p]; }, groups: 0);
        await SendAsync(new UpdateVesselPropertyCommand(aid, ids[0], OwnerName: "new", VesselName: "Ship 2"));
        var v = (await PropAsync(aid, ids[0])).VesselDetail!;
        Assert.Equal(("new", "Ship 2"), (v.OwnerName, v.VesselName));
    }

    // ── delete / copy / correct ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteProperty_removes_the_row_and_group_item_and_recomputes_insurance()
    {
        var (aid, groups, ids) = await SeedGroupedAsync(a =>
        {
            var doomed = a.AddBuildingProperty();
            doomed.BuildingDetail!.Update(ownerName: "doomed");
            doomed.BuildingDetail.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 100_000m);
            return [doomed, AddOther(a)];
        });

        var result = await SendAsync(new DeletePropertyCommand(aid, ids[0]));

        Assert.True(result.IsSuccess);
        var loaded = await LoadAsync(aid);
        Assert.Null(loaded.GetProperty(ids[0]));
        Assert.Equal(ids[1], Assert.Single(loaded.Properties).Id);
        Assert.Equal(1, Assert.Single(loaded.Properties).SequenceNumber);
        AssertInGroup(loaded, groups[0], ids[1], 1);
        Assert.DoesNotContain(loaded.Groups.Single().Items, i => i.AppraisalPropertyId == ids[0]);
        Assert.Equal(OtherInsurance, await InsuranceAsync(aid));
    }

    [Fact]
    public async Task CopyPropertyToGroup_creates_the_copy_with_its_children_in_the_target_group_and_recomputes_insurance()
    {
        var (aid, groups, ids) = await SeedGroupedAsync(a =>
        {
            var source = a.AddBuildingProperty();
            source.BuildingDetail!.Update(ownerName: "Source owner");
            source.BuildingDetail.AddDepreciationDetail("Gross", isBuilding: true, priceAfterDepreciation: 100_000m);
            source.BuildingDetail.AddSurface(1, 1, "F");
            return [source];
        }, groups: 2);

        var result = await SendAsync(new CopyPropertyToGroupCommand(aid, ids[0], groups[1]));

        var loaded = await LoadAsync(aid);
        Assert.Equal(2, loaded.Properties.Count);
        var copy = loaded.GetProperty(result.PropertyId)!.BuildingDetail!;
        Assert.Equal("Source owner", copy.OwnerName);
        Assert.Equal(100_000m, Assert.Single(copy.DepreciationDetails).PriceAfterDepreciation);
        Assert.Single(copy.Surfaces);
        AssertInGroup(loaded, groups[1], result.PropertyId, 1);
        AssertInGroup(loaded, groups[0], ids[0], 1);
        Assert.Equal(200_000m, await InsuranceAsync(aid));
    }

    private async Task<(Guid AppraisalId, Guid PropertyId)> SeedCompletedAsync(Func<AppraisalAggregate, AppraisalProperty> add)
    {
        var (aid, _, ids) = await SeedGroupedAsync(a => { var p = add(a); a.SyncStatusFromWorkflow(AppraisalStatus.Completed); return [p]; }, groups: 0);
        return (aid, ids[0]);
    }

    private Task<CorrectPropertyDataResult> CorrectAsync(Guid aid, Guid pid, string suffix, string json)
        => SendAsync(new CorrectPropertyDataCommand(aid, pid, suffix, "qa", JsonDocument.Parse(json).RootElement));

    [Fact]
    public async Task CorrectPropertyData_commits_a_condo_correction_with_area_details()
    {
        var (aid, pid) = await SeedCompletedAsync(a => { var p = a.AddCondoProperty(); SeedCondoRows(p.CondoDetail!); return p; });
        var result = await CorrectAsync(aid, pid, "condo-detail", """
            { "ownerName": "Corrected", "subDistrict": "Sub", "district": "Dist", "province": "Prov",
              "areaDetails": [ { "sequence": 1, "areaDescription": "only", "areaSize": 30 } ] }
            """);

        var condo = (await PropAsync(aid, pid)).CondoDetail!;
        Assert.Equal("Corrected", condo.OwnerName);
        Assert.Equal("only", Assert.Single(condo.AreaDetails).AreaDescription);
        Assert.Contains("Condo.OwnerName", result.ChangedFields);
    }

    [Fact]
    public async Task CorrectPropertyData_commits_a_land_and_building_correction_with_children()
    {
        var (aid, pid) = await SeedCompletedAsync(a =>
        {
            var p = a.AddLandAndBuildingProperty();
            p.LandDetail!.Update(ownerName: "old");
            SeedTitle(p.LandDetail, "T1", 1m);
            SeedBuildingRows(p.BuildingDetail!);
            return p;
        });
        var (kept, dropped) = await BuildingRowIdsAsync(aid, pid);
        var t1 = (await PropAsync(aid, pid)).LandDetail!.Titles.Single().Id;

        await CorrectAsync(aid, pid, "land-and-building-detail", $$"""
            { "ownerNameLand": "Corrected land", "ownerNameBuilding": "Corrected",
              "titles": [ { "id": "{{t1}}", "titleNumber": "T1x", "titleType": "DEED", "rai": 3, "ngan": 0, "squareWa": 0 },
                          { "titleNumber": "T2", "titleType": "DEED", "rai": 1, "ngan": 0, "squareWa": 0 } ],
              "depreciationDetails": [ { "id": "{{kept}}", "depreciationMethod": "Gross", "priceAfterDepreciation": 120400,
                  "depreciationPeriods": [ { "atYear": 1, "toYear": 5, "depreciationPerYear": 2, "totalDepreciationPct": 10, "priceDepreciation": 1000 } ] },
                  { "depreciationMethod": "Gross", "isBuilding": false, "priceAfterDepreciation": 999999 } ],
              "surfaces": [ { "fromFloorNumber": 2, "toFloorNumber": 3, "floorType": "T" } ] }
            """);

        var p = await PropAsync(aid, pid);
        Assert.Equal("Corrected land", p.LandDetail!.OwnerName);
        Assert.Equal(new[] { "T1x", "T2" }, p.LandDetail.Titles.Select(t => t.TitleNumber).Order());
        AssertBuildingEdited(p.BuildingDetail!, kept, dropped, "Corrected");
    }
}
