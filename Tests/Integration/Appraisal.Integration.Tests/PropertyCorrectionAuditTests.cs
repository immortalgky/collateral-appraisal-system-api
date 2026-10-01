using System.Text.Json;
using Appraisal.Application.Features.Appraisals.CorrectPropertyData;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Exceptions;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// The data-correction command against a real database, through the MediatR pipeline. A unit test cannot
/// cover what matters most here: that the change and its audit row commit or roll back together
/// (TransactionalBehavior), that rows created by the payload are flushed with their ids before the
/// after-snapshot is taken, and that the real update logic works when nothing but the correction's own
/// SaveChanges persists it (the condo / land-and-building handlers call UpdateAsync, this path does not).
/// </summary>
[Collection("Integration")]
public class PropertyCorrectionAuditTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>Persists a Completed appraisal holding the property built by <paramref name="add"/>.</summary>
    private async Task<(Guid AppraisalId, Guid PropertyId)> SeedAsync(
        Func<AppraisalAggregate, AppraisalProperty> add, CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"COR-{Guid.NewGuid():N}"[..18]);
        var property = add(appraisal);
        appraisal.SyncStatusFromWorkflow(AppraisalStatus.Completed);

        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(ct);

        return (appraisal.Id, property.Id);
    }

    private async Task<CorrectPropertyDataResult> CorrectAsync(
        Guid appraisalId, Guid propertyId, string suffix, string data, CancellationToken ct)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CorrectPropertyDataCommand(appraisalId, propertyId, suffix, "integration test", Body(data)), ct);
    }

    /// <summary>Reads the property back in a fresh scope, so only what was committed is visible.</summary>
    private async Task<AppraisalProperty> ReloadAsync(Guid appraisalId, Guid propertyId, CancellationToken ct)
    {
        using var scope = CreateScope();
        var appraisal = await scope.ServiceProvider.GetRequiredService<IAppraisalRepository>()
            .GetByIdWithPropertiesAsync(appraisalId, ct);
        return appraisal!.GetProperty(propertyId)!;
    }

    private async Task<List<AppraisalPropertyCorrectionLog>> LogsAsync(Guid appraisalId, CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        return await db.AppraisalPropertyCorrectionLogs.AsNoTracking()
            .Where(l => l.AppraisalId == appraisalId).ToListAsync(ct);
    }

    [Fact]
    public async Task Land_correction_commits_the_change_and_its_audit_row_together()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedAsync(a =>
        {
            var property = a.AddLandProperty();
            property.LandDetail!.Update(ownerName: "Owner A", street: "Rama II");
            var title = LandTitle.Create(property.LandDetail.Id, "111", "DEED");
            title.Update(null, null, null, null, null, null, null, null, LandArea.Create(2m, 0m, 0m),
                null, null, null, null, null, null, null);
            property.LandDetail.AddTitle(title);
            return property;
        }, ct);
        var titleId = (await ReloadAsync(appraisalId, propertyId, ct)).LandDetail!.Titles.Single().Id;

        var result = await CorrectAsync(appraisalId, propertyId, "land-detail", $$"""
            { "ownerNameLand": "Owner B", "street": "Rama II",
              "titles": [ { "id": "{{titleId}}", "titleNumber": "112", "titleType": "DEED", "rai": 3, "ngan": 0, "squareWa": 0 } ],
              "landAreaDeductions": [ { "reasonCode": "99", "reasonOther": "canal", "areaInSqWa": 4 } ] }
            """, ct);

        // The committed state: new values, the renumbered title in place, and the new deduction with an id.
        var land = (await ReloadAsync(appraisalId, propertyId, ct)).LandDetail!;
        Assert.Equal("Owner B", land.OwnerName);
        var title = Assert.Single(land.Titles);
        Assert.Equal(titleId, title.Id);
        Assert.Equal("112", title.TitleNumber);
        Assert.Equal(3m, title.Area!.Rai);
        var deduction = Assert.Single(land.Deductions);
        Assert.NotEqual(Guid.Empty, deduction.Id);
        Assert.Equal(4m, land.DeductedAreaInSqWa);

        // The audit row was written in the same transaction and describes exactly that.
        var log = Assert.Single(await LogsAsync(appraisalId, ct));
        Assert.Equal(propertyId, log.AppraisalPropertyId);
        Assert.Equal(PropertyType.Land.Code, log.PropertyType);
        Assert.Equal("integration test", log.Reason);
        Assert.False(string.IsNullOrWhiteSpace(log.ChangedBy));

        using var changes = JsonDocument.Parse(log.ChangedFields);
        var fields = changes.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(fields.Order(), result.ChangedFields.Order());
        Assert.Contains("Land.OwnerName", fields);
        Assert.Contains("Land.Titles[#111].TitleNumber", fields);
        Assert.Contains("Land.Titles[#111].Rai", fields);
        Assert.DoesNotContain("Land.Street", fields); // sent unchanged
        var added = Assert.Single(fields, f => f.StartsWith("Land.Deductions["));
        Assert.Contains("canal", changes.RootElement.GetProperty(added).GetProperty("to").GetString());
    }

    [Fact]
    public async Task A_correction_that_only_reorders_titles_is_stored_and_audited_as_one_field()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedAsync(a =>
        {
            var property = a.AddLandProperty();
            foreach (var number in new[] { "111", "222", "333" })
            {
                var title = LandTitle.Create(property.LandDetail!.Id, number, "DEED");
                title.Update(null, null, null, null, null, null, null, null, LandArea.Create(1m, 0m, 0m),
                    null, null, null, null, null, null, null);
                title.SetSequenceNumber(property.LandDetail.Titles.Count + 1);
                property.LandDetail.AddTitle(title);
            }
            return property;
        }, ct);
        var ids = (await ReloadAsync(appraisalId, propertyId, ct)).LandDetail!.Titles
            .ToDictionary(t => t.TitleNumber, t => t.Id);

        string Row(string number) =>
            $$"""{ "id": "{{ids[number]}}", "titleNumber": "{{number}}", "titleType": "DEED", "rai": 1, "ngan": 0, "squareWa": 0 }""";

        var result = await CorrectAsync(appraisalId, propertyId, "land-detail",
            $$"""{ "titles": [ {{Row("333")}}, {{Row("111")}}, {{Row("222")}} ] }""", ct);

        Assert.Equal(["Land.TitleOrder"], result.ChangedFields);
        var titles = (await ReloadAsync(appraisalId, propertyId, ct)).LandDetail!.Titles;
        Assert.Equal(["333", "111", "222"], titles.Select(t => t.TitleNumber));
        Assert.Equal(ids.Values.Order(), titles.Select(t => t.Id).Order()); // reordered in place, none re-created
        var log = Assert.Single(await LogsAsync(appraisalId, ct));
        using var changes = JsonDocument.Parse(log.ChangedFields);
        Assert.Equal("111, 222, 333", changes.RootElement.GetProperty("Land.TitleOrder").GetProperty("from").GetString());
        Assert.Equal("333, 111, 222", changes.RootElement.GetProperty("Land.TitleOrder").GetProperty("to").GetString());
    }

    [Fact]
    public async Task A_correction_that_changes_nothing_is_rejected_and_rolls_back_what_it_flushed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedAsync(a =>
        {
            var property = a.AddBuildingProperty();
            property.BuildingDetail!.Update(ownerName: "Owner A", isAppraisable: true);
            var dep = property.BuildingDetail.AddDepreciationDetail("Period", "main", 10m, 2020);
            dep.AddPeriod(1, 5, 2m, 10m, 100m);
            property.BuildingDetail.ResolveDerivedValues(); // as saved since insurance became a stored value
            return property;
        }, ct);
        var before = (await ReloadAsync(appraisalId, propertyId, ct)).BuildingDetail!.DepreciationDetails.Single();
        var depId = before.Id;
        var periodId = before.DepreciationPeriods.Single().Id;

        // Same content: the periods are deleted and re-created with new ids, which is flushed before the
        // diff finds nothing to report.
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => CorrectAsync(
            appraisalId, propertyId, "building-detail", $$"""
            { "ownerNameBuilding": "Owner A", "isAppraisable": true,
              "depreciationDetails": [ { "id": "{{depId}}", "depreciationMethod": "Period", "areaDescription": "main",
                "area": 10, "year": 2020,
                "depreciationPeriods": [ { "atYear": 1, "toYear": 5, "depreciationPerYear": 2, "totalDepreciationPct": 10, "priceDepreciation": 100 } ] } ] }
            """, ct));

        Assert.Equal("NO_CHANGES", exception.Code);
        Assert.Empty(await LogsAsync(appraisalId, ct));
        var period = (await ReloadAsync(appraisalId, propertyId, ct))
            .BuildingDetail!.DepreciationDetails.Single().DepreciationPeriods.Single();
        Assert.Equal(periodId, period.Id); // the flush was rolled back: still the original row
    }

    [Fact]
    public async Task Building_correction_persists_a_changed_period_and_reports_only_that_field()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedAsync(a =>
        {
            var property = a.AddBuildingProperty();
            property.BuildingDetail!.Update(ownerName: "Owner A", isAppraisable: true);
            property.BuildingDetail.AddDepreciationDetail("Period", "main", 10m, 2020)
                .AddPeriod(1, 5, 2m, 10m, 100m);
            property.BuildingDetail.ResolveDerivedValues(); // as saved since insurance became a stored value
            return property;
        }, ct);
        var depId = (await ReloadAsync(appraisalId, propertyId, ct)).BuildingDetail!.DepreciationDetails.Single().Id;

        var result = await CorrectAsync(appraisalId, propertyId, "building-detail", $$"""
            { "ownerNameBuilding": "Owner A", "isAppraisable": true,
              "depreciationDetails": [ { "id": "{{depId}}", "depreciationMethod": "Period", "areaDescription": "main",
                "area": 10, "year": 2020,
                "depreciationPeriods": [ { "atYear": 1, "toYear": 5, "depreciationPerYear": 3, "totalDepreciationPct": 10, "priceDepreciation": 100 } ] } ] }
            """, ct);

        Assert.Equal(["Building.DepreciationDetails[1].DepreciationPeriods[1].DepreciationPerYear"], result.ChangedFields);
        var period = (await ReloadAsync(appraisalId, propertyId, ct))
            .BuildingDetail!.DepreciationDetails.Single().DepreciationPeriods.Single();
        Assert.Equal(3m, period.DepreciationPerYear);
    }

    [Fact]
    public async Task Condo_correction_persists_new_area_rows_and_keeps_the_stored_insurance_price()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedAsync(a =>
        {
            var property = a.AddCondoProperty();
            // No fire-insurance code, so a re-derivation would produce null: keeping 777 proves it was kept.
            property.CondoDetail!.Update(
                ownerName: "Owner A", usableArea: 50m, buildingInsurancePrice: 777m,
                address: Address.Create("Sub", "Dist", "Prov"));
            return property;
        }, ct);

        var result = await CorrectAsync(appraisalId, propertyId, "condo-detail", """
            { "ownerName": "Owner B", "usableArea": 50, "subDistrict": "Sub", "district": "Dist", "province": "Prov",
              "areaDetails": [ { "sequence": 1, "areaDescription": "living", "areaSize": 30 } ] }
            """, ct);

        var condo = (await ReloadAsync(appraisalId, propertyId, ct)).CondoDetail!;
        Assert.Equal("Owner B", condo.OwnerName);
        Assert.Equal(777m, condo.BuildingInsurancePrice);
        var area = Assert.Single(condo.AreaDetails);
        Assert.Equal("living", area.AreaDescription);
        Assert.Contains("Condo.OwnerName", result.ChangedFields);
        Assert.DoesNotContain("Condo.BuildingInsurancePrice", result.ChangedFields);
    }

    [Fact]
    public async Task Lease_agreement_correction_persists_the_lease_it_carries()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, propertyId) = await SeedAsync(a =>
        {
            var property = a.AddLeaseAgreementLandProperty();
            property.LandDetail!.Update(ownerName: "Owner A");
            return property;
        }, ct);

        await CorrectAsync(appraisalId, propertyId, "lease-agreement-land-detail", """
            { "ownerNameLand": "Owner A", "leaseAgreement": { "lesseeName": "Lessee", "contractNo": "C-1" } }
            """, ct);

        var lease = (await ReloadAsync(appraisalId, propertyId, ct)).LeaseAgreementDetail!;
        Assert.Equal("Lessee", lease.LesseeName);
        Assert.Equal("C-1", lease.ContractNo);
    }

    [Fact]
    public void The_correction_handler_cannot_recompute_valuation()
    {
        // Approved figures stay as approved: the handler has no way to call RecomputeAsync at all.
        var dependencies = typeof(CorrectPropertyDataCommandHandler).GetConstructors().Single()
            .GetParameters().Select(p => p.ParameterType);

        Assert.DoesNotContain(typeof(global::Appraisal.Application.Services.AppraisalValuationSummaryService), dependencies);
    }
}
