using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.Workflow;

public class ForkJoinEngineTests
{
    // --- WorkflowInstance branch tracking tests ---

    [Fact]
    public void AddBranchActivity_ShouldAddToActiveBranchActivities()
    {
        var instance = CreateWorkflowInstance();

        instance.AddBranchActivity(new BranchActivityState
        {
            ForkId = "fork-1",
            BranchId = "branch-a",
            ActivityId = "task-a",
            Status = "Pending"
        });

        instance.ActiveBranchActivities.Should().HaveCount(1);
        instance.IsInParallelMode().Should().BeTrue();
    }

    [Fact]
    public void RemoveBranchActivity_ShouldRemoveCorrectBranch()
    {
        var instance = CreateWorkflowInstance();
        instance.AddBranchActivity(new BranchActivityState
        {
            ForkId = "fork-1", BranchId = "branch-a", ActivityId = "task-a", Status = "Pending"
        });
        instance.AddBranchActivity(new BranchActivityState
        {
            ForkId = "fork-1", BranchId = "branch-b", ActivityId = "task-b", Status = "Pending"
        });

        instance.RemoveBranchActivity("fork-1", "branch-a");

        instance.ActiveBranchActivities.Should().HaveCount(1);
        instance.ActiveBranchActivities[0].BranchId.Should().Be("branch-b");
    }

    [Fact]
    public void GetBranchActivity_ShouldFindByActivityId()
    {
        var instance = CreateWorkflowInstance();
        instance.AddBranchActivity(new BranchActivityState
        {
            ForkId = "fork-1", BranchId = "branch-a", ActivityId = "task-a", Status = "Pending"
        });

        var found = instance.GetBranchActivity("task-a");
        found.Should().NotBeNull();
        found!.BranchId.Should().Be("branch-a");

        var notFound = instance.GetBranchActivity("nonexistent");
        notFound.Should().BeNull();
    }

    [Fact]
    public void UpdateBranchActivityId_ShouldUpdateCorrectBranch()
    {
        var instance = CreateWorkflowInstance();
        instance.AddBranchActivity(new BranchActivityState
        {
            ForkId = "fork-1", BranchId = "branch-a", ActivityId = "task-a", Status = "Pending"
        });

        instance.UpdateBranchActivityId("fork-1", "branch-a", "task-a2");

        instance.GetBranchActivity("task-a2").Should().NotBeNull();
        instance.GetBranchActivity("task-a").Should().BeNull();
    }

    [Fact]
    public void ClearBranchActivities_ShouldRemoveAllForFork()
    {
        var instance = CreateWorkflowInstance();
        instance.AddBranchActivity(new BranchActivityState
        {
            ForkId = "fork-1", BranchId = "branch-a", ActivityId = "task-a", Status = "Pending"
        });
        instance.AddBranchActivity(new BranchActivityState
        {
            ForkId = "fork-1", BranchId = "branch-b", ActivityId = "task-b", Status = "Pending"
        });
        instance.AddBranchActivity(new BranchActivityState
        {
            ForkId = "fork-2", BranchId = "branch-c", ActivityId = "task-c", Status = "Pending"
        });

        instance.ClearBranchActivities("fork-1");

        instance.ActiveBranchActivities.Should().HaveCount(1);
        instance.ActiveBranchActivities[0].ForkId.Should().Be("fork-2");
    }

    [Fact]
    public void HasActiveBranches_ShouldReturnFalseWhenEmpty()
    {
        var instance = CreateWorkflowInstance();
        instance.HasActiveBranches().Should().BeFalse();
        instance.IsInParallelMode().Should().BeFalse();
    }

    // --- FlowControlManager fork routing tests ---

    [Fact]
    public void DetermineNextActivitiesForFork_ShouldMapBranchesToTransitions()
    {
        var logger = Substitute.For<ILogger<FlowControlManager>>();
        var flowControl = new FlowControlManager(logger);

        var schema = new WorkflowSchema
        {
            Id = "test",
            Name = "Test",
            Activities = new List<ActivityDefinition>
            {
                new() { Id = "fork-1", Type = ActivityTypes.ForkActivity, Name = "Fork" },
                new() { Id = "task-a", Type = ActivityTypes.TaskActivity, Name = "Task A" },
                new() { Id = "task-b", Type = ActivityTypes.TaskActivity, Name = "Task B" }
            },
            Transitions = new List<TransitionDefinition>
            {
                new()
                {
                    Id = "t1", From = "fork-1", To = "task-a", Type = TransitionType.Normal,
                    Properties = new Dictionary<string, object> { ["branchId"] = "branch_a" }
                },
                new()
                {
                    Id = "t2", From = "fork-1", To = "task-b", Type = TransitionType.Normal,
                    Properties = new Dictionary<string, object> { ["branchId"] = "branch_b" }
                }
            }
        };

        var activityResult = ActivityResult.Success(new Dictionary<string, object>
        {
            ["forkId"] = "f1",
            ["branchIds"] = new List<string> { "branch_a", "branch_b" }
        });

        var result = flowControl.DetermineNextActivitiesForFork(schema, "fork-1", activityResult);

        result.Should().HaveCount(2);
        result[0].BranchId.Should().Be("branch_a");
        result[0].ActivityId.Should().Be("task-a");
        result[1].BranchId.Should().Be("branch_b");
        result[1].ActivityId.Should().Be("task-b");
    }

    [Fact]
    public void DetermineNextActivitiesForFork_ShouldReturnEmptyWhenNoBranchIds()
    {
        var logger = Substitute.For<ILogger<FlowControlManager>>();
        var flowControl = new FlowControlManager(logger);

        var schema = new WorkflowSchema
        {
            Id = "test",
            Name = "Test",
            Activities = new List<ActivityDefinition>(),
            Transitions = new List<TransitionDefinition>()
        };

        var activityResult = ActivityResult.Success(new Dictionary<string, object> { ["forkId"] = "f1" });

        var result = flowControl.DetermineNextActivitiesForFork(schema, "fork-1", activityResult);
        result.Should().BeEmpty();
    }

    [Fact]
    public void DetermineNextActivitiesForFork_ShouldSkipUnmatchedBranches()
    {
        var logger = Substitute.For<ILogger<FlowControlManager>>();
        var flowControl = new FlowControlManager(logger);

        var schema = new WorkflowSchema
        {
            Id = "test",
            Name = "Test",
            Activities = new List<ActivityDefinition>
            {
                new() { Id = "fork-1", Type = ActivityTypes.ForkActivity, Name = "Fork" },
                new() { Id = "task-a", Type = ActivityTypes.TaskActivity, Name = "Task A" }
            },
            Transitions = new List<TransitionDefinition>
            {
                new()
                {
                    Id = "t1", From = "fork-1", To = "task-a", Type = TransitionType.Normal,
                    Properties = new Dictionary<string, object> { ["branchId"] = "branch_a" }
                }
            }
        };

        var activityResult = ActivityResult.Success(new Dictionary<string, object>
        {
            ["forkId"] = "f1",
            ["branchIds"] = new List<string> { "branch_a", "branch_missing" }
        });

        var result = flowControl.DetermineNextActivitiesForFork(schema, "fork-1", activityResult);
        result.Should().HaveCount(1);
        result[0].BranchId.Should().Be("branch_a");
    }

    // --- StateManager branch validation tests ---

    [Fact]
    public void ValidateWorkflowState_ShouldAcceptBranchActivityId()
    {
        var logger = Substitute.For<ILogger<WorkflowStateManager>>();
        var persistence = Substitute.For<IWorkflowPersistenceService>();
        var stateManager = new WorkflowStateManager(persistence, logger);

        var instance = CreateWorkflowInstance();
        instance.SetCurrentActivity("main-activity");
        instance.UpdateStatus(WorkflowStatus.Suspended);
        instance.AddBranchActivity(new BranchActivityState
        {
            ForkId = "fork-1", BranchId = "branch-a", ActivityId = "task-in-branch", Status = "Pending"
        });

        // Should accept branch activity ID even though CurrentActivityId is different
        var result = stateManager.ValidateWorkflowState(instance, "task-in-branch", WorkflowStatus.Suspended);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateWorkflowState_ShouldRejectUnknownActivityWhenInParallelMode()
    {
        var logger = Substitute.For<ILogger<WorkflowStateManager>>();
        var persistence = Substitute.For<IWorkflowPersistenceService>();
        var stateManager = new WorkflowStateManager(persistence, logger);

        var instance = CreateWorkflowInstance();
        instance.SetCurrentActivity("main-activity");
        instance.UpdateStatus(WorkflowStatus.Suspended);
        instance.AddBranchActivity(new BranchActivityState
        {
            ForkId = "fork-1", BranchId = "branch-a", ActivityId = "task-in-branch", Status = "Pending"
        });

        var result = stateManager.ValidateWorkflowState(instance, "nonexistent-activity", WorkflowStatus.Suspended);
        result.IsValid.Should().BeFalse();
    }

    // --- ForkActivity unit tests ---

    [Fact]
    public async Task ForkActivity_Execute_ShouldReturnBranchIds()
    {
        var fork = new ForkActivity();

        var branches = new List<object>
        {
            new Dictionary<string, object> { ["id"] = "branch_a", ["name"] = "Branch A" },
            new Dictionary<string, object> { ["id"] = "branch_b", ["name"] = "Branch B" }
        };

        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "fork-1",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = branches,
                ["forkType"] = "all"
            },
            Variables = new Dictionary<string, object>(),
            WorkflowInstance = CreateWorkflowInstance()
        };

        var result = await fork.ExecuteAsync(context, CancellationToken.None);

        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("forkId");
        result.OutputData.Should().ContainKey("branchIds");
    }

    // --- JoinActivity unit tests ---

    [Fact]
    public async Task JoinActivity_Execute_ShouldReturnPending_WhenNotAllBranchesComplete()
    {
        var join = new JoinActivity();

        var forkContext = new ForkExecutionContext
        {
            ForkId = "fork-1",
            ActivityId = "fork-activity",
            Branches = new List<ForkBranch>
            {
                new() { Id = "branch_a", Name = "A" },
                new() { Id = "branch_b", Name = "B" }
            },
            ForkType = "all",
            CreatedAt = DateTime.UtcNow,
            Status = ForkStatus.Active
        };

        // Only branch_a completed
        var branchResults = new Dictionary<string, BranchExecutionResult>
        {
            ["branch_a"] = new()
            {
                BranchId = "branch_a",
                Status = BranchStatus.Completed,
                CompletedAt = DateTime.UtcNow
            }
        };

        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "join-1",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = "fork-1",
                ["joinType"] = "all"
            },
            Variables = new Dictionary<string, object>
            {
                ["fork_fork-1"] = forkContext,
                ["fork_fork-1_results"] = branchResults
            },
            WorkflowInstance = CreateWorkflowInstance()
        };

        var result = await join.ExecuteAsync(context, CancellationToken.None);

        result.Status.Should().Be(ActivityResultStatus.Pending);
    }

    [Fact]
    public async Task JoinActivity_Execute_ShouldComplete_WhenAllBranchesComplete()
    {
        var join = new JoinActivity();

        var forkContext = new ForkExecutionContext
        {
            ForkId = "fork-1",
            ActivityId = "fork-activity",
            Branches = new List<ForkBranch>
            {
                new() { Id = "branch_a", Name = "A" },
                new() { Id = "branch_b", Name = "B" }
            },
            ForkType = "all",
            CreatedAt = DateTime.UtcNow,
            Status = ForkStatus.Active
        };

        var branchResults = new Dictionary<string, BranchExecutionResult>
        {
            ["branch_a"] = new()
            {
                BranchId = "branch_a", Status = BranchStatus.Completed, CompletedAt = DateTime.UtcNow
            },
            ["branch_b"] = new()
            {
                BranchId = "branch_b", Status = BranchStatus.Completed, CompletedAt = DateTime.UtcNow
            }
        };

        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "join-1",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = "fork-1",
                ["joinType"] = "all"
            },
            Variables = new Dictionary<string, object>
            {
                ["fork_fork-1"] = forkContext,
                ["fork_fork-1_results"] = branchResults
            },
            WorkflowInstance = CreateWorkflowInstance()
        };

        var result = await join.ExecuteAsync(context, CancellationToken.None);

        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("completedBranches");
    }

    [Fact]
    public async Task JoinActivity_AnyType_ShouldComplete_WhenFirstBranchComplete()
    {
        var join = new JoinActivity();

        var forkContext = new ForkExecutionContext
        {
            ForkId = "fork-1",
            ActivityId = "fork-activity",
            Branches = new List<ForkBranch>
            {
                new() { Id = "branch_a", Name = "A" },
                new() { Id = "branch_b", Name = "B" }
            },
            ForkType = "all",
            CreatedAt = DateTime.UtcNow,
            Status = ForkStatus.Active
        };

        var branchResults = new Dictionary<string, BranchExecutionResult>
        {
            ["branch_a"] = new()
            {
                BranchId = "branch_a", Status = BranchStatus.Completed, CompletedAt = DateTime.UtcNow
            }
        };

        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "join-1",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = "fork-1",
                ["joinType"] = "any"
            },
            Variables = new Dictionary<string, object>
            {
                ["fork_fork-1"] = forkContext,
                ["fork_fork-1_results"] = branchResults
            },
            WorkflowInstance = CreateWorkflowInstance()
        };

        var result = await join.ExecuteAsync(context, CancellationToken.None);

        result.Status.Should().Be(ActivityResultStatus.Completed);
    }

    // --- Helper ---

    private static WorkflowInstance CreateWorkflowInstance()
    {
        return WorkflowInstance.Create(
            Guid.NewGuid(),
            "test-workflow",
            null,
            "test-user");
    }
}
