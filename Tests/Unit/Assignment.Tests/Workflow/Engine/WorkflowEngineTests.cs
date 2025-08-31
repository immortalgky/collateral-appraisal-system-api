using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Activities.Factories;
using Assignment.Workflow.Engine;
using Assignment.Workflow.Models;
using Assignment.Workflow.Schema;
using Assignment.Workflow.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Assignment.Tests.Workflow.Engine;

/// <summary>
/// Comprehensive unit tests for WorkflowEngine covering all orchestration methods
/// Tests activity execution, workflow coordination, flow control, and lifecycle management
/// </summary>
public class WorkflowEngineTests
{
    private readonly IWorkflowActivityFactory _activityFactory;
    private readonly IFlowControlManager _flowControlManager;
    private readonly IWorkflowLifecycleManager _lifecycleManager;
    private readonly IWorkflowPersistenceService _persistenceService;
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly WorkflowEngine _workflowEngine;
    private readonly IWorkflowActivity _mockActivity;

    public WorkflowEngineTests()
    {
        _activityFactory = Substitute.For<IWorkflowActivityFactory>();
        _flowControlManager = Substitute.For<IFlowControlManager>();
        _lifecycleManager = Substitute.For<IWorkflowLifecycleManager>();
        _persistenceService = Substitute.For<IWorkflowPersistenceService>();
        _logger = Substitute.For<ILogger<WorkflowEngine>>();

        _mockActivity = Substitute.For<IWorkflowActivity>();
        
        _workflowEngine = new WorkflowEngine(
            _activityFactory,
            _flowControlManager,
            _lifecycleManager,
            _persistenceService,
            _logger);
    }

    [Fact]
    public void Constructor_WithValidDependencies_ShouldCreateInstance()
    {
        // Act & Assert
        _workflowEngine.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteActivityAsync_WithValidActivity_ShouldExecuteAndReturnResult()
    {
        // Arrange
        var activityDefinition = CreateTestActivityDefinition("test-activity", "TaskActivity");
        var context = CreateTestActivityContext();
        var expectedResult = ActivityResult.Success(new Dictionary<string, object> { ["completed"] = true });

        _activityFactory.CreateActivity("TaskActivity").Returns(_mockActivity);
        _mockActivity.ExecuteAsync(context, Arg.Any<CancellationToken>()).Returns(expectedResult);

        // Act
        var result = await _workflowEngine.ExecuteActivityAsync(activityDefinition, context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("completed");
        result.OutputData["completed"].Should().Be(true);

        // Verify activity factory was called
        _activityFactory.Received(1).CreateActivity("TaskActivity");
        await _mockActivity.Received(1).ExecuteAsync(context, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteActivityAsync_WithActivityException_ShouldReturnFailedResult()
    {
        // Arrange
        var activityDefinition = CreateTestActivityDefinition("test-activity", "TaskActivity");
        var context = CreateTestActivityContext();

        _activityFactory.CreateActivity("TaskActivity").Returns(_mockActivity);
        _mockActivity.ExecuteAsync(context, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Activity execution failed"));

        // Act
        var result = await _workflowEngine.ExecuteActivityAsync(activityDefinition, context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Activity execution failed");
    }

    [Fact]
    public async Task ResumeActivityAsync_WithValidActivity_ShouldResumeAndReturnResult()
    {
        // Arrange
        var activityDefinition = CreateTestActivityDefinition("test-activity", "TaskActivity");
        var context = CreateTestActivityContext();
        var resumeInput = new Dictionary<string, object> { ["decision"] = "approved" };
        var expectedResult = ActivityResult.Success(new Dictionary<string, object> { ["resumed"] = true });

        _activityFactory.CreateActivity("TaskActivity").Returns(_mockActivity);
        _mockActivity.ResumeAsync(context, resumeInput, Arg.Any<CancellationToken>()).Returns(expectedResult);

        // Act
        var result = await _workflowEngine.ResumeActivityAsync(activityDefinition, context, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("resumed");
        result.OutputData["resumed"].Should().Be(true);

        // Verify activity factory and resume were called
        _activityFactory.Received(1).CreateActivity("TaskActivity");
        await _mockActivity.Received(1).ResumeAsync(context, resumeInput, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeActivityAsync_WithActivityException_ShouldReturnFailedResult()
    {
        // Arrange
        var activityDefinition = CreateTestActivityDefinition("test-activity", "TaskActivity");
        var context = CreateTestActivityContext();
        var resumeInput = new Dictionary<string, object> { ["decision"] = "approved" };

        _activityFactory.CreateActivity("TaskActivity").Returns(_mockActivity);
        _mockActivity.ResumeAsync(context, resumeInput, Arg.Any<CancellationToken>())
            .Throws(new ArgumentException("Invalid resume data"));

        // Act
        var result = await _workflowEngine.ResumeActivityAsync(activityDefinition, context, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Activity resume failed: Invalid resume data");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WithSingleCompletedActivity_ShouldCompleteWorkflow()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstance = CreateTestWorkflowInstance();
        var startActivity = workflowSchema.Activities.First();

        _activityFactory.CreateActivity(startActivity.Type).Returns(_mockActivity);
        _mockActivity.ExecuteAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(ActivityResult.Success(new Dictionary<string, object> { ["completed"] = true }));

        // Setup flow control - no next activity means workflow completes
        _flowControlManager.DetermineNextActivityAsync(
                workflowSchema, startActivity.Id, Arg.Any<ActivityResult>(), 
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));

        // Act
        var result = await _workflowEngine.ExecuteWorkflowAsync(workflowSchema, workflowInstance, startActivity);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        result.WorkflowInstance.Should().Be(workflowInstance);

        // Verify workflow completion was called
        await _lifecycleManager.Received(1).CompleteWorkflowAsync(workflowInstance, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WithMultipleActivities_ShouldExecuteInSequence()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstance = CreateTestWorkflowInstance();
        var startActivity = workflowSchema.Activities.First();
        var secondActivity = workflowSchema.Activities.Skip(1).First();

        _activityFactory.CreateActivity(Arg.Any<string>()).Returns(_mockActivity);
        _mockActivity.ExecuteAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(
                ActivityResult.Success(new Dictionary<string, object> { ["step"] = 1 }),
                ActivityResult.Success(new Dictionary<string, object> { ["step"] = 2 }));

        // Setup flow control sequence
        _flowControlManager.DetermineNextActivityAsync(
                workflowSchema, startActivity.Id, Arg.Any<ActivityResult>(), 
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(secondActivity.Id);
            
        _flowControlManager.DetermineNextActivityAsync(
                workflowSchema, secondActivity.Id, Arg.Any<ActivityResult>(), 
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null)); // End workflow

        // Act
        var result = await _workflowEngine.ExecuteWorkflowAsync(workflowSchema, workflowInstance, startActivity);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        
        // Verify both activities were executed
        await _mockActivity.Received(2).ExecuteAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>());
        
        // Verify workflow progression
        await _lifecycleManager.Received(1).AdvanceWorkflowAsync(
            workflowInstance, secondActivity.Id, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WithPendingActivity_ShouldReturnPendingResult()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstance = CreateTestWorkflowInstance();
        var startActivity = workflowSchema.Activities.First();

        _activityFactory.CreateActivity(startActivity.Type).Returns(_mockActivity);
        _mockActivity.ExecuteAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(ActivityResult.Pending(new Dictionary<string, object> { ["assigned"] = "user@company.com" }));

        // Act
        var result = await _workflowEngine.ExecuteWorkflowAsync(workflowSchema, workflowInstance, startActivity);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Pending);
        result.NextActivityId.Should().Be(startActivity.Id);
        result.RequiresExternalCompletion.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WithFailedActivity_ShouldReturnFailedResult()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstance = CreateTestWorkflowInstance();
        var startActivity = workflowSchema.Activities.First();

        _activityFactory.CreateActivity(startActivity.Type).Returns(_mockActivity);
        _mockActivity.ExecuteAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(ActivityResult.Failed("Activity validation failed", new Dictionary<string, object>()));

        // Act
        var result = await _workflowEngine.ExecuteWorkflowAsync(workflowSchema, workflowInstance, startActivity);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Failed);
        result.ErrorMessage.Should().Be("Activity validation failed");

        // Verify workflow failure was handled
        await _lifecycleManager.Received(1).TransitionWorkflowStateAsync(
            workflowInstance, WorkflowStatus.Failed, "Activity validation failed", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WithException_ShouldReturnFailedResult()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstance = CreateTestWorkflowInstance();
        var startActivity = workflowSchema.Activities.First();

        _activityFactory.CreateActivity(startActivity.Type).Throws(new ArgumentException("Unknown activity type"));

        // Act
        var result = await _workflowEngine.ExecuteWorkflowAsync(workflowSchema, workflowInstance, startActivity);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Failed);
        result.ErrorMessage.Should().Contain("Unknown activity type");

        // Verify workflow failure was handled
        await _lifecycleManager.Received(1).TransitionWorkflowStateAsync(
            workflowInstance, WorkflowStatus.Failed, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeWorkflowExecutionAsync_WithValidResume_ShouldContinueExecution()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstance = CreateTestWorkflowInstance();
        var currentActivity = workflowSchema.Activities.First();
        var resumeInput = new Dictionary<string, object> { ["decision"] = "approved" };

        _activityFactory.CreateActivity(currentActivity.Type).Returns(_mockActivity);
        _mockActivity.ResumeAsync(Arg.Any<ActivityContext>(), resumeInput, Arg.Any<CancellationToken>())
            .Returns(ActivityResult.Success(new Dictionary<string, object> { ["resumed"] = true }));

        // Setup flow control - no next activity means workflow completes
        _flowControlManager.DetermineNextActivityAsync(
                workflowSchema, currentActivity.Id, Arg.Any<ActivityResult>(), 
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));

        // Act
        var result = await _workflowEngine.ResumeWorkflowExecutionAsync(
            workflowSchema, workflowInstance, currentActivity, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        // Verify activity was resumed and workflow completed
        await _mockActivity.Received(1).ResumeAsync(Arg.Any<ActivityContext>(), resumeInput, Arg.Any<CancellationToken>());
        await _lifecycleManager.Received(1).CompleteWorkflowAsync(workflowInstance, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeWorkflowExecutionAsync_WithNextActivity_ShouldContinueToNextActivity()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstance = CreateTestWorkflowInstance();
        var currentActivity = workflowSchema.Activities.First();
        var nextActivity = workflowSchema.Activities.Skip(1).First();
        var resumeInput = new Dictionary<string, object> { ["decision"] = "approved" };

        _activityFactory.CreateActivity(Arg.Any<string>()).Returns(_mockActivity);
        _mockActivity.ResumeAsync(Arg.Any<ActivityContext>(), resumeInput, Arg.Any<CancellationToken>())
            .Returns(ActivityResult.Success(new Dictionary<string, object> { ["resumed"] = true }));
        _mockActivity.ExecuteAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(ActivityResult.Success(new Dictionary<string, object> { ["next_completed"] = true }));

        // Setup flow control sequence
        _flowControlManager.DetermineNextActivityAsync(
                workflowSchema, currentActivity.Id, Arg.Any<ActivityResult>(), 
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(nextActivity.Id);
            
        _flowControlManager.DetermineNextActivityAsync(
                workflowSchema, nextActivity.Id, Arg.Any<ActivityResult>(), 
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null)); // End workflow

        // Act
        var result = await _workflowEngine.ResumeWorkflowExecutionAsync(
            workflowSchema, workflowInstance, currentActivity, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        // Verify workflow progression
        await _lifecycleManager.Received(1).AdvanceWorkflowAsync(
            workflowInstance, nextActivity.Id, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeWorkflowExecutionAsync_WithException_ShouldReturnFailedResult()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstance = CreateTestWorkflowInstance();
        var currentActivity = workflowSchema.Activities.First();
        var resumeInput = new Dictionary<string, object> { ["decision"] = "approved" };

        _activityFactory.CreateActivity(currentActivity.Type).Throws(new InvalidOperationException("Resume failed"));

        // Act
        var result = await _workflowEngine.ResumeWorkflowExecutionAsync(
            workflowSchema, workflowInstance, currentActivity, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Failed);
        result.ErrorMessage.Should().Contain("Resume failed");

        // Verify workflow failure was handled
        await _lifecycleManager.Received(1).TransitionWorkflowStateAsync(
            workflowInstance, WorkflowStatus.Failed, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartWorkflowAsync_WithValidDefinition_ShouldStartWorkflowSuccessfully()
    {
        // Arrange
        var workflowDefinitionId = Guid.NewGuid();
        var instanceName = "Test Workflow Instance";
        var startedBy = "test@company.com";
        var initialVariables = new Dictionary<string, object> { ["test"] = "value" };

        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstance = CreateTestWorkflowInstance();
        var startActivity = workflowSchema.Activities.First();

        _persistenceService.GetWorkflowSchemaAsync(workflowDefinitionId, Arg.Any<CancellationToken>())
            .Returns(workflowSchema);
        _lifecycleManager.InitializeWorkflowAsync(
                workflowDefinitionId, workflowSchema, instanceName, startedBy, 
                initialVariables, Arg.Any<string?>(), Arg.Any<Dictionary<string, RuntimeOverride>?>(), 
                Arg.Any<CancellationToken>())
            .Returns(workflowInstance);
        _flowControlManager.GetStartActivity(workflowSchema).Returns(startActivity);

        // Setup activity execution
        _activityFactory.CreateActivity(startActivity.Type).Returns(_mockActivity);
        _mockActivity.ExecuteAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(ActivityResult.Success(new Dictionary<string, object>()));

        _flowControlManager.DetermineNextActivityAsync(
                workflowSchema, startActivity.Id, Arg.Any<ActivityResult>(), 
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));

        // Act
        var result = await _workflowEngine.StartWorkflowAsync(
            workflowDefinitionId, instanceName, startedBy, initialVariables);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        result.WorkflowInstance.Should().Be(workflowInstance);

        // Verify workflow initialization and execution
        await _persistenceService.Received(1).GetWorkflowSchemaAsync(workflowDefinitionId, Arg.Any<CancellationToken>());
        await _lifecycleManager.Received(1).InitializeWorkflowAsync(
            workflowDefinitionId, workflowSchema, instanceName, startedBy, 
            initialVariables, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartWorkflowAsync_WithMissingWorkflowDefinition_ShouldReturnFailedResult()
    {
        // Arrange
        var workflowDefinitionId = Guid.NewGuid();

        _persistenceService.GetWorkflowSchemaAsync(workflowDefinitionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkflowSchema?>(null));

        // Act
        var result = await _workflowEngine.StartWorkflowAsync(
            workflowDefinitionId, "Test", "test@company.com");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Failed);
        result.ErrorMessage.Should().Contain($"Workflow definition not found: {workflowDefinitionId}");
    }

    [Fact]
    public async Task ResumeWorkflowAsync_WithValidInstance_ShouldResumeSuccessfully()
    {
        // Arrange
        var workflowInstanceId = Guid.NewGuid();
        var activityId = "test-activity";
        var completedBy = "user@company.com";
        var input = new Dictionary<string, object> { ["decision"] = "approved" };

        var workflowInstance = CreateTestWorkflowInstance();
        workflowInstance.SetCurrentActivity(activityId, completedBy);
        
        var workflowSchema = CreateTestWorkflowSchema();
        var currentActivity = workflowSchema.Activities.First(a => a.Id == activityId);

        _persistenceService.GetWorkflowInstanceAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(workflowInstance);
        _persistenceService.GetWorkflowSchemaAsync(workflowInstance.WorkflowDefinitionId, Arg.Any<CancellationToken>())
            .Returns(workflowSchema);

        // Setup activity resume
        _activityFactory.CreateActivity(currentActivity.Type).Returns(_mockActivity);
        _mockActivity.ResumeAsync(Arg.Any<ActivityContext>(), input, Arg.Any<CancellationToken>())
            .Returns(ActivityResult.Success(new Dictionary<string, object>()));

        _flowControlManager.DetermineNextActivityAsync(
                workflowSchema, activityId, Arg.Any<ActivityResult>(), 
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));

        // Act
        var result = await _workflowEngine.ResumeWorkflowAsync(
            workflowInstanceId, activityId, completedBy, input);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        // Verify persistence service calls
        await _persistenceService.Received(1).GetWorkflowInstanceAsync(workflowInstanceId, Arg.Any<CancellationToken>());
        await _persistenceService.Received(1).GetWorkflowSchemaAsync(workflowInstance.WorkflowDefinitionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeWorkflowAsync_WithMissingWorkflowInstance_ShouldReturnFailedResult()
    {
        // Arrange
        var workflowInstanceId = Guid.NewGuid();

        _persistenceService.GetWorkflowInstanceAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkflowInstance?>(null));

        // Act
        var result = await _workflowEngine.ResumeWorkflowAsync(
            workflowInstanceId, "test-activity", "user@company.com");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Failed);
        result.ErrorMessage.Should().Contain($"Workflow instance not found: {workflowInstanceId}");
    }

    [Fact]
    public async Task ResumeWorkflowAsync_WithWrongCurrentActivity_ShouldReturnFailedResult()
    {
        // Arrange
        var workflowInstanceId = Guid.NewGuid();
        var activityId = "test-activity";
        var differentActivityId = "different-activity";

        var workflowInstance = CreateTestWorkflowInstance();
        workflowInstance.SetCurrentActivity(differentActivityId, "user@company.com");
        
        var workflowSchema = CreateTestWorkflowSchema();

        _persistenceService.GetWorkflowInstanceAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(workflowInstance);
        _persistenceService.GetWorkflowSchemaAsync(workflowInstance.WorkflowDefinitionId, Arg.Any<CancellationToken>())
            .Returns(workflowSchema);

        // Act
        var result = await _workflowEngine.ResumeWorkflowAsync(
            workflowInstanceId, activityId, "user@company.com");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Failed);
        result.ErrorMessage.Should().Contain($"Activity {activityId} is not the current activity");
    }

    [Fact]
    public async Task ValidateWorkflowDefinitionAsync_WithValidWorkflow_ShouldReturnTrue()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();

        _flowControlManager.ValidateWorkflowTransitions(workflowSchema).Returns(true);
        _activityFactory.CreateActivity(Arg.Any<string>()).Returns(_mockActivity);
        _mockActivity.ValidateAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(Assignment.Workflow.Activities.Core.ValidationResult.Success());

        // Act
        var result = await _workflowEngine.ValidateWorkflowDefinitionAsync(workflowSchema);

        // Assert
        result.Should().BeTrue();

        // Verify validation calls
        _flowControlManager.Received(1).ValidateWorkflowTransitions(workflowSchema);
        await _mockActivity.Received(workflowSchema.Activities.Count).ValidateAsync(
            Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateWorkflowDefinitionAsync_WithEmptyName_ShouldReturnFalse()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        workflowSchema.Name = "";

        // Act
        var result = await _workflowEngine.ValidateWorkflowDefinitionAsync(workflowSchema);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateWorkflowDefinitionAsync_WithNoActivities_ShouldReturnFalse()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        workflowSchema.Activities = new List<ActivityDefinition>();

        // Act
        var result = await _workflowEngine.ValidateWorkflowDefinitionAsync(workflowSchema);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateWorkflowDefinitionAsync_WithInvalidTransitions_ShouldReturnFalse()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        _flowControlManager.ValidateWorkflowTransitions(workflowSchema).Returns(false);

        // Act
        var result = await _workflowEngine.ValidateWorkflowDefinitionAsync(workflowSchema);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateWorkflowDefinitionAsync_WithInvalidActivity_ShouldReturnFalse()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();

        _flowControlManager.ValidateWorkflowTransitions(workflowSchema).Returns(true);
        _activityFactory.CreateActivity(Arg.Any<string>()).Returns(_mockActivity);
        _mockActivity.ValidateAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(Assignment.Workflow.Activities.Core.ValidationResult.Failure("Activity validation failed"));

        // Act
        var result = await _workflowEngine.ValidateWorkflowDefinitionAsync(workflowSchema);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateWorkflowDefinitionAsync_WithException_ShouldReturnFalse()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();

        _flowControlManager.ValidateWorkflowTransitions(workflowSchema).Throws(new ArgumentException("Invalid transitions"));

        // Act
        var result = await _workflowEngine.ValidateWorkflowDefinitionAsync(workflowSchema);

        // Assert
        result.Should().BeFalse();
    }

    private ActivityDefinition CreateTestActivityDefinition(string id, string type)
    {
        return new ActivityDefinition
        {
            Id = id,
            Type = type,
            Name = $"Test {type}",
            Properties = new Dictionary<string, object>()
        };
    }

    private ActivityContext CreateTestActivityContext()
    {
        var workflowInstance = CreateTestWorkflowInstance();
        
        return new ActivityContext
        {
            ActivityId = "test-activity",
            WorkflowInstanceId = workflowInstance.Id,
            WorkflowInstance = workflowInstance,
            Variables = new Dictionary<string, object>(),
            Properties = new Dictionary<string, object>(),
            CurrentAssignee = "test@company.com"
        };
    }

    private WorkflowSchema CreateTestWorkflowSchema()
    {
        return new WorkflowSchema
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Workflow Schema",
            Description = "Test workflow for unit testing",
            Activities = new List<ActivityDefinition>
            {
                CreateTestActivityDefinition("start", "StartActivity"),
                CreateTestActivityDefinition("test-activity", "TaskActivity"),
                CreateTestActivityDefinition("end", "EndActivity")
            },
            Transitions = new List<TransitionDefinition>
            {
                new() { Id = "start-to-test", From = "start", To = "test-activity" },
                new() { Id = "test-to-end", From = "test-activity", To = "end" }
            },
            Variables = new Dictionary<string, object>(),
            Metadata = new WorkflowMetadata
            {
                Version = "1.0.0",
                Author = "Unit Test",
                CreatedDate = DateTime.UtcNow
            }
        };
    }

    private WorkflowInstance CreateTestWorkflowInstance()
    {
        return WorkflowInstance.Create(
            Guid.NewGuid(),
            "Test Workflow Instance",
            null,
            "test@company.com",
            new Dictionary<string, object> { ["test"] = "value" });
    }
}