using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Appraisal.Application.Features.Appraisals.CorrectPropertyData;
using Appraisal.Application.Features.Project.CorrectProjectData;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Exceptions;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// The project data-correction command against a real database, through the MediatR pipeline: the block
/// page's own commands run inside the correction's transaction, the audit row commits or rolls back with
/// them, and the block page's own write routes refuse a Completed appraisal.
/// </summary>
[Collection("Integration")]
public class ProjectCorrectionAuditTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private IServiceScope CreateScope()
        => Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>A condo block appraisal with one tower and one unit priced at 1,000,000.</summary>
    private async Task<(Guid AppraisalId, Guid TowerId)> SeedAsync(bool completed, CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        appraisal.SetAppraisalNumber($"PCR-{Guid.NewGuid():N}"[..18]);
        if (completed)
            appraisal.SyncStatusFromWorkflow(AppraisalStatus.Completed);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(ct);

        var project = Project.Create(appraisal.Id, ProjectType.Condo, projectName: "Before", landOffice: "Office A");
        var tower = project.AddTower("Tower A");
        var unit = ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, 1, "A", "9/1", "101", "T1", 30m, 1m);
        project.ImportUnits("units.xlsx", null, [unit]);
        project.SetCondoPricingAssumption("AdjustPriceSqm", 0m, 0m, 0m, 0m, 0m, null, null, 70);
        db.Projects.Add(project);

        var price = ProjectUnitPrice.Create(unit.Id);
        price.ApplyImportedValues(1m, 1_000_000m, 700_000m, 10_000m);
        db.ProjectUnitPrices.Add(price);
        await db.SaveChangesAsync(ct);

        return (appraisal.Id, tower.Id);
    }

    private async Task<CorrectPropertyDataResult> CorrectAsync(
        Guid appraisalId, string target, Guid? targetId, string data, CancellationToken ct)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CorrectProjectDataCommand(appraisalId, target, targetId, "integration test", Body(data)), ct);
    }

    private async Task<List<AppraisalPropertyCorrectionLog>> LogsAsync(Guid appraisalId, CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        return await db.AppraisalPropertyCorrectionLogs.AsNoTracking()
            .Where(l => l.AppraisalId == appraisalId).ToListAsync(ct);
    }

    private async Task<Project> ReloadAsync(Guid appraisalId, CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        return await db.Projects.AsNoTracking().Include(p => p.Towers).FirstAsync(p => p.AppraisalId == appraisalId, ct);
    }

    [Fact]
    public async Task Info_correction_commits_the_change_and_its_audit_row_together()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, _) = await SeedAsync(completed: true, ct);

        // The payload says LB; the stored type (U) is what the save runs as.
        var result = await CorrectAsync(appraisalId, ProjectCorrectionTargets.Info, null,
            """{ "projectType": "LB", "projectName": "After", "landOffice": "Office A" }""", ct);

        var project = await ReloadAsync(appraisalId, ct);
        Assert.Equal("After", project.ProjectName);
        Assert.Equal(ProjectType.Condo, project.ProjectType);

        var log = Assert.Single(await LogsAsync(appraisalId, ct));
        Assert.Null(log.AppraisalPropertyId);
        Assert.Null(log.TargetId);
        Assert.Equal(AppraisalPropertyCorrectionLog.ProjectEntryType, log.PropertyType);
        Assert.Equal("integration test", log.Reason);
        Assert.Contains("Project.ProjectName", result.ChangedFields);
        Assert.DoesNotContain("Project.LandOffice", result.ChangedFields); // sent unchanged
    }

    [Fact]
    public async Task Tower_correction_records_which_tower_it_changed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, towerId) = await SeedAsync(completed: true, ct);

        var result = await CorrectAsync(appraisalId, ProjectCorrectionTargets.Tower, towerId,
            """{ "towerName": "Tower B" }""", ct);

        Assert.Equal("Tower B", (await ReloadAsync(appraisalId, ct)).Towers.Single(t => t.Id == towerId).TowerName);
        Assert.Contains("Tower.TowerName", result.ChangedFields);
        var log = Assert.Single(await LogsAsync(appraisalId, ct));
        Assert.Equal(AppraisalPropertyCorrectionLog.ProjectTowerEntryType, log.PropertyType);
        Assert.Equal(towerId, log.TargetId);
    }

    [Fact]
    public async Task Unit_price_correction_stores_the_flags_and_keys_the_audit_by_unit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, _) = await SeedAsync(completed: true, ct);
        Guid unitId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var projectId = await db.Projects.Where(p => p.AppraisalId == appraisalId).Select(p => p.Id).SingleAsync(ct);
            unitId = await db.ProjectUnits.Where(u => u.ProjectId == projectId).Select(u => u.Id).SingleAsync(ct);
        }

        var result = await CorrectAsync(appraisalId, ProjectCorrectionTargets.UnitPrices, null,
            $$"""{ "unitPriceFlags": [ { "projectUnitId": "{{unitId}}", "isCorner": true, "isEdge": false, "isOther": false } ] }""",
            ct);

        Assert.Contains(result.ChangedFields, f => f.StartsWith("UnitPrices.") && f.EndsWith(".IsCorner"));
        var log = Assert.Single(await LogsAsync(appraisalId, ct));
        Assert.Equal(AppraisalPropertyCorrectionLog.ProjectUnitPriceEntryType, log.PropertyType);
    }

    [Fact]
    public async Task A_correction_that_changes_nothing_is_rejected_and_writes_no_audit_row()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, towerId) = await SeedAsync(completed: true, ct);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => CorrectAsync(
            appraisalId, ProjectCorrectionTargets.Tower, towerId, """{ "towerName": "Tower A" }""", ct));

        Assert.Equal("NO_CHANGES", exception.Code);
        Assert.Empty(await LogsAsync(appraisalId, ct));
    }

    [Fact]
    public async Task A_correction_of_an_appraisal_that_is_not_completed_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, _) = await SeedAsync(completed: false, ct);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => CorrectAsync(
            appraisalId, ProjectCorrectionTargets.Info, null,
            """{ "projectName": "After", "landOffice": "Office A" }""", ct));

        Assert.Equal("APPRAISAL_NOT_COMPLETED", exception.Code);
        Assert.Equal("Before", (await ReloadAsync(appraisalId, ct)).ProjectName);
    }

    [Fact]
    public async Task The_block_pages_own_save_refuses_a_completed_appraisal()
    {
        var ct = TestContext.Current.CancellationToken;
        var (appraisalId, _) = await SeedAsync(completed: true, ct);

        var response = await _client.PutAsJsonAsync(
            $"/appraisals/{appraisalId}/project",
            new { projectType = "U", projectName = "Sneaky", landOffice = "Office A" }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("APPRAISAL_CLOSED", await response.Content.ReadAsStringAsync(ct));
        Assert.Equal("Before", (await ReloadAsync(appraisalId, ct)).ProjectName);
    }

    [Fact]
    public void The_correction_handler_cannot_recompute_valuation()
    {
        // The approved appraisal total stays as approved: the unit-price targets send their commands with
        // RecomputeValuation off, and the handler itself has no way to call RecomputeAsync.
        var dependencies = typeof(CorrectProjectDataCommandHandler).GetConstructors().Single()
            .GetParameters().Select(p => p.ParameterType);

        Assert.DoesNotContain(typeof(global::Appraisal.Application.Services.AppraisalValuationSummaryService), dependencies);
    }
}
