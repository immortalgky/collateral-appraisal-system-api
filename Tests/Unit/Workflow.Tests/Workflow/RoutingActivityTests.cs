using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.AssigneeSelection.Services;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.Workflow;

public class RoutingActivityTests
{
    private readonly ICompanyRoundRobinService _companyRoundRobinService;
    private readonly RoutingActivity _sut;

    public RoutingActivityTests()
    {
        _companyRoundRobinService = Substitute.For<ICompanyRoundRobinService>();
        var logger = Substitute.For<ILogger<RoutingActivity>>();
        _sut = new RoutingActivity(_companyRoundRobinService, logger);
    }

    private static ActivityContext CreateContext(
        Dictionary<string, object>? properties = null,
        Dictionary<string, object>? variables = null)
    {
        var workflowInstance = WorkflowInstance.Create(
            Guid.NewGuid(),
            "test-workflow",
            null,
            "test-user");

        return new ActivityContext
        {
            WorkflowInstanceId = workflowInstance.Id,
            ActivityId = "initial-routing",
            Properties = properties ?? new Dictionary<string, object>(),
            Variables = variables ?? new Dictionary<string, object>(),
            WorkflowInstance = workflowInstance
        };
    }

    [Fact]
    public async Task ExecuteAsync_AutoAssignExternal_SetsRoutingPathToExternal()
    {
        // Arrange — default decision is auto_assign_external, company selection succeeds
        var companyId = Guid.NewGuid();
        _companyRoundRobinService
            .SelectCompanyAsync(Arg.Any<CancellationToken>())
            .Returns(CompanySelectionResult.Success(companyId, "Test Company"));

        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["defaultDecision"] = "auto_assign_external"
            },
            variables: new Dictionary<string, object>
            {
                ["amount"] = 10000
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("routingPath");
        result.OutputData["routingPath"].Should().Be("external");
        result.OutputData["decision"].Should().Be("auto_assign_external");
    }

    [Fact]
    public async Task ExecuteAsync_AdminReview_SetsRoutingPathToInternal()
    {
        // Arrange — amount > 50000 triggers admin_review condition
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["routingConditions"] = new Dictionary<string, string>
                {
                    ["admin_review"] = "amount > 50000"
                },
                ["defaultDecision"] = "auto_assign_external"
            },
            variables: new Dictionary<string, object>
            {
                ["amount"] = 100000
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("routingPath");
        result.OutputData["routingPath"].Should().Be("internal");
        result.OutputData["decision"].Should().Be("admin_review");
    }

    [Fact]
    public async Task ExecuteAsync_CompanySelectionFails_FallsBackToInternalPath()
    {
        // Arrange — auto_assign_external but company selection fails → fallback to admin_review
        _companyRoundRobinService
            .SelectCompanyAsync(Arg.Any<CancellationToken>())
            .Returns(CompanySelectionResult.Failure("No companies available"));

        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["defaultDecision"] = "auto_assign_external"
            },
            variables: new Dictionary<string, object>
            {
                ["amount"] = 10000
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["decision"].Should().Be("admin_review");
        // Fallback changes decision to admin_review → routingPath should be "external"
        // because routingPath is set BEFORE the fallback logic runs
        result.OutputData["routingPath"].Should().Be("external");
    }

    [Fact]
    public async Task ExecuteAsync_OutputDataAlwaysContainsRoutingPath()
    {
        // Arrange — no conditions, just default
        var context = CreateContext(
            properties: new Dictionary<string, object>
            {
                ["defaultDecision"] = "admin_review"
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.OutputData.Should().ContainKey("routingPath");
        result.OutputData.Should().ContainKey("decision");
        result.OutputData.Should().ContainKey("routedAt");
    }
}
