using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.Data;
using Workflow.Data.Entities;
using Workflow.Services.Configuration;
using Xunit;

namespace Workflow.Tests.Services.Configuration;

/// <summary>
/// Verifies the most-specific-wins resolution across the (WorkflowDefinitionId, BankingSegment) scopes.
/// </summary>
public class TaskConfigurationServiceTests : IDisposable
{
    private const string Activity = "int-appraisal-checker";
    private const string Workflow = "wf-1";

    private readonly WorkflowDbContext _db;
    private readonly TaskConfigurationService _sut;

    public TaskConfigurationServiceTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);
        _sut = new TaskConfigurationService(_db, Substitute.For<ILogger<TaskConfigurationService>>());
    }

    private void Seed(string label, string? workflowDefinitionId, string? bankingSegment, bool isActive = true)
    {
        var entity = TaskAssignmentConfiguration.Create(
            Activity, "[]", "[]", "tester", workflowDefinitionId, null, label, bankingSegment,
            isActive: isActive);
        _db.TaskAssignmentConfigurations.Add(entity);
        _db.SaveChanges();
    }

    [Fact]
    public async Task GetConfigurationAsync_PicksMostSpecific_WorkflowAndSegment()
    {
        Seed("wf+seg", Workflow, "Retail");
        Seed("wf-only", Workflow, null);
        Seed("seg-only", null, "Retail");
        Seed("wildcard", null, null);

        var result = await _sut.GetConfigurationAsync(Activity, Workflow, "Retail");

        result!.AssigneeGroup.Should().Be("wf+seg");
    }

    [Fact]
    public async Task GetConfigurationAsync_FallsBackToWorkflowOnly_WhenSegmentDiffers()
    {
        Seed("wf+retail", Workflow, "Retail");
        Seed("wf-only", Workflow, null);
        Seed("wildcard", null, null);

        // IBG has no specific row → workflow-only (tier 3) beats wildcard (tier 1).
        var result = await _sut.GetConfigurationAsync(Activity, Workflow, "IBG");

        result!.AssigneeGroup.Should().Be("wf-only");
    }

    [Fact]
    public async Task GetConfigurationAsync_SegmentOnlyBeatsWildcard_WhenNoWorkflowMatch()
    {
        Seed("seg-only", null, "Retail");
        Seed("wildcard", null, null);

        // Unknown workflow → only null-workflow rows are eligible; segment match wins.
        var result = await _sut.GetConfigurationAsync(Activity, "other-wf", "Retail");

        result!.AssigneeGroup.Should().Be("seg-only");
    }

    [Fact]
    public async Task GetConfigurationAsync_EmptySegment_TreatedAsNoSegment()
    {
        Seed("seg-only", null, "Retail");
        Seed("wildcard", null, null);

        // Empty/whitespace segment must not match the Retail row — falls to the wildcard.
        var result = await _sut.GetConfigurationAsync(Activity, null, "");

        result!.AssigneeGroup.Should().Be("wildcard");
    }

    [Fact]
    public async Task GetConfigurationAsync_IgnoresInactiveRows()
    {
        Seed("inactive-specific", Workflow, "Retail", isActive: false);
        Seed("active-wildcard", null, null);

        var result = await _sut.GetConfigurationAsync(Activity, Workflow, "Retail");

        result!.AssigneeGroup.Should().Be("active-wildcard");
    }

    [Fact]
    public async Task GetConfigurationAsync_NoRows_ReturnsNull()
    {
        var result = await _sut.GetConfigurationAsync(Activity, Workflow, "Retail");

        result.Should().BeNull();
    }

    public void Dispose() => _db.Dispose();
}
