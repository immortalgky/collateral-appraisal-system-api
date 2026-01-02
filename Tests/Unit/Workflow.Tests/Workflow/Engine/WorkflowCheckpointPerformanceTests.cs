using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Activities.Factories;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Workflow.Workflow.Engine.Core;
using Xunit;
using System.Diagnostics;
using WorkflowActivityContext = Workflow.Workflow.Activities.Core.ActivityContext;

namespace Workflow.Tests.Workflow.Engine;

/// <summary>
/// Performance and integrity tests for workflow checkpoint mechanisms
/// Tests persistence performance, concurrent scenarios, and checkpoint recovery
/// </summary>
public class WorkflowCheckpointPerformanceTests
{
    private readonly IWorkflowActivityFactory _activityFactory;
    private readonly IFlowControlManager _flowControlManager;
    private readonly IWorkflowLifecycleManager _lifecycleManager;
    private readonly IWorkflowPersistenceService _persistenceService;
    private readonly IWorkflowStateManager _stateManager;
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly WorkflowEngine _workflowEngine;
    private readonly IWorkflowActivity _mockActivity;

    public WorkflowCheckpointPerformanceTests()
    {
        _activityFactory = Substitute.For<IWorkflowActivityFactory>();
        _flowControlManager = Substitute.For<IFlowControlManager>();
        _lifecycleManager = Substitute.For<IWorkflowLifecycleManager>();
        _persistenceService = Substitute.For<IWorkflowPersistenceService>();
        _stateManager = Substitute.For<IWorkflowStateManager>();
        _logger = Substitute.For<ILogger<WorkflowEngine>>();
        _mockActivity = Substitute.For<IWorkflowActivity>();

        _workflowEngine = new WorkflowEngine(
            _activityFactory,
            _flowControlManager,
            _lifecycleManager,
            _persistenceService,
            _stateManager,
            _logger);
    }

    /// <summary>
    /// Tests that checkpoint operations don't significantly degrade performance
    /// Verifies the reduction in database writes compared to old automatic persistence
    /// </summary>
    [Fact]
    public async Task CheckpointPerformance_ShouldReduceDatabaseWrites()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstance = CreateTestWorkflowInstance();
        var startActivity = CreateTestActivityDefinition("start", "StartActivity");

        _activityFactory.CreateActivity("StartActivity").Returns(_mockActivity);

        var executionCount = 0;
        var checkpointCount = 0;

        // Simulate multi-step workflow execution
        _mockActivity.ExecuteAsync(Arg.Any<WorkflowActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                executionCount++;
                return ActivityResult.Success(new Dictionary<string, object> 
                {
                    ["step"] = executionCount,
                    ["timestamp"] = DateTime.UtcNow
                });
            });

        // Track checkpoint calls
        _stateManager.CreateCheckpointAsync(Arg.Any<WorkflowInstance>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                checkpointCount++;
                return Task.CompletedTask;
            });

        // Simulate multi-step flow: start -> middle -> end
        var callSequence = 0;
        _flowControlManager.DetermineNextActivityAsync(
                Arg.Any<WorkflowSchema>(), Arg.Any<string>(), Arg.Any<ActivityResult>(),
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callSequence++;
                return callSequence switch
                {
                    1 => "middle-activity", // After start
                    2 => "end-activity",    // After middle  
                    _ => null               // After end - complete workflow
                };
            });

        _lifecycleManager.CompleteWorkflowAsync(Arg.Any<WorkflowInstance>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _workflowEngine.ExecuteWorkflowAsync(
            workflowSchema, workflowInstance, startActivity);

        stopwatch.Stop();

        // Assert
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        // Verify performance characteristics
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Workflow execution should be fast");

        // Verify strategic checkpointing - should have only 1 checkpoint at completion
        // Old system would have had 3+ writes (start transition, middle transition, completion)
        // New system should have just 1 strategic checkpoint
        checkpointCount.Should().Be(1, "Should only checkpoint at workflow completion");

        await _stateManager.Received(1).CreateCheckpointAsync(
            workflowInstance, "Workflow completed successfully", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests partial checkpoint failure handling - ensuring workflow integrity
    /// </summary>
    [Fact]
    public async Task PartialCheckpointFailure_ShouldMaintainWorkflowIntegrity()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstance = CreateTestWorkflowInstance();
        var startActivity = CreateTestActivityDefinition("start", "StartActivity");

        _activityFactory.CreateActivity("StartActivity").Returns(_mockActivity);

        _mockActivity.ExecuteAsync(Arg.Any<WorkflowActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(ActivityResult.Failed("Critical validation error"));

        // Simulate state transition succeeding but checkpoint failing
        _lifecycleManager.TransitionWorkflowStateAsync(
                Arg.Any<WorkflowInstance>(), WorkflowStatus.Failed, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // First checkpoint fails (e.g., DB connection lost during write)
        var checkpointCallCount = 0;
        _stateManager.CreateCheckpointAsync(Arg.Any<WorkflowInstance>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                checkpointCallCount++;
                if (checkpointCallCount == 1)
                    throw new InvalidOperationException("Database connection lost"); // First attempt fails
                return Task.CompletedTask;      // Subsequent attempts succeed
            });

        // Act
        var result = await _workflowEngine.ExecuteWorkflowAsync(
            workflowSchema, workflowInstance, startActivity);

        // Assert
        result.Status.Should().Be(WorkflowExecutionStatus.Failed);
        result.ErrorMessage.Should().Be("Critical validation error");

        // Verify workflow state was transitioned even though checkpoint failed
        await _lifecycleManager.Received(1).TransitionWorkflowStateAsync(
            workflowInstance, WorkflowStatus.Failed, 
            "Critical validation error", Arg.Any<CancellationToken>());

        // Verify checkpoint was attempted (even though it failed)
        await _stateManager.Received(1).CreateCheckpointAsync(
            workflowInstance, "Activity failed - workflow terminated", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests concurrent workflow checkpoint scenarios
    /// </summary>
    [Fact]
    public async Task ConcurrentWorkflowCheckpoints_ShouldNotInterfere()
    {
        // Arrange
        var workflowSchema = CreateTestWorkflowSchema();
        var workflowInstances = new[]
        {
            CreateTestWorkflowInstance(),
            CreateTestWorkflowInstance(),
            CreateTestWorkflowInstance()
        };
        var startActivity = CreateTestActivityDefinition("start", "StartActivity");

        _activityFactory.CreateActivity("StartActivity").Returns(_mockActivity);

        _mockActivity.ExecuteAsync(Arg.Any<WorkflowActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(ActivityResult.Success(new Dictionary<string, object>()));

        _flowControlManager.DetermineNextActivityAsync(
                Arg.Any<WorkflowSchema>(), Arg.Any<string>(), Arg.Any<ActivityResult>(),
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns((string?)null); // All workflows complete

        _lifecycleManager.CompleteWorkflowAsync(Arg.Any<WorkflowInstance>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var checkpointCallTimes = new List<DateTime>();
        _stateManager.CreateCheckpointAsync(Arg.Any<WorkflowInstance>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                lock (checkpointCallTimes)
                {
                    checkpointCallTimes.Add(DateTime.UtcNow);
                }
                // Simulate some checkpoint processing time
                Thread.Sleep(50);
                return Task.CompletedTask;
            });

        // Act - Execute workflows concurrently
        var tasks = workflowInstances.Select(instance =>
            _workflowEngine.ExecuteWorkflowAsync(workflowSchema, instance, startActivity)
        ).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Status.Should().Be(WorkflowExecutionStatus.Completed));

        // Verify all checkpoints were called
        checkpointCallTimes.Should().HaveCount(3);

        // Verify checkpoints happened concurrently (within reasonable time window)
        var timeSpan = checkpointCallTimes.Max() - checkpointCallTimes.Min();
        timeSpan.Should().BeLessThan(TimeSpan.FromSeconds(1), "Concurrent checkpoints should not be serialized");

        await _stateManager.Received(3).CreateCheckpointAsync(
            Arg.Any<WorkflowInstance>(), "Workflow completed successfully", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests recovery from incomplete checkpoint state
    /// </summary>
    [Fact]
    public async Task RecoveryFromIncompleteCheckpoint_ShouldResumeCorrectly()
    {
        // Arrange
        var workflowInstanceId = Guid.NewGuid();
        var workflowInstance = CreateTestWorkflowInstance();
        workflowInstance.SetCurrentActivity("suspended-activity");
        workflowInstance.UpdateStatus(WorkflowStatus.Suspended, "Checkpoint interrupted");

        var workflowSchema = CreateTestWorkflowSchema();

        _persistenceService.GetWorkflowInstanceAsync(workflowInstanceId, Arg.Any<CancellationToken>())
            .Returns(workflowInstance);

        _persistenceService.GetWorkflowSchemaAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(workflowSchema);

        _stateManager.ValidateWorkflowState(
                workflowInstance, "suspended-activity", WorkflowStatus.Suspended)
            .Returns(new StateValidationResult { IsValid = true });

        _activityFactory.CreateActivity("TaskActivity").Returns(_mockActivity);

        _mockActivity.ExecuteAsync(Arg.Any<WorkflowActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(ActivityResult.Success(new Dictionary<string, object>()));

        _flowControlManager.DetermineNextActivityAsync(
                Arg.Any<WorkflowSchema>(), Arg.Any<string>(), Arg.Any<ActivityResult>(),
                Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns((string?)null); // Complete after resume

        _lifecycleManager.CompleteWorkflowAsync(Arg.Any<WorkflowInstance>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _stateManager.CreateCheckpointAsync(Arg.Any<WorkflowInstance>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _workflowEngine.ResumeWorkflowAsync(
            workflowInstanceId, "suspended-activity", "recovery@company.com");

        // Assert
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        // Verify recovery checkpoint was created
        await _stateManager.Received(1).CreateCheckpointAsync(
            workflowInstance, "Workflow completed after resume operation", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests checkpoint during workflow cancellation scenarios
    /// </summary>
    [Fact]
    public async Task CheckpointDuringWorkflowCancellation_ShouldPersistCancelledState()
    {
        // Arrange
        var workflowInstance = CreateTestWorkflowInstance();
        workflowInstance.UpdateStatus(WorkflowStatus.Running, "Currently executing");

        // Simulate cancellation during execution
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _lifecycleManager.TransitionWorkflowStateAsync(
                workflowInstance, WorkflowStatus.Cancelled, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _stateManager.CreateCheckpointAsync(workflowInstance, Arg.Any<string>(), cancellationToken)
            .Returns(Task.CompletedTask);

        // Act
        await _lifecycleManager.TransitionWorkflowStateAsync(
            workflowInstance, WorkflowStatus.Cancelled, "User requested cancellation", cancellationToken);
        
        await _stateManager.CreateCheckpointAsync(
            workflowInstance, "Workflow cancelled by user", cancellationToken);

        // Assert
        workflowInstance.Status.Should().Be(WorkflowStatus.Cancelled);

        // Verify cancellation checkpoint
        await _stateManager.Received(1).CreateCheckpointAsync(
            workflowInstance, "Workflow cancelled by user", cancellationToken);
    }

    /// <summary>
    /// Tests memory usage doesn't grow unbounded without checkpoints
    /// </summary>
    [Fact]
    public void WorkflowStateManager_WithoutCheckpoints_ShouldNotLeakMemory()
    {
        // Arrange
        var workflowInstance = CreateTestWorkflowInstance();
        var initialMemory = GC.GetTotalMemory(true);

        // Act - Simulate many state updates without checkpoints
        for (int i = 0; i < 1000; i++)
        {
            var outputData = new Dictionary<string, object>
            {
                [$"variable_{i}"] = $"value_{i}",
                ["timestamp"] = DateTime.UtcNow,
                ["iteration"] = i
            };

            // Update workflow variables (in-memory only, no persistence)
            workflowInstance.UpdateVariables(outputData);
        }

        var afterUpdatesMemory = GC.GetTotalMemory(true);

        // Assert
        var memoryIncrease = afterUpdatesMemory - initialMemory;
        
        // Memory increase should be reasonable (less than 1MB for 1000 updates)
        memoryIncrease.Should().BeLessThan(1_000_000, 
            "Memory usage should not grow unbounded without strategic checkpointing");

        // Verify workflow state is still valid
        workflowInstance.Variables.Should().NotBeEmpty();
        workflowInstance.Variables.Should().ContainKey("variable_999");
    }

    #region Helper Methods

    private ActivityDefinition CreateTestActivityDefinition(string id, string type)
    {
        return new ActivityDefinition
        {
            Id = id,
            Name = $"Test {id}",
            Type = type,
            Properties = new Dictionary<string, object>()
        };
    }

    private WorkflowSchema CreateTestWorkflowSchema()
    {
        return new WorkflowSchema
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Workflow Schema",
            Description = "Test workflow for performance testing",
            Activities = new List<ActivityDefinition>
            {
                CreateTestActivityDefinition("start", "StartActivity"),
                CreateTestActivityDefinition("middle-activity", "TaskActivity"),
                CreateTestActivityDefinition("end-activity", "EndActivity")
            },
            Transitions = new List<TransitionDefinition>
            {
                new() { Id = "start-to-middle", From = "start", To = "middle-activity" },
                new() { Id = "middle-to-end", From = "middle-activity", To = "end-activity" }
            },
            Variables = new Dictionary<string, object>(),
            Metadata = new WorkflowMetadata
            {
                Version = "1.0.0",
                Author = "Performance Test",
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

    #endregion
}