using Assignment.Workflow.Activities;
using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Assignment.Tests.Workflow.Activities;

public class JoinActivityTests
{
    private readonly JoinActivity _activity;

    public JoinActivityTests()
    {
        _activity = new JoinActivity();
    }

    [Fact]
    public async Task ExecuteAsync_AllBranchesComplete_ReturnsSuccess()
    {
        // Arrange
        var forkId = "test_fork";
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "join-test",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = forkId,
                ["joinType"] = "all"
            },
            Variables = new Dictionary<string, object>
            {
                [$"fork_{forkId}"] = new ForkExecutionContext
                {
                    ForkId = forkId,
                    Branches = new List<ForkBranch>
                    {
                        new() { Id = "branch1", Name = "Credit Check" },
                        new() { Id = "branch2", Name = "Income Verification" }
                    },
                    Status = ForkStatus.Active
                },
                [$"fork_{forkId}_results"] = new Dictionary<string, BranchExecutionResult>
                {
                    ["branch1"] = new() 
                    { 
                        BranchId = "branch1", 
                        Status = BranchStatus.Completed, 
                        OutputData = new() { ["credit_result"] = "passed" },
                        CompletedAt = DateTime.UtcNow
                    },
                    ["branch2"] = new() 
                    { 
                        BranchId = "branch2", 
                        Status = BranchStatus.Completed, 
                        OutputData = new() { ["income_result"] = "verified" },
                        CompletedAt = DateTime.UtcNow
                    }
                }
            },
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("completedBranches");
        result.OutputData["completedBranches"].Should().Be(2);
        result.OutputData.Should().ContainKey("mergedData");
    }

    [Fact]
    public async Task ExecuteAsync_JoinTypeAll_WaitsForAllBranches()
    {
        // Arrange - Only one branch completed out of two
        var forkId = "all_join_test";
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "join-all-test",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = forkId,
                ["joinType"] = "all"
            },
            Variables = new Dictionary<string, object>
            {
                [$"fork_{forkId}"] = new ForkExecutionContext
                {
                    ForkId = forkId,
                    Branches = new List<ForkBranch>
                    {
                        new() { Id = "branch1", Name = "Branch 1" },
                        new() { Id = "branch2", Name = "Branch 2" }
                    },
                    Status = ForkStatus.Active
                },
                [$"fork_{forkId}_results"] = new Dictionary<string, BranchExecutionResult>
                {
                    ["branch1"] = new() 
                    { 
                        BranchId = "branch1", 
                        Status = BranchStatus.Completed,
                        CompletedAt = DateTime.UtcNow
                    },
                    ["branch2"] = new() 
                    { 
                        BranchId = "branch2", 
                        Status = BranchStatus.Running 
                    }
                }
            },
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Pending);
    }

    [Fact]
    public async Task ExecuteAsync_JoinTypeAny_ProceedsWithOneBranch()
    {
        // Arrange
        var forkId = "any_join_test";
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "join-any-test",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = forkId,
                ["joinType"] = "any"
            },
            Variables = new Dictionary<string, object>
            {
                [$"fork_{forkId}"] = new ForkExecutionContext
                {
                    ForkId = forkId,
                    Branches = new List<ForkBranch>
                    {
                        new() { Id = "branch1", Name = "Branch 1" },
                        new() { Id = "branch2", Name = "Branch 2" },
                        new() { Id = "branch3", Name = "Branch 3" }
                    },
                    Status = ForkStatus.Active
                },
                [$"fork_{forkId}_results"] = new Dictionary<string, BranchExecutionResult>
                {
                    ["branch1"] = new() 
                    { 
                        BranchId = "branch1", 
                        Status = BranchStatus.Completed,
                        OutputData = new() { ["result"] = "success1" },
                        CompletedAt = DateTime.UtcNow
                    },
                    ["branch2"] = new() { BranchId = "branch2", Status = BranchStatus.Running },
                    ["branch3"] = new() { BranchId = "branch3", Status = BranchStatus.Pending }
                }
            },
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["completedBranches"].Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_JoinTypeMajority_RequiresMajorityCompletion()
    {
        // Test cases for majority join logic
        var testCases = new[]
        {
            new { CompletedBranches = 2, TotalBranches = 3, ShouldProceed = true, Description = "2 of 3 completed" },
            new { CompletedBranches = 1, TotalBranches = 3, ShouldProceed = false, Description = "1 of 3 completed" },
            new { CompletedBranches = 3, TotalBranches = 4, ShouldProceed = true, Description = "3 of 4 completed" },
            new { CompletedBranches = 2, TotalBranches = 4, ShouldProceed = false, Description = "2 of 4 completed" }
        };

        foreach (var testCase in testCases)
        {
            // Arrange
            var forkId = $"majority_test_{testCase.TotalBranches}_{testCase.CompletedBranches}";
            var branches = Enumerable.Range(0, testCase.TotalBranches)
                .Select(i => new ForkBranch { Id = $"branch_{i}", Name = $"Branch {i}" })
                .ToList();

            var branchResults = new Dictionary<string, BranchExecutionResult>();
            for (int i = 0; i < testCase.TotalBranches; i++)
            {
                var branchId = $"branch_{i}";
                var status = i < testCase.CompletedBranches ? BranchStatus.Completed : BranchStatus.Running;
                branchResults[branchId] = new BranchExecutionResult
                {
                    BranchId = branchId,
                    Status = status,
                    OutputData = status == BranchStatus.Completed 
                        ? new Dictionary<string, object> { ["result"] = $"completed_{i}" }
                        : new Dictionary<string, object>(),
                    CompletedAt = status == BranchStatus.Completed ? DateTime.UtcNow : null
                };
            }

            var context = new ActivityContext
            {
                WorkflowInstanceId = Guid.NewGuid(),
                ActivityId = "join-majority-test",
                Properties = new Dictionary<string, object>
                {
                    ["forkId"] = forkId,
                    ["joinType"] = "majority"
                },
                Variables = new Dictionary<string, object>
                {
                    [$"fork_{forkId}"] = new ForkExecutionContext
                    {
                        ForkId = forkId,
                        Branches = branches,
                        Status = ForkStatus.Active
                    },
                    [$"fork_{forkId}_results"] = branchResults
                },
                WorkflowInstance = null!
            };

            // Act
            var result = await _activity.ExecuteAsync(context);

            // Assert
            result.Should().NotBeNull($"Test case: {testCase.Description}");
            if (testCase.ShouldProceed)
            {
                result.Status.Should().Be(ActivityResultStatus.Completed, $"Test case: {testCase.Description}");
            }
            else
            {
                result.Status.Should().Be(ActivityResultStatus.Pending, $"Test case: {testCase.Description}");
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_MissingForkId_ReturnsFailure()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "missing-fork-id-test",
            Properties = new Dictionary<string, object>
            {
                ["joinType"] = "all"
                // Missing forkId
            },
            Variables = new Dictionary<string, object>(),
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("requires a forkId");
    }

    [Fact]
    public async Task ExecuteAsync_MissingForkContext_ReturnsFailure()
    {
        // Arrange
        var forkId = "missing_context";
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "missing-context-test",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = forkId,
                ["joinType"] = "all"
            },
            Variables = new Dictionary<string, object>(), // Missing fork context
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain($"Fork context not found for forkId: {forkId}");
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_HandlesTimeoutCorrectly()
    {
        // Arrange - Simulate timeout scenario
        var forkId = "timeout_test";
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "join-timeout-test",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = forkId,
                ["joinType"] = "all",
                ["timeoutMinutes"] = 1 // Very short timeout for testing
            },
            Variables = new Dictionary<string, object>
            {
                [$"fork_{forkId}"] = new ForkExecutionContext
                {
                    ForkId = forkId,
                    Branches = new List<ForkBranch>
                    {
                        new() { Id = "branch1", Name = "Branch 1" }
                    },
                    Status = ForkStatus.Active,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2) // Created 2 minutes ago
                },
                [$"fork_{forkId}_results"] = new Dictionary<string, BranchExecutionResult>
                {
                    ["branch1"] = new() 
                    { 
                        BranchId = "branch1", 
                        Status = BranchStatus.Running // Still running, causing timeout
                    }
                }
            },
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        // Should handle timeout according to implementation logic
        result.Status.Should().BeOneOf(ActivityResultStatus.Failed, ActivityResultStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_DataMerging_CombinesOutputDataCorrectly()
    {
        // Arrange
        var forkId = "merge_test";
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "join-merge-test",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = forkId,
                ["joinType"] = "all",
                ["mergeStrategy"] = "combine"
            },
            Variables = new Dictionary<string, object>
            {
                [$"fork_{forkId}"] = new ForkExecutionContext
                {
                    ForkId = forkId,
                    Branches = new List<ForkBranch>
                    {
                        new() { Id = "credit_check", Name = "Credit Check" },
                        new() { Id = "employment_verify", Name = "Employment Verification" }
                    },
                    Status = ForkStatus.Active
                },
                [$"fork_{forkId}_results"] = new Dictionary<string, BranchExecutionResult>
                {
                    ["credit_check"] = new() 
                    { 
                        BranchId = "credit_check", 
                        Status = BranchStatus.Completed,
                        OutputData = new Dictionary<string, object>
                        {
                            ["credit_score"] = 750,
                            ["credit_status"] = "excellent",
                            ["debt_to_income"] = 0.25
                        },
                        CompletedAt = DateTime.UtcNow
                    },
                    ["employment_verify"] = new() 
                    { 
                        BranchId = "employment_verify", 
                        Status = BranchStatus.Completed,
                        OutputData = new Dictionary<string, object>
                        {
                            ["employment_status"] = "employed",
                            ["income_verified"] = true,
                            ["employment_years"] = 5
                        },
                        CompletedAt = DateTime.UtcNow
                    }
                }
            },
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        var mergedData = result.OutputData["mergedData"] as Dictionary<string, object>;
        mergedData.Should().NotBeNull();
        
        // Verify data from both branches is merged
        mergedData.Should().ContainKey("credit_score");
        mergedData.Should().ContainKey("employment_status");
        mergedData.Should().ContainKey("income_verified");
        mergedData["credit_score"].Should().Be(750);
        mergedData["employment_status"].Should().Be("employed");
    }

    [Fact]
    public async Task ExecuteAsync_BranchFailures_HandlesFailedBranches()
    {
        // Arrange
        var forkId = "failure_test";
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "join-failure-test",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = forkId,
                ["joinType"] = "any" // Should proceed even with some failures
            },
            Variables = new Dictionary<string, object>
            {
                [$"fork_{forkId}"] = new ForkExecutionContext
                {
                    ForkId = forkId,
                    Branches = new List<ForkBranch>
                    {
                        new() { Id = "success_branch", Name = "Success Branch" },
                        new() { Id = "failed_branch", Name = "Failed Branch" }
                    },
                    Status = ForkStatus.Active
                },
                [$"fork_{forkId}_results"] = new Dictionary<string, BranchExecutionResult>
                {
                    ["success_branch"] = new() 
                    { 
                        BranchId = "success_branch", 
                        Status = BranchStatus.Completed,
                        OutputData = new() { ["result"] = "success" },
                        CompletedAt = DateTime.UtcNow
                    },
                    ["failed_branch"] = new() 
                    { 
                        BranchId = "failed_branch", 
                        Status = BranchStatus.Failed,
                        ErrorMessage = "Branch processing failed",
                        CompletedAt = DateTime.UtcNow
                    }
                }
            },
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed); // Should proceed with 'any' join type
        result.OutputData["completedBranches"].Should().Be(1); // Only successful branch counted
        result.OutputData["failedBranches"].Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ComplexAppraisalJoin_HandlesRealWorldScenario()
    {
        // Arrange - Complex appraisal workflow with multiple verification branches
        var forkId = "complex_appraisal_join";
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "complex-appraisal-join",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = forkId,
                ["joinType"] = "majority", // Need majority to pass
                ["mergeStrategy"] = "combine"
            },
            Variables = new Dictionary<string, object>
            {
                [$"fork_{forkId}"] = new ForkExecutionContext
                {
                    ForkId = forkId,
                    Branches = new List<ForkBranch>
                    {
                        new() { Id = "property_valuation", Name = "Property Valuation" },
                        new() { Id = "market_analysis", Name = "Market Analysis" },
                        new() { Id = "environmental_check", Name = "Environmental Assessment" },
                        new() { Id = "legal_review", Name = "Legal Review" }
                    },
                    Status = ForkStatus.Active
                },
                [$"fork_{forkId}_results"] = new Dictionary<string, BranchExecutionResult>
                {
                    ["property_valuation"] = new() 
                    { 
                        BranchId = "property_valuation", 
                        Status = BranchStatus.Completed,
                        OutputData = new Dictionary<string, object>
                        {
                            ["estimated_value"] = 450000,
                            ["confidence_level"] = 0.95,
                            ["valuation_method"] = "comparative_market_analysis"
                        },
                        CompletedAt = DateTime.UtcNow
                    },
                    ["market_analysis"] = new() 
                    { 
                        BranchId = "market_analysis", 
                        Status = BranchStatus.Completed,
                        OutputData = new Dictionary<string, object>
                        {
                            ["market_trend"] = "stable",
                            ["absorption_rate"] = 4.2,
                            ["price_per_sqft"] = 285
                        },
                        CompletedAt = DateTime.UtcNow
                    },
                    ["environmental_check"] = new() 
                    { 
                        BranchId = "environmental_check", 
                        Status = BranchStatus.Completed,
                        OutputData = new Dictionary<string, object>
                        {
                            ["environmental_clear"] = true,
                            ["hazards_identified"] = false,
                            ["remediation_required"] = false
                        },
                        CompletedAt = DateTime.UtcNow
                    },
                    ["legal_review"] = new() 
                    { 
                        BranchId = "legal_review", 
                        Status = BranchStatus.Running // Still in progress
                    }
                }
            },
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed); // 3 of 4 completed = majority
        result.OutputData["completedBranches"].Should().Be(3);
        
        var mergedData = result.OutputData["mergedData"] as Dictionary<string, object>;
        mergedData.Should().ContainKey("estimated_value");
        mergedData.Should().ContainKey("market_trend");
        mergedData.Should().ContainKey("environmental_clear");
        mergedData["estimated_value"].Should().Be(450000);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var forkId = "cancellation_test";
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "join-cancellation-test",
            Properties = new Dictionary<string, object>
            {
                ["forkId"] = forkId,
                ["joinType"] = "all"
            },
            Variables = new Dictionary<string, object>
            {
                [$"fork_{forkId}"] = new ForkExecutionContext
                {
                    ForkId = forkId,
                    Branches = new List<ForkBranch>
                    {
                        new() { Id = "branch1", Name = "Branch 1" }
                    },
                    Status = ForkStatus.Active
                },
                [$"fork_{forkId}_results"] = new Dictionary<string, BranchExecutionResult>()
            },
            WorkflowInstance = null!
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _activity.ExecuteAsync(context, cts.Token));
    }
}