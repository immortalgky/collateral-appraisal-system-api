using Assignment.AssigneeSelection.Core;
using Assignment.AssigneeSelection.Engine;
using Assignment.AssigneeSelection.Factories;
using Assignment.AssigneeSelection.Services;
using Assignment.Services.Configuration;
using Assignment.Services.Configuration.Models;
using Assignment.Workflow.Actions.Core;
using Assignment.Workflow.Activities;
using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Assignment.Tests.Workflow;

/// <summary>
/// Simplified workflow tests focusing on core functionality
/// </summary>
public class SimpleWorkflowTests
{
    [Fact]
    public void TaskActivity_Constructor_ShouldCreateInstanceSuccessfully()
    {
        // Arrange
        var assigneeSelectorFactory = Substitute.For<IAssigneeSelectorFactory>();
        var cascadingAssignmentEngine = Substitute.For<ICascadingAssignmentEngine>();
        var configurationService = Substitute.For<ITaskConfigurationService>();
        var customAssignmentServiceFactory = Substitute.For<ICustomAssignmentServiceFactory>();
        var actionExecutor = Substitute.For<IWorkflowActionExecutor>();
        var auditService = Substitute.For<IWorkflowAuditService>();
        var logger = Substitute.For<ILogger<TaskActivity>>();

        // Act
        var taskActivity = new TaskActivity(
            assigneeSelectorFactory,
            cascadingAssignmentEngine,
            configurationService,
            customAssignmentServiceFactory,
            actionExecutor,
            auditService,
            logger);

        // Assert
        taskActivity.Should().NotBeNull();
        taskActivity.ActivityType.Should().Be("TaskActivity");
        taskActivity.Name.Should().Be("Task Activity");
    }

    [Fact]
    public void AssigneeSelectionResult_Success_ShouldCreateSuccessResult()
    {
        // Arrange
        var assigneeId = "test-user-123";
        var metadata = new Dictionary<string, object> { ["method"] = "supervisor" };

        // Act
        var result = AssigneeSelectionResult.Success(assigneeId, metadata);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be(assigneeId);
        result.Metadata.Should().ContainKey("method");
    }

    [Fact]
    public void AssigneeSelectionResult_Failure_ShouldCreateFailureResult()
    {
        // Arrange
        var errorMessage = "No suitable assignee found";

        // Act
        var result = AssigneeSelectionResult.Failure(errorMessage);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.AssigneeId.Should().BeNull();
    }

    [Fact]
    public void ActivityResult_Success_ShouldCreateCompletedResult()
    {
        // Arrange
        var outputData = new Dictionary<string, object> 
        { 
            ["Decision"] = "Approved", 
            ["Value"] = 250000 
        };

        // Act
        var result = ActivityResult.Success(outputData, "next-activity", "Task completed successfully");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("Decision");
        result.NextActivityId.Should().Be("next-activity");
        result.Comments.Should().Be("Task completed successfully");
    }

    [Fact]
    public void ActivityResult_Failed_ShouldCreateFailedResult()
    {
        // Arrange
        var errorMessage = "Assignment failed";

        // Act
        var result = ActivityResult.Failed(errorMessage);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void ActivityContext_Creation_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var workflowInstanceId = Guid.NewGuid();
        var activityId = "test-activity";
        var variables = new Dictionary<string, object> { ["ClientId"] = 123 };

        // Act
        var context = new ActivityContext
        {
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            Variables = variables,
            CurrentAssignee = "test-user@example.com"
        };

        // Assert
        context.Should().NotBeNull();
        context.WorkflowInstanceId.Should().Be(workflowInstanceId);
        context.ActivityId.Should().Be(activityId);
        context.Variables.Should().ContainKey("ClientId");
        context.CurrentAssignee.Should().Be("test-user@example.com");
    }

    [Fact]
    public void ActionBatchExecutionResult_Creation_ShouldSetPropertiesCorrectly()
    {
        // Act
        var result = new ActionBatchExecutionResult
        {
            IsSuccess = true,
            SuccessfulActions = 3,
            FailedActions = 0,
            TotalExecutionDuration = TimeSpan.FromSeconds(2.5)
        };

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.SuccessfulActions.Should().Be(3);
        result.FailedActions.Should().Be(0);
        result.TotalExecutionDuration.Should().Be(TimeSpan.FromSeconds(2.5));
    }

    [Fact]
    public void TaskAssignmentConfigurationDto_Creation_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var activityId = "test-activity";
        var strategies = new[] { "supervisor", "manual" };

        // Act
        var config = new TaskAssignmentConfigurationDto
        {
            Id = id,
            ActivityId = activityId,
            WorkflowDefinitionId = "test-workflow",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        config.Should().NotBeNull();
        config.Id.Should().Be(id);
        config.ActivityId.Should().Be(activityId);
        config.WorkflowDefinitionId.Should().Be("test-workflow");
    }

    [Fact]
    public void ActionExecutionResult_Success_ShouldCreateSuccessResult()
    {
        // Arrange
        var message = "Action executed successfully";
        var outputData = new Dictionary<string, object> { ["result"] = "success" };

        // Act
        var result = ActionExecutionResult.Success(message, outputData);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ResultMessage.Should().Be(message);
        result.OutputData.Should().ContainKey("result");
    }

    [Fact]
    public void ActionExecutionResult_Failure_ShouldCreateFailureResult()
    {
        // Arrange
        var errorMessage = "Action execution failed";

        // Act
        var result = ActionExecutionResult.Failed(errorMessage);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
    }
}