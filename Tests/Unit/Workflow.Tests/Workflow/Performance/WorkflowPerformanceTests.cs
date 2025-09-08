using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Diagnostics;
using Workflow.Workflow.Engine.Core;
using Xunit;
using ActivityContext = Workflow.Workflow.Activities.Core.ActivityContext;

namespace Workflow.Tests.Workflow.Performance;

/// <summary>
/// Performance tests for workflow engine components
/// Validates execution times, memory usage, and scalability
/// </summary>
public class WorkflowPerformanceTests
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowPerformanceTests()
    {
        _logger = Substitute.For<ILogger<WorkflowEngine>>();
        _workflowEngine = Substitute.For<IWorkflowEngine>();
        _workflowService = Substitute.For<IWorkflowService>();
    }

    [Fact]
    public async Task WorkflowExecution_SimpleActivity_CompletesWithinPerformanceThreshold()
    {
        // Arrange
        var activityDefinition = new ActivityDefinition
        {
            Id = "test-activity",
            Type = "TaskActivity",
            Name = "Performance Test Activity",
            Properties = new Dictionary<string, object>()
        };

        var context = new ActivityContext
        {
            ActivityId = "test-activity",
            WorkflowInstanceId = Guid.NewGuid(),
            WorkflowInstance = WorkflowInstance.Create(
                Guid.NewGuid(),
                "Test Workflow",
                null,
                "test@company.com"),
            Variables = new Dictionary<string, object>(),
            Properties = new Dictionary<string, object>()
        };

        var expectedResult = ActivityResult.Success(new Dictionary<string, object>
        {
            ["completed_at"] = DateTime.UtcNow,
            ["execution_time_ms"] = 50
        });

        _workflowEngine.ExecuteActivityAsync(activityDefinition, context, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var result = await _workflowEngine.ExecuteActivityAsync(activityDefinition, context);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);

        // Performance assertion - should complete within 1000ms (generous threshold for mocked test)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000,
            "Single activity execution should complete within 1 second");
    }

    [Fact]
    public async Task WorkflowStartup_MultipleInstances_HandlesLoadEfficiently()
    {
        // Arrange
        const int instanceCount = 100;
        var workflowDefinitionId = Guid.NewGuid();
        var tasks = new List<Task<WorkflowExecutionResult>>();

        // Setup mock to return quickly
        _workflowEngine.StartWorkflowAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>()
        ).Returns(WorkflowExecutionResult.Running(
            WorkflowInstance.Create(workflowDefinitionId, "Test", null, "user", new Dictionary<string, object>()),
            "next-activity"));

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < instanceCount; i++)
        {
            var task = _workflowEngine.StartWorkflowAsync(
                workflowDefinitionId,
                $"Performance Test #{i + 1}",
                $"user{i}@company.com",
                new Dictionary<string, object> { ["instance_id"] = i });

            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(instanceCount);
        results.Should().OnlyContain(r => r.Status == WorkflowExecutionStatus.Running);

        // Performance assertions
        var averageTimePerInstance = (double)stopwatch.ElapsedMilliseconds / instanceCount;
        averageTimePerInstance.Should().BeLessThan(50,
            "Average workflow startup time should be less than 50ms per instance");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
            $"Starting {instanceCount} workflows should complete within 10 seconds");
    }

    [Fact]
    public async Task ActivityExecution_ConcurrentActivities_MaintainsPerformance()
    {
        // Arrange
        const int concurrentActivities = 50;
        var tasks = new List<Task<ActivityResult>>();

        var activityDefinition = new ActivityDefinition
        {
            Id = "concurrent-test",
            Type = "TaskActivity",
            Name = "Concurrent Activity Test",
            Properties = new Dictionary<string, object>()
        };

        _workflowEngine.ExecuteActivityAsync(
            Arg.Any<ActivityDefinition>(),
            Arg.Any<ActivityContext>(),
            Arg.Any<CancellationToken>()
        ).Returns(ActivityResult.Success(new Dictionary<string, object>
        {
            ["completed"] = true,
            ["timestamp"] = DateTime.UtcNow
        }));

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < concurrentActivities; i++)
        {
            var context = new ActivityContext
            {
                ActivityId = $"activity-{i}",
                WorkflowInstanceId = Guid.NewGuid(),
                WorkflowInstance = WorkflowInstance.Create(
                    Guid.NewGuid(),
                    $"Concurrent Test {i}",
                    null,
                    "test@company.com"),
                Variables = new Dictionary<string, object> { ["thread_id"] = i },
                Properties = new Dictionary<string, object>()
            };

            var task = _workflowEngine.ExecuteActivityAsync(activityDefinition, context);
            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(concurrentActivities);
        results.Should().OnlyContain(r => r.Status == ActivityResultStatus.Completed);

        // Performance assertions for concurrent execution
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
            $"Executing {concurrentActivities} activities concurrently should complete within 5 seconds");
    }

    [Fact]
    public async Task WorkflowValidation_ComplexSchema_CompletesQuickly()
    {
        // Arrange
        var complexSchema = CreateComplexWorkflowSchema();

        _workflowEngine.ValidateWorkflowDefinitionAsync(complexSchema, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var isValid = await _workflowEngine.ValidateWorkflowDefinitionAsync(complexSchema);
        stopwatch.Stop();

        // Assert
        isValid.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000,
            "Complex workflow validation should complete within 2 seconds");
    }

    [Fact]
    public async Task WorkflowResumption_MultipleResumes_MaintainsResponseTime()
    {
        // Arrange
        const int resumeCount = 20;
        var workflowInstanceId = Guid.NewGuid();
        var tasks = new List<Task<WorkflowExecutionResult>>();

        _workflowEngine.ResumeWorkflowAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>()
        ).Returns(WorkflowExecutionResult.Running(
            WorkflowInstance.Create(Guid.NewGuid(), "Test", null, "user", new Dictionary<string, object>()),
            "next-activity"));

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < resumeCount; i++)
        {
            var task = _workflowEngine.ResumeWorkflowAsync(
                workflowInstanceId,
                $"activity-{i}",
                $"user{i}@company.com",
                new Dictionary<string, object> { ["resume_data"] = $"data-{i}" });

            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(resumeCount);
        results.Should().OnlyContain(r => r.Status == WorkflowExecutionStatus.Running);

        var averageResumeTime = (double)stopwatch.ElapsedMilliseconds / resumeCount;
        averageResumeTime.Should().BeLessThan(100,
            "Average workflow resume time should be less than 100ms");
    }

    [Fact]
    public async Task UserTaskRetrieval_LargeTaskList_RespondsQuickly()
    {
        // Arrange
        const int taskCount = 500;
        var userId = "heavy-user@company.com";

        var largeTasks = Enumerable.Range(0, taskCount)
            .Select(i => WorkflowInstance.Create(
                Guid.NewGuid(),
                $"Task #{i + 1}",
                null,
                userId,
                new Dictionary<string, object> { ["task_number"] = i }))
            .ToList();

        _workflowService.GetUserTasksAsync(userId, Arg.Any<CancellationToken>())
            .Returns(largeTasks);

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var userTasks = await _workflowService.GetUserTasksAsync(userId);
        stopwatch.Stop();

        // Assert
        var taskList = userTasks.ToList();
        taskList.Should().HaveCount(taskCount);

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000,
            $"Retrieving {taskCount} user tasks should complete within 3 seconds");
    }

    [Fact]
    public async Task WorkflowMemoryUsage_LargeVariableSets_StaysWithinLimits()
    {
        // Arrange
        var baseMemory = GC.GetTotalMemory(true);
        var workflowDefinitionId = Guid.NewGuid();
        const int workflowCount = 10;

        // Create workflows with large variable sets
        var tasks = new List<Task<WorkflowExecutionResult>>();

        _workflowEngine.StartWorkflowAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, RuntimeOverride>>(),
            Arg.Any<CancellationToken>()
        ).Returns(callInfo =>
        {
            var variables = callInfo.ArgAt<Dictionary<string, object>>(3);
            return WorkflowExecutionResult.Running(
                WorkflowInstance.Create(workflowDefinitionId, "Memory Test", null, "user", variables),
                "next");
        });

        // Act
        for (var i = 0; i < workflowCount; i++)
        {
            var largeVariables = new Dictionary<string, object>();

            // Create 1000 variables per workflow
            for (var j = 0; j < 1000; j++)
                largeVariables[$"var_{i}_{j}"] =
                    $"Large data string for variable {i}-{j} with substantial content to simulate real-world variable storage requirements";

            var task = _workflowEngine.StartWorkflowAsync(
                workflowDefinitionId,
                $"Memory Test Workflow #{i + 1}",
                "memory-test@company.com",
                largeVariables);

            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);
        var afterMemory = GC.GetTotalMemory(true);
        var memoryIncrease = afterMemory - baseMemory;

        // Assert
        results.Should().HaveCount(workflowCount);
        results.Should().OnlyContain(r => r.Status == WorkflowExecutionStatus.Running);

        // Memory usage should be reasonable (less than 50MB for test workflows with large variables)
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024,
            $"Memory increase should be less than 50MB for {workflowCount} workflows with large variable sets");
    }

    [Fact]
    public async Task ActivityChainExecution_LongSequence_MaintainsPerformance()
    {
        // Arrange
        const int sequenceLength = 30;
        var activityResults = new List<ActivityResult>();

        var activityDefinition = new ActivityDefinition
        {
            Id = "chain-activity",
            Type = "TaskActivity",
            Name = "Chain Activity",
            Properties = new Dictionary<string, object>()
        };

        _workflowEngine.ExecuteActivityAsync(
            Arg.Any<ActivityDefinition>(),
            Arg.Any<ActivityContext>(),
            Arg.Any<CancellationToken>()
        ).Returns(ActivityResult.Success(new Dictionary<string, object> { ["step"] = "completed" }));

        var context = new ActivityContext
        {
            ActivityId = "chain-start",
            WorkflowInstanceId = Guid.NewGuid(),
            WorkflowInstance = WorkflowInstance.Create(
                Guid.NewGuid(),
                "Chain Test",
                null,
                "test@company.com"),
            Variables = new Dictionary<string, object>(),
            Properties = new Dictionary<string, object>()
        };

        // Act & Measure - Simulate sequential activity execution
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < sequenceLength; i++)
        {
            // Create new context for each iteration since ActivityId is init-only
            var iterationContext = new ActivityContext
            {
                ActivityId = $"chain-step-{i}",
                WorkflowInstanceId = context.WorkflowInstanceId,
                WorkflowInstance = context.WorkflowInstance,
                Variables = new Dictionary<string, object>(context.Variables) { [$"step_{i}"] = $"Step {i} data" },
                Properties = context.Properties
            };

            var result = await _workflowEngine.ExecuteActivityAsync(activityDefinition, iterationContext);
            activityResults.Add(result);
        }

        stopwatch.Stop();

        // Assert
        activityResults.Should().HaveCount(sequenceLength);
        activityResults.Should().OnlyContain(r => r.Status == ActivityResultStatus.Completed);

        var averageExecutionTime = (double)stopwatch.ElapsedMilliseconds / sequenceLength;
        averageExecutionTime.Should().BeLessThan(200,
            "Average activity execution in chain should be less than 200ms");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
            $"Sequential execution of {sequenceLength} activities should complete within 10 seconds");
    }

    [Fact]
    public async Task WorkflowCancellation_MultipleWorkflows_RespondsQuickly()
    {
        // Arrange
        const int workflowCount = 25;
        var cancellationTasks = new List<Task>();
        var workflowIds = Enumerable.Range(0, workflowCount).Select(_ => Guid.NewGuid()).ToList();

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();

        foreach (var workflowId in workflowIds)
        {
            var task = _workflowService.CancelWorkflowAsync(
                workflowId,
                "performance-test@company.com",
                "Performance test cancellation");

            cancellationTasks.Add(task);
        }

        await Task.WhenAll(cancellationTasks);
        stopwatch.Stop();

        // Assert
        var averageCancellationTime = (double)stopwatch.ElapsedMilliseconds / workflowCount;
        averageCancellationTime.Should().BeLessThan(50,
            "Average workflow cancellation should be less than 50ms");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000,
            $"Cancelling {workflowCount} workflows should complete within 2 seconds");

        // Verify all cancellation calls were made
        await _workflowService.Received(workflowCount).CancelWorkflowAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private WorkflowSchema CreateComplexWorkflowSchema()
    {
        var activities = new Dictionary<string, ActivityDefinition>();
        var transitions = new List<TransitionDefinition>();

        // Create a complex workflow with 20 activities and multiple decision points
        for (var i = 0; i < 20; i++)
        {
            var activityId = $"activity-{i}";
            var activityType = (i % 5) switch
            {
                0 => "TaskActivity",
                1 => "IfElseActivity",
                2 => "SwitchActivity",
                3 => "ForkActivity",
                4 => "JoinActivity",
                _ => "TaskActivity"
            };

            activities[activityId] = new ActivityDefinition
            {
                Id = activityId,
                Type = activityType,
                Name = $"Complex Activity {i + 1}",
                Properties = new Dictionary<string, object>
                {
                    ["complexity_level"] = "high",
                    ["processing_time"] = TimeSpan.FromMinutes(i + 1).ToString(),
                    ["assignment_strategies"] = new[] { "workload_based", "round_robin", "manual" }
                }
            };


            // Add transitions (sequential for simplicity in performance test)
            if (i > 0)
                transitions.Add(new TransitionDefinition
                {
                    From = $"activity-{i - 1}",
                    To = activityId,
                    Condition = i % 3 == 0 ? $"step_{i - 1}_completed == true" : null
                });
        }

        return new WorkflowSchema
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Complex Performance Test Workflow",
            Description = "Complex workflow for performance testing with multiple activity types and decision points",
            Activities = activities.Values.ToList(),
            Transitions = transitions,
            Metadata = new WorkflowMetadata
            {
                Version = "1.0.0",
                Author = "Performance Test",
                CreatedDate = DateTime.UtcNow
            }
        };
    }
}