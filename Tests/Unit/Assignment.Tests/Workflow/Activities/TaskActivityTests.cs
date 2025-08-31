using Assignment.AssigneeSelection.Core;
using Assignment.AssigneeSelection.Engine;
using Assignment.AssigneeSelection.Factories;
using Assignment.AssigneeSelection.Services;
using Assignment.Services.Configuration;
using Assignment.Services.Configuration.Models;
using Assignment.Workflow.Activities;
using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Actions.Core;
using Assignment.Workflow.Models;
using Assignment.Workflow.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Reflection;
using Xunit;

namespace Assignment.Tests.Workflow.Activities;

/// <summary>
/// Comprehensive unit tests for TaskActivity covering all execution scenarios
/// Tests assignment logic, lifecycle events, custom services, and error handling
/// </summary>
public class TaskActivityTests
{
    private readonly IAssigneeSelectorFactory _assigneeSelectorFactory;
    private readonly ICascadingAssignmentEngine _cascadingEngine;
    private readonly ITaskConfigurationService _configurationService;
    private readonly ICustomAssignmentServiceFactory _customAssignmentServiceFactory;
    private readonly IWorkflowActionExecutor _actionExecutor;
    private readonly IWorkflowAuditService _auditService;
    private readonly ILogger<TaskActivity> _logger;
    private readonly TaskActivity _taskActivity;
    private readonly IAssigneeSelector _mockPreviousOwnerSelector;
    private readonly IAssigneeSelector _mockManualSelector;

    public TaskActivityTests()
    {
        _assigneeSelectorFactory = Substitute.For<IAssigneeSelectorFactory>();
        _cascadingEngine = Substitute.For<ICascadingAssignmentEngine>();
        _configurationService = Substitute.For<ITaskConfigurationService>();
        _customAssignmentServiceFactory = Substitute.For<ICustomAssignmentServiceFactory>();
        _actionExecutor = Substitute.For<IWorkflowActionExecutor>();
        _auditService = Substitute.For<IWorkflowAuditService>();
        _logger = Substitute.For<ILogger<TaskActivity>>();

        // Setup mock selectors
        _mockPreviousOwnerSelector = Substitute.For<IAssigneeSelector>();
        _mockManualSelector = Substitute.For<IAssigneeSelector>();
        
        _assigneeSelectorFactory.GetSelector(AssigneeSelectionStrategy.PreviousOwner)
            .Returns(_mockPreviousOwnerSelector);
        _assigneeSelectorFactory.GetSelector(AssigneeSelectionStrategy.Manual)
            .Returns(_mockManualSelector);

        _taskActivity = new TaskActivity(
            _assigneeSelectorFactory,
            _cascadingEngine,
            _configurationService,
            _customAssignmentServiceFactory,
            _actionExecutor,
            _auditService,
            _logger);
    }

    [Fact]
    public void Constructor_WithValidDependencies_ShouldCreateInstance()
    {
        // Act & Assert
        _taskActivity.Should().NotBeNull();
        _taskActivity.ActivityType.Should().Be("TaskActivity");
        _taskActivity.Name.Should().Be("Task Activity");
        _taskActivity.Description.Should().Be("Assigns a task to a user or role for completion using various strategies");
    }

    [Fact]
    public async Task ExecuteActivityAsync_WithPreviousOwner_ShouldAssignToPreviousHandlerEarly()
    {
        // Arrange
        var context = CreateTestActivityContext();
        
        // Setup previous owner found
        _mockPreviousOwnerSelector.SelectAssigneeAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Success("previous-owner@company.com", 
                new Dictionary<string, object> { ["Strategy"] = "PreviousOwner" }));

        // Act
        var result = await _taskActivity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Pending);
        result.OutputData.Should().ContainKey("assignedTo");
        result.OutputData["assignedTo"].Should().Be("previous-owner@company.com");
        result.OutputData["isPreviousHandler"].Should().Be(true);
        result.OutputData[$"{NormalizeActivityId(context.ActivityId)}_assignedTo"].Should().Be("previous-owner@company.com");

        // Verify audit service was called
        await _auditService.Received(1).LogAssignmentChangeAsync(
            Arg.Any<ActivityContext>(),
            null, // no previous assignee
            "previous-owner@company.com",
            "Found and assigned to previous handler",
            AssignmentChangeType.InitialAssignment,
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteActivityAsync_WithNoPreviousOwner_ShouldUseCascadingEngine()
    {
        // Arrange
        var context = CreateTestActivityContext();
        
        // Setup no previous owner
        _mockPreviousOwnerSelector.SelectAssigneeAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Failure("No previous owner found"));

        // Setup successful cascading assignment
        _cascadingEngine.ExecuteAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Success("new-assignee@company.com", 
                new Dictionary<string, object> { ["Strategy"] = "RoundRobin" }));

        // Act
        var result = await _taskActivity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Pending);
        result.OutputData.Should().ContainKey("assignedTo");
        result.OutputData["assignedTo"].Should().Be("new-assignee@company.com");
        result.OutputData["isPreviousHandler"].Should().Be(false);
        
        // Verify cascading engine was called
        await _cascadingEngine.Received(1).ExecuteAsync(
            Arg.Is<AssignmentContext>(ac => ac.AssignmentStrategies.Contains("RoundRobin")), 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteActivityAsync_WithCustomAssignmentService_ShouldUseCustomServiceFirst()
    {
        // Arrange
        var context = CreateTestActivityContext(new Dictionary<string, object>
        {
            ["customAssignmentService"] = "SpecialAssignmentService"
        });

        var mockCustomService = Substitute.For<ICustomAssignmentService>();
        mockCustomService.GetAssignmentContextAsync(
                Arg.Any<string>(), 
                Arg.Any<string>(), 
                Arg.Any<Dictionary<string, object>>(), 
                Arg.Any<CancellationToken>())
            .Returns(new CustomAssignmentResult
            {
                UseCustomAssignment = true,
                SpecificAssignee = "custom-assignee@company.com",
                Reason = "Custom service assignment",
                Metadata = new Dictionary<string, object> { ["CustomService"] = "SpecialAssignmentService" }
            });

        _customAssignmentServiceFactory.GetService("SpecialAssignmentService")
            .Returns(mockCustomService);

        // Act
        var result = await _taskActivity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Pending);
        result.OutputData.Should().ContainKey("assignedTo");
        result.OutputData["assignedTo"].Should().Be("custom-assignee@company.com");
        result.OutputData["assignmentReason"].Should().Be("Custom service assignment");

        // Verify custom service was called first (previous owner selector should not be called)
        await _mockPreviousOwnerSelector.DidNotReceive().SelectAssigneeAsync(
            Arg.Any<AssignmentContext>(), 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteActivityAsync_WithRuntimeOverride_ShouldUseRuntimeAssignment()
    {
        // Arrange
        var context = CreateTestActivityContext();
        context = new ActivityContext
        {
            ActivityId = context.ActivityId,
            WorkflowInstanceId = context.WorkflowInstanceId,
            WorkflowInstance = context.WorkflowInstance,
            Variables = context.Variables,
            Properties = context.Properties,
            CurrentAssignee = context.CurrentAssignee,
            RuntimeOverrides = new RuntimeOverride
            {
                RuntimeAssignee = "override-assignee@company.com",
                OverrideBy = "supervisor@company.com",
                OverrideReason = "Urgent assignment override"
            }
        };

        // Act
        var result = await _taskActivity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Pending);
        result.OutputData.Should().ContainKey("assignedTo");
        result.OutputData["assignedTo"].Should().Be("override-assignee@company.com");
        result.OutputData["assignmentReason"].Should().Be("Urgent assignment override");

        // Verify previous owner selector was not called due to runtime override
        await _mockPreviousOwnerSelector.DidNotReceive().SelectAssigneeAsync(
            Arg.Any<AssignmentContext>(), 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteActivityAsync_WithExternalConfiguration_ShouldUseExternalStrategies()
    {
        // Arrange
        var context = CreateTestActivityContext();
        
        var externalConfig = new TaskAssignmentConfigurationDto
        {
            PrimaryStrategies = new List<string> { "WorkloadBased", "Supervisor" },
            SpecificAssignee = "config-assignee@company.com",
            AssigneeGroup = "config-group"
        };

        _configurationService.GetConfigurationAsync(
                context.ActivityId, 
                Arg.Any<string>(), 
                Arg.Any<CancellationToken>())
            .Returns(externalConfig);

        // Setup no previous owner
        _mockPreviousOwnerSelector.SelectAssigneeAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Failure("No previous owner"));

        // Setup successful cascading assignment
        _cascadingEngine.ExecuteAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Success("config-based-assignee@company.com", 
                new Dictionary<string, object> { ["Strategy"] = "WorkloadBased" }));

        // Act
        var result = await _taskActivity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Pending);
        
        // Verify cascading engine was called with external config strategies
        await _cascadingEngine.Received(1).ExecuteAsync(
            Arg.Is<AssignmentContext>(ac => 
                ac.AssignmentStrategies.Contains("WorkloadBased") && 
                ac.AssignmentStrategies.Contains("Supervisor") &&
                ac.UserCode == "config-assignee@company.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteActivityAsync_WithAssignmentFailure_ShouldReturnFailedResult()
    {
        // Arrange
        var context = CreateTestActivityContext();
        
        // Setup no previous owner
        _mockPreviousOwnerSelector.SelectAssigneeAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Failure("No previous owner"));

        // Setup failed cascading assignment
        _cascadingEngine.ExecuteAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Failure("All assignment strategies failed"));

        // Act
        var result = await _taskActivity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Pending); // Still pending but with assignment failure
        result.OutputData[$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"].Should().Be("assignment_failed");
        result.OutputData["assignmentError"].Should().Be("All assignment strategies failed");
    }

    [Fact]
    public async Task ExecuteActivityAsync_WithAdminPoolFallback_ShouldUseAdminPoolWhenConfigured()
    {
        // Arrange
        var context = CreateTestActivityContext();
        
        var externalConfig = new TaskAssignmentConfigurationDto
        {
            PrimaryStrategies = new List<string> { "RoundRobin" },
            EscalateToAdminPool = true,
            AdminPoolId = "ADMIN_ESCALATION_POOL"
        };

        _configurationService.GetConfigurationAsync(
                context.ActivityId, 
                Arg.Any<string>(), 
                Arg.Any<CancellationToken>())
            .Returns(externalConfig);

        // Setup no previous owner
        _mockPreviousOwnerSelector.SelectAssigneeAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Failure("No previous owner"));

        // Setup failed primary assignment
        _cascadingEngine.ExecuteAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Failure("Primary strategies failed"));

        // Act
        var result = await _taskActivity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Pending);
        result.OutputData.Should().ContainKey("assignedTo");
        result.OutputData["assignedTo"].Should().Be("ADMIN_ESCALATION_POOL");
        result.OutputData["assignmentMetadata"].As<Dictionary<string, object>>()["SelectionStrategy"].Should().Be("AdminPoolFallback");
    }

    [Fact]
    public async Task ExecuteActivityAsync_WithLifecycleActions_ShouldExecuteOnStartAndOnCompleteActions()
    {
        // Arrange
        var context = CreateTestActivityContext();
        
        // Setup no previous owner and successful assignment
        _mockPreviousOwnerSelector.SelectAssigneeAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Failure("No previous owner"));

        _cascadingEngine.ExecuteAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(AssigneeSelectionResult.Success("assignee@company.com", 
                new Dictionary<string, object>()));

        // Setup action executor
        _actionExecutor.ExecuteActionsAsync(
                Arg.Any<ActivityContext>(), 
                Arg.Any<List<WorkflowActionConfiguration>>(), 
                Arg.Any<ActivityLifecycleEvent>(), 
                Arg.Any<CancellationToken>())
            .Returns(new ActionBatchExecutionResult
            {
                IsSuccess = true,
                SuccessfulActions = 1,
                FailedActions = 0,
                SkippedActions = 0
            });

        // Act
        var result = await _taskActivity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Pending);
        
        // Verify lifecycle actions were executed
        await _actionExecutor.Received().ExecuteActionsAsync(
            Arg.Any<ActivityContext>(), 
            Arg.Any<List<WorkflowActionConfiguration>>(), 
            ActivityLifecycleEvent.OnStart, 
            Arg.Any<CancellationToken>());
            
        await _actionExecutor.Received().ExecuteActionsAsync(
            Arg.Any<ActivityContext>(), 
            Arg.Any<List<WorkflowActionConfiguration>>(), 
            ActivityLifecycleEvent.OnComplete, 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteActivityAsync_WithException_ShouldReturnFailedResultAndExecuteErrorActions()
    {
        // Arrange
        var context = CreateTestActivityContext();
        
        // Setup exception in previous owner selector
        _mockPreviousOwnerSelector.SelectAssigneeAsync(
                Arg.Any<AssignmentContext>(), 
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AssigneeSelectionResult>(new InvalidOperationException("Database connection failed")));

        // Setup action executor for error actions
        _actionExecutor.ExecuteActionsAsync(
                Arg.Any<ActivityContext>(), 
                Arg.Any<List<WorkflowActionConfiguration>>(), 
                ActivityLifecycleEvent.OnError, 
                Arg.Any<CancellationToken>())
            .Returns(new ActionBatchExecutionResult { IsSuccess = true });

        // Act
        var result = await _taskActivity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Task activity failed: Database connection failed");
        result.OutputData[$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"].Should().Be("failed");
        result.OutputData[$"{NormalizeActivityId(context.ActivityId)}_error"].Should().Be("Database connection failed");

        // Verify error lifecycle actions were executed
        await _actionExecutor.Received().ExecuteActionsAsync(
            Arg.Any<ActivityContext>(), 
            Arg.Any<List<WorkflowActionConfiguration>>(), 
            ActivityLifecycleEvent.OnError, 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeActivityAsync_WithDecisionTaken_ShouldProcessInputAndReturnSuccess()
    {
        // Arrange
        var context = CreateTestActivityContext();
        var resumeInput = new Dictionary<string, object>
        {
            ["decisionTaken"] = "approved",
            ["approvalAmount"] = 350000,
            ["notes"] = "Application approved after review",
            ["completedBy"] = "reviewer@company.com"
        };

        // Act
        var result = await _taskActivity.ResumeAsync(context, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("decision");
        result.OutputData["decision"].Should().Be("approved");
        result.OutputData[$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"].Should().Be("approved");
        result.OutputData[$"{NormalizeActivityId(context.ActivityId)}_approvalAmount"].Should().Be(350000);
        result.OutputData[$"{NormalizeActivityId(context.ActivityId)}_notes"].Should().Be("Application approved after review");
    }

    [Fact]
    public async Task ResumeActivityAsync_WithInputMappings_ShouldMapInputsCorrectly()
    {
        // Arrange
        var context = CreateTestActivityContext(new Dictionary<string, object>
        {
            ["inputMappings"] = new Dictionary<string, string>
            {
                ["loanAmount"] = "loan_amount_variable",
                ["propertyValue"] = "property_value_variable"
            }
        });
        
        context.Variables["loan_amount_variable"] = 300000;
        context.Variables["property_value_variable"] = 400000;
        
        var resumeInput = new Dictionary<string, object>
        {
            ["decisionTaken"] = "completed",
            ["loanAmount"] = 350000, // This should be mapped
            ["propertyValue"] = 450000 // This should be mapped
        };

        // Act
        var result = await _taskActivity.ResumeAsync(context, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("loan_amount_variable");
        result.OutputData["loan_amount_variable"].Should().Be(350000);
        result.OutputData.Should().ContainKey("property_value_variable");
        result.OutputData["property_value_variable"].Should().Be(450000);
    }

    [Fact]
    public async Task ResumeActivityAsync_WithOutputMappings_ShouldMapOutputsCorrectly()
    {
        // Arrange
        var context = CreateTestActivityContext(new Dictionary<string, object>
        {
            ["outputMappings"] = new Dictionary<string, string>
            {
                ["finalDecision"] = "workflow_final_decision",
                ["reviewNotes"] = "{activityId}_review_notes"
            }
        });
        
        var resumeInput = new Dictionary<string, object>
        {
            ["decisionTaken"] = "approved",
            ["finalDecision"] = "APPROVED_WITH_CONDITIONS",
            ["reviewNotes"] = "Detailed review completed successfully"
        };

        // Act
        var result = await _taskActivity.ResumeAsync(context, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("workflow_final_decision");
        result.OutputData["workflow_final_decision"].Should().Be("APPROVED_WITH_CONDITIONS");
        result.OutputData.Should().ContainKey($"{context.ActivityId}_review_notes");
        result.OutputData[$"{context.ActivityId}_review_notes"].Should().Be("Detailed review completed successfully");
    }

    [Fact]
    public async Task ResumeActivityAsync_WithDecisionConditions_ShouldEvaluateConditionsCorrectly()
    {
        // Arrange
        var context = CreateTestActivityContext(new Dictionary<string, object>
        {
            ["decisionConditions"] = new Dictionary<string, string>
            {
                ["approved"] = "approvalAmount >= 100000 && risk_score < 5",
                ["rejected"] = "risk_score >= 8",
                ["review"] = "approvalAmount > 500000"
            }
        });
        
        context.Variables["risk_score"] = 3;
        
        var resumeInput = new Dictionary<string, object>
        {
            ["approvalAmount"] = 250000,
            ["decisionTaken"] = "completed"
        };

        // Act
        var result = await _taskActivity.ResumeAsync(context, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("decision");
        result.OutputData["decision"].Should().Be("approved"); // First matching condition
    }

    [Fact]
    public async Task ResumeActivityAsync_WithException_ShouldReturnFailedResultAndExecuteErrorActions()
    {
        // Arrange
        var context = CreateTestActivityContext(new Dictionary<string, object>
        {
            ["inputMappings"] = "invalid_mapping_format" // This will cause an exception
        });
        
        var resumeInput = new Dictionary<string, object>
        {
            ["decisionTaken"] = "completed"
        };

        // Setup action executor for error actions
        _actionExecutor.ExecuteActionsAsync(
                Arg.Any<ActivityContext>(), 
                Arg.Any<List<WorkflowActionConfiguration>>(), 
                ActivityLifecycleEvent.OnError, 
                Arg.Any<CancellationToken>())
            .Returns(new ActionBatchExecutionResult { IsSuccess = true });

        // Act
        var result = await _taskActivity.ResumeAsync(context, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("TaskActivity resume failed");
        result.OutputData[$"{NormalizeActivityId(context.ActivityId)}_decisionTaken"].Should().Be("resume_failed");

        // Verify error lifecycle actions were executed
        await _actionExecutor.Received().ExecuteActionsAsync(
            Arg.Any<ActivityContext>(), 
            Arg.Any<List<WorkflowActionConfiguration>>(), 
            ActivityLifecycleEvent.OnError, 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_WithValidConfiguration_ShouldReturnSuccess()
    {
        // Arrange
        var context = CreateTestActivityContext(new Dictionary<string, object>
        {
            ["assigneeRole"] = "Appraiser",
            ["assignmentStrategies"] = new List<string> { "RoundRobin", "WorkloadBased" }
        });

        // Act
        var result = await _taskActivity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithMissingAssigneeRole_ShouldReturnValidationError()
    {
        // Arrange
        var context = CreateTestActivityContext(); // No assigneeRole

        // Act
        var result = await _taskActivity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("AssigneeRole is required for TaskActivity");
    }

    [Fact]
    public async Task ValidateAsync_WithManualStrategyButNoAssignee_ShouldReturnValidationError()
    {
        // Arrange
        var context = CreateTestActivityContext(new Dictionary<string, object>
        {
            ["assigneeRole"] = "Appraiser",
            ["assignmentStrategies"] = new List<string> { "Manual" }
            // No assignee or assignee_group
        });

        // Act
        var result = await _taskActivity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("For Manual assignment strategy, either 'assignee' or 'assignee_group' must be specified");
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidAssignmentStrategy_ShouldReturnValidationError()
    {
        // Arrange
        var context = CreateTestActivityContext(new Dictionary<string, object>
        {
            ["assigneeRole"] = "Appraiser",
            ["assignmentStrategies"] = new List<string> { "InvalidStrategy" }
        });

        // Act
        var result = await _taskActivity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Invalid assignment strategy: InvalidStrategy");
    }

    [Fact]
    public void CreateActivityExecution_WithContext_ShouldCreateExecution()
    {
        // Arrange
        var context = CreateTestActivityContext();

        // Act - Use reflection to access protected method
        var methodInfo = typeof(TaskActivity).GetMethod("CreateActivityExecution", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var execution = (WorkflowActivityExecution)methodInfo!.Invoke(_taskActivity, new object[] { context })!;

        // Assert
        execution.Should().NotBeNull();
        execution.WorkflowInstanceId.Should().Be(context.WorkflowInstance.Id);
        execution.ActivityId.Should().Be(context.ActivityId);
        execution.ActivityName.Should().Be("Task Activity");
        execution.ActivityType.Should().Be("TaskActivity");
        execution.AssignedTo.Should().Be(context.CurrentAssignee);
    }

    private ActivityContext CreateTestActivityContext(Dictionary<string, object>? additionalProperties = null)
    {
        var workflowInstance = WorkflowInstance.Create(
            Guid.NewGuid(),
            "Test Workflow",
            null,
            "test@company.com",
            new Dictionary<string, object>
            {
                ["property_value"] = 450000,
                ["loan_amount"] = 360000
            });

        var properties = new Dictionary<string, object>
        {
            ["assigneeRole"] = "Appraiser",
            ["assignmentStrategies"] = new List<string> { "RoundRobin" },
            ["activityName"] = "Property Evaluation",
            ["workflowDefinitionId"] = workflowInstance.WorkflowDefinitionId.ToString()
        };

        if (additionalProperties != null)
        {
            foreach (var kvp in additionalProperties)
            {
                properties[kvp.Key] = kvp.Value;
            }
        }

        return new ActivityContext
        {
            ActivityId = "property-evaluation",
            WorkflowInstanceId = workflowInstance.Id,
            WorkflowInstance = workflowInstance,
            Variables = new Dictionary<string, object>(workflowInstance.Variables),
            Properties = properties,
            CurrentAssignee = "current@company.com"
        };
    }

    private string NormalizeActivityId(string activityId)
    {
        return activityId.Replace("-", "_").Replace(" ", "_");
    }
}