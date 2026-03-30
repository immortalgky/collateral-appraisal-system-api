using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.Workflow;

public class RoutingActivityTests
{
    private readonly RoutingActivity _sut;

    public RoutingActivityTests()
    {
        var logger = Substitute.For<ILogger<RoutingActivity>>();
        _sut = new RoutingActivity(logger);
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
        // Arrange — default decision is auto_assign_external
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
        result.OutputData["selectionMethod"].Should().Be("roundrobin");
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
    public async Task ExecuteAsync_AutoAssignExternal_SetsSelectionMethodToRoundRobin()
    {
        // Arrange — auto_assign_external should set selectionMethod for CompanySelectionActivity
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
        result.OutputData.Should().ContainKey("selectionMethod");
        result.OutputData["selectionMethod"].Should().Be("roundrobin");
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
