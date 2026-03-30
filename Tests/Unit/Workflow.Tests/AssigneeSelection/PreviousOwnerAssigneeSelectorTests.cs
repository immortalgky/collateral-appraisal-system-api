using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Strategies;
using Workflow.Data;
using Workflow.Workflow.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Workflow.Tests.AssigneeSelection;

public class PreviousOwnerAssigneeSelectorTests
{
    private readonly PreviousOwnerAssigneeSelector _selector;

    public PreviousOwnerAssigneeSelectorTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"PreviousOwnerTests_{Guid.NewGuid()}")
            .Options;
        var dbContext = new WorkflowDbContext(options);
        var logger = Substitute.For<ILogger<PreviousOwnerAssigneeSelector>>();
        _selector = new PreviousOwnerAssigneeSelector(dbContext, logger);
    }

    [Fact]
    public async Task SelectAssigneeAsync_EmptyWorkflowInstanceId_ReturnsFailure()
    {
        var context = new AssignmentContext
        {
            WorkflowInstanceId = Guid.Empty,
            ActivityName = "ext-admin"
        };

        var result = await _selector.SelectAssigneeAsync(context);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("WorkflowInstanceId");
    }

    [Fact]
    public async Task SelectAssigneeAsync_NoPreviousExecution_ReturnsFailure()
    {
        var context = new AssignmentContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityName = "ext-admin"
        };

        var result = await _selector.SelectAssigneeAsync(context);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No previous owner");
    }

    [Fact]
    public async Task SelectAssigneeAsync_HasPreviousExecution_ReturnsPreviousOwner()
    {
        // Arrange — seed a completed execution
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"PreviousOwnerTests_{Guid.NewGuid()}")
            .Options;
        var dbContext = new WorkflowDbContext(options);

        var workflowInstanceId = Guid.NewGuid();
        var execution = WorkflowActivityExecution.Create(
            workflowInstanceId, "ext-admin", "External Admin", "TaskActivity", "th.admin1",
            new Dictionary<string, object>());
        execution.Complete("th.admin1", new Dictionary<string, object>(), "Done");

        dbContext.WorkflowActivityExecutions.Add(execution);
        await dbContext.SaveChangesAsync();

        var logger = Substitute.For<ILogger<PreviousOwnerAssigneeSelector>>();
        var selector = new PreviousOwnerAssigneeSelector(dbContext, logger);

        var context = new AssignmentContext
        {
            WorkflowInstanceId = workflowInstanceId,
            ActivityName = "ext-admin"
        };

        // Act
        var result = await selector.SelectAssigneeAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("th.admin1");
        result.Metadata.Should().ContainKey("PreviouslyCompletedBy");
    }
}
