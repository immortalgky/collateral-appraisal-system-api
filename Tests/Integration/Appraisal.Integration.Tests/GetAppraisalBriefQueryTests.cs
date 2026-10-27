using System.Text.Json;
using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.GetAppraisalBrief;
using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Dapper;
using Integration.Fixtures;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// The brief is one hand-written Dapper batch of 20 result sets, and no unit test executes it. The two
/// big rows (AssetRow, ProjectRow) bind BY NAME: a SELECT alias that no longer matches a property does
/// not throw, it just leaves that property null, so a renamed alias would silently blank a field. This
/// runs the whole batch against the real migrated schema (views and masters included), with one
/// appraisal per branch the batch has (property-based, condo project, horizontal project), and asserts
/// a handful of seeded descriptive values so that a renamed alias fails here.
/// </summary>
[Collection("Integration")]
public class GetAppraisalBriefQueryTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Through the mediator rather than the HTTP route: the route needs the "appraisal.browse" policy,
    // which this harness's anonymous client does not hold, and the batch is what is under test.
    private async Task<JsonElement> GetBriefAsync(Guid appraisalId)
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetAppraisalBriefQuery(appraisalId), Ct);
        return JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    // A Thai label the brief should resolve for a code that exists only in this test, so the label result
    // set and its mapping are what produce the text, not a master row that happens to be seeded.
    private async Task SeedLabelAsync(string group, string code, string description)
    {
        await using var conn = new SqlConnection(Fixture.ConnectionString);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM parameter.Parameters WHERE [Group] = @group AND Code = @code AND [Language] = N'TH')
                INSERT parameter.Parameters ([Group], Country, [Language], Code, Description, IsActive, SeqNo)
                VALUES (@group, N'TH', N'TH', @code, @description, 1, 1)
            """,
            new { group, code, description }, cancellationToken: Ct));
    }

    [Fact]
    public async Task A_property_based_appraisal_returns_titles_condo_building_and_machine_detail()
    {
        await SeedLabelAsync("BuildingType", "ZB", "Test Building Type");
        Guid appraisalId;
        using (var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"BRF-{Guid.NewGuid():N}"[..18]);

            var land = appraisal.AddLandProperty();
            LandDetailSync.SyncTitles(land.LandDetail!,
                [new LandTitleItemData(null, "T1", "DEED", Rai: 1m), new LandTitleItemData(null, "T2", "NS3", Rai: 2m)]);
            var building = appraisal.AddBuildingProperty();
            building.BuildingDetail!.Update(ownerName: "Owner", buildingType: "ZB", roofType: ["01"]);
            // 30% done: the progress query's row, bound by name.
            building.SetConstructionInspection(ConstructionInspection.CreateSummary(
                building.Id, 1_000m, null, null, 30m, null));
            building.BuildingDetail.AddDepreciationDetail("Gross", isBuilding: false, priceAfterDepreciation: 1m);
            var condo = appraisal.AddCondoProperty();
            condo.CondoDetail!.Update(ownerName: "Owner", roomNumber: "9/9");
            condo.CondoDetail.AddCondoAreaDetail(CondoAppraisalAreaDetail.Create(1, "Room", 33m));
            var machine = appraisal.AddMachineryProperty();
            machine.MachineryDetail!.Update(propertyName: "Crane", brand: "Acme", registrationStatus: true);

            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(Ct);
            appraisalId = appraisal.Id;
        }

        var brief = await GetBriefAsync(appraisalId);

        Assert.Equal(4, brief.GetProperty("assets").GetArrayLength());
        var titles = brief.GetProperty("assets").EnumerateArray()
            .SelectMany(a => a.GetProperty("landTitles").EnumerateArray()).ToList();
        Assert.Equal(["T1", "T2"], titles.Select(t => t.GetProperty("titleNumber").GetString()));
        var listed = Assert.Single(brief.GetProperty("machines").EnumerateArray());
        Assert.Equal("Crane", listed.GetProperty("machineName").GetString());
        Assert.Equal("Acme", listed.GetProperty("brand").GetString());
        Assert.Equal("registered", listed.GetProperty("section").GetString());

        // Name-bound AssetRow columns: a roof code, a condo room number.
        var assets = brief.GetProperty("assets").EnumerateArray().ToList();
        Assert.Contains(assets, a => a.GetProperty("roofType").EnumerateArray().Any(c => c.GetString() == "01"));
        Assert.Contains(assets, a => a.GetProperty("roomNumber").GetString() == "9/9");

        // One assertion per remaining side result set: the inspection (ProgressRow), the condo area rows
        // (CondoAreaRow) and a parameter label (ParamLabelRow, resolved through LabelMap).
        Assert.Contains(assets, a => a.GetProperty("constructionProgressPercent").ValueKind == JsonValueKind.Number
                                     && a.GetProperty("constructionProgressPercent").GetDecimal() == 30m);
        var area = Assert.Single(assets.SelectMany(a => a.GetProperty("condoAreas").EnumerateArray()));
        Assert.Equal("Room", area.GetProperty("description").GetString());
        Assert.Equal(33m, area.GetProperty("size").GetDecimal());
        Assert.Contains(assets, a => a.GetProperty("buildingDisplayName").GetString() == "Test Building Type");

        // Standard body: D043, named from the DocumentTypes master by the last result set.
        Assert.Equal("D043", brief.GetProperty("summaryTypeCode").GetString());
        Assert.False(string.IsNullOrEmpty(brief.GetProperty("summaryTypeName").GetString()));
    }

    [Fact]
    public async Task A_progressive_appraisal_files_its_summary_as_D042()
    {
        Guid appraisalId;
        using (var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Progressive", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"BRG-{Guid.NewGuid():N}"[..18]);
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(Ct);
            appraisalId = appraisal.Id;
        }

        var brief = await GetBriefAsync(appraisalId);

        Assert.Equal("D042", brief.GetProperty("summaryTypeCode").GetString());
        Assert.False(string.IsNullOrEmpty(brief.GetProperty("summaryTypeName").GetString()));
    }

    [Theory]
    [InlineData(true)]    // condo project with a tower
    [InlineData(false)]   // horizontal (land and building) project with a house model
    public async Task A_block_appraisal_returns_its_project_detail(bool condo)
    {
        await SeedLabelAsync("PublicUtility", "ZU", "Test Utility");
        Guid appraisalId;
        using (var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Progressive", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"BRP-{Guid.NewGuid():N}"[..18]);
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(Ct);

            var project = Project.Create(
                appraisal.Id, condo ? ProjectType.Condo : ProjectType.LandAndBuilding,
                projectName: "Brief Test", developer: "Dev", utilities: ["ZU"], facilities: ["01"]);
            if (condo)
            {
                project.AddTower("A");
                project.ImportUnits("u.xlsx", null,
                    [ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, 1, "A", "9/1", "101", "T1", 30m, 1m)]);
            }
            else
            {
                project.AddModel(null, "Monaco");
                project.GetOrCreateLand().Update(ownerName: "Owner", roadFrontage: 12m);   // ProjectLandRow
                project.ImportUnits("u.xlsx", null,
                    [ProjectUnit.CreateLandAndBuilding(project.Id, Guid.Empty, 1, "1", "9/1", "Monaco", 2, 50m, 120m, 1m)]);
            }
            db.Projects.Add(project);
            await db.SaveChangesAsync(Ct);
            appraisalId = appraisal.Id;
        }

        var brief = await GetBriefAsync(appraisalId);

        var p = brief.GetProperty("project");
        // Block wins over the appraisal type.
        Assert.Equal("D043", brief.GetProperty("summaryTypeCode").GetString());
        Assert.Equal("Brief Test", p.GetProperty("projectName").GetString());
        Assert.Equal("Dev", p.GetProperty("developer").GetString());   // ProjectRow, bound by name
        Assert.Equal(1, p.GetProperty("unitCount").GetInt32());
        Assert.Equal(["Test Utility"], p.GetProperty("utilities").EnumerateArray().Select(u => u.GetString()));
        if (!condo) Assert.Equal(12m, p.GetProperty("land").GetProperty("roadFrontage").GetDecimal());
        Assert.Equal(condo ? 1 : 0, p.GetProperty("towers").GetArrayLength());
        Assert.Equal(condo ? 0 : 1, p.GetProperty("houseModels").GetArrayLength());
    }
}
