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

public class SameAssigneeAsActivitySelectorTests
{
    private static SameAssigneeAsActivitySelector CreateSelector(out WorkflowDbContext dbContext)
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"SameAssigneeTests_{Guid.NewGuid()}")
            .Options;
        dbContext = new WorkflowDbContext(options);
        var logger = Substitute.For<ILogger<SameAssigneeAsActivitySelector>>();
        return new SameAssigneeAsActivitySelector(dbContext, logger);
    }

    [Fact]
    public async Task SelectAssigneeAsync_MissingProperty_ReturnsFailure()
    {
        var selector = CreateSelector(out _);
        var context = new AssignmentContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityName = "appraisal-book-verification"
        };

        var result = await selector.SelectAssigneeAsync(context);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("sameAssigneeAsActivity");
    }

    [Fact]
    public async Task SelectAssigneeAsync_EmptyWorkflowInstanceId_ReturnsFailure()
    {
        var selector = CreateSelector(out _);
        var context = new AssignmentContext
        {
            WorkflowInstanceId = Guid.Empty,
            ActivityName = "appraisal-book-verification",
            Properties = new Dictionary<string, object> { ["sameAssigneeAsActivity"] = "int-pma-input" }
        };

        var result = await selector.SelectAssigneeAsync(context);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("WorkflowInstanceId");
    }

    [Fact]
    public async Task SelectAssigneeAsync_NoCompletedSourceExecution_ReturnsFailure()
    {
        var selector = CreateSelector(out _);
        var context = new AssignmentContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityName = "appraisal-book-verification",
            Properties = new Dictionary<string, object> { ["sameAssigneeAsActivity"] = "int-pma-input" }
        };

        var result = await selector.SelectAssigneeAsync(context);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No completed assignee");
    }

    [Fact]
    public async Task SelectAssigneeAsync_SourceActivityCompleted_ReturnsSameAssignee()
    {
        // Arrange — seed a completed PMA input execution assigned to th.pma1
        var selector = CreateSelector(out var dbContext);
        var workflowInstanceId = Guid.NewGuid();

        var pmaInput = WorkflowActivityExecution.Create(
            workflowInstanceId, "int-pma-input", "PMA Property Input", "TaskActivity", "th.pma1",
            new Dictionary<string, object>());
        pmaInput.Complete("th.pma1", new Dictionary<string, object>(), "Done");

        dbContext.WorkflowActivityExecutions.Add(pmaInput);
        await dbContext.SaveChangesAsync();

        var context = new AssignmentContext
        {
            WorkflowInstanceId = workflowInstanceId,
            ActivityName = "appraisal-book-verification",
            Properties = new Dictionary<string, object> { ["sameAssigneeAsActivity"] = "int-pma-input" }
        };

        // Act
        var result = await selector.SelectAssigneeAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("th.pma1");
        result.Metadata.Should().ContainKey("SourceActivity");
    }
}
