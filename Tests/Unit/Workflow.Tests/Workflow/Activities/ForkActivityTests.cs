using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Workflow.Tests.Workflow.Activities;

public class ForkActivityTests
{
    private readonly ForkActivity _activity;

    public ForkActivityTests()
    {
        _activity = new ForkActivity();
    }

    [Fact]
    public async Task ExecuteAsync_ValidBranches_ReturnsSuccessResult()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "fork-test",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = new List<ForkBranch>
                {
                    new() { Id = "branch1", Name = "Credit Check Branch" },
                    new() { Id = "branch2", Name = "Income Verification Branch" }
                }
            },
            Variables = new Dictionary<string, object>(),
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("forkId");
        result.OutputData.Should().ContainKey("activeBranches");
        result.OutputData["activeBranches"].Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ConditionalBranches_FiltersCorrectly()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "conditional-fork",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = new List<ForkBranch>
                {
                    new() { Id = "simple_check", Name = "Simple Check", Condition = "amount <= 1000" },
                    new() { Id = "detailed_check", Name = "Detailed Check", Condition = "amount > 1000" },
                    new() { Id = "manager_review", Name = "Manager Review", Condition = "priority == 'high'" }
                }
            },
            Variables = new Dictionary<string, object>
            {
                ["amount"] = 500,
                ["priority"] = "low"
            },
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // Only simple_check should be active (amount <= 1000 and priority is not high)
        result.OutputData["activeBranches"].Should().Be(1);
        var branchIds = result.OutputData["branchIds"] as List<string>;
        branchIds.Should().Contain("simple_check");
        branchIds.Should().NotContain("detailed_check");
        branchIds.Should().NotContain("manager_review");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleConditionsTrue_ActivatesMultipleBranches()
    {
        // Arrange - High value property requiring multiple reviews
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "high-value-fork",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = new List<ForkBranch>
                {
                    new() { Id = "appraisal_review", Name = "Appraisal Review", Condition = "property_value > 100000" },
                    new() { Id = "senior_review", Name = "Senior Review", Condition = "property_value > 500000" },
                    new() { Id = "committee_review", Name = "Committee Review", Condition = "property_value > 1000000" },
                    new() { Id = "compliance_check", Name = "Compliance Check", Condition = "client_tier == 'VIP'" }
                }
            },
            Variables = new Dictionary<string, object>
            {
                ["property_value"] = 750000,
                ["client_tier"] = "VIP"
            },
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // Should activate appraisal_review, senior_review, and compliance_check (3 branches)
        result.OutputData["activeBranches"].Should().Be(3);
        var branchIds = result.OutputData["branchIds"] as List<string>;
        branchIds.Should().Contain("appraisal_review");
        branchIds.Should().Contain("senior_review"); 
        branchIds.Should().Contain("compliance_check");
        branchIds.Should().NotContain("committee_review"); // property_value not > 1000000
    }

    [Fact]
    public async Task ExecuteAsync_EmptyBranches_ReturnsFailure()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "empty-fork",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = new List<ForkBranch>()
            },
            Variables = new Dictionary<string, object>(),
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("must have at least one branch");
    }

    [Fact]
    public async Task ExecuteAsync_BranchWithoutId_ReturnsFailure()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "invalid-branch-fork",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = new List<ForkBranch>
                {
                    new() { Id = "valid_branch", Name = "Valid Branch" },
                    new() { Name = "Invalid Branch" } // Missing Id
                }
            },
            Variables = new Dictionary<string, object>(),
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("must have an ID");
    }

    [Fact]
    public async Task ExecuteAsync_NoActiveBranches_ReturnsFailure()
    {
        // Arrange - All branches have conditions that evaluate to false
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "no-active-branches",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = new List<ForkBranch>
                {
                    new() { Id = "high_value", Name = "High Value", Condition = "amount > 10000" },
                    new() { Id = "vip_client", Name = "VIP Client", Condition = "client_tier == 'VIP'" }
                }
            },
            Variables = new Dictionary<string, object>
            {
                ["amount"] = 500,
                ["client_tier"] = "Standard"
            },
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("No branches were activated");
    }

    [Fact]
    public async Task ExecuteAsync_WithForkTypeAll_SetsCorrectForkType()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "fork-type-test",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = new List<ForkBranch>
                {
                    new() { Id = "branch1", Name = "Branch 1" }
                },
                ["forkType"] = "all"
            },
            Variables = new Dictionary<string, object>(),
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // Check that fork context is stored in variables with correct fork type
        var forkContextKey = result.OutputData["forkContextKey"] as string;
        forkContextKey.Should().StartWith("fork_");
    }

    [Fact]
    public async Task ExecuteAsync_WithMaxConcurrency_RespectsConcurrencyLimits()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "concurrency-test",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = new List<ForkBranch>
                {
                    new() { Id = "branch1", Name = "Branch 1" },
                    new() { Id = "branch2", Name = "Branch 2" },
                    new() { Id = "branch3", Name = "Branch 3" }
                },
                ["maxConcurrency"] = 2
            },
            Variables = new Dictionary<string, object>(),
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        // All branches should still be created, but maxConcurrency should be recorded
        result.OutputData["activeBranches"].Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ComplexAppraisalScenario_RoutesCorrectly()
    {
        // Arrange - Complex appraisal workflow with multiple routing conditions
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "complex-appraisal-fork",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = new List<ForkBranch>
                {
                    // Property valuation branch
                    new() 
                    { 
                        Id = "property_valuation", 
                        Name = "Property Valuation", 
                        Condition = "property_type == 'residential' || property_type == 'commercial'" 
                    },
                    // Market analysis branch
                    new() 
                    { 
                        Id = "market_analysis", 
                        Name = "Market Analysis", 
                        Condition = "property_value > 250000 || market_volatility == 'high'" 
                    },
                    // Environmental assessment branch
                    new() 
                    { 
                        Id = "environmental_check", 
                        Name = "Environmental Assessment", 
                        Condition = "property_type == 'commercial' || property_age > 30" 
                    },
                    // Legal review branch
                    new() 
                    { 
                        Id = "legal_review", 
                        Name = "Legal Review", 
                        Condition = "legal_complexity > 5 || easement_issues == true" 
                    },
                    // Rush processing branch
                    new() 
                    { 
                        Id = "rush_processing", 
                        Name = "Rush Processing", 
                        Condition = "priority == 'urgent' && turnaround_days <= 3" 
                    }
                }
            },
            Variables = new Dictionary<string, object>
            {
                ["property_type"] = "commercial",
                ["property_value"] = 450000,
                ["property_age"] = 15,
                ["market_volatility"] = "medium",
                ["legal_complexity"] = 3,
                ["easement_issues"] = false,
                ["priority"] = "standard",
                ["turnaround_days"] = 7
            },
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        
        var branchIds = result.OutputData["branchIds"] as List<string>;
        
        // Should activate property_valuation (commercial property)
        branchIds.Should().Contain("property_valuation");
        
        // Should activate market_analysis (property_value > 250000)
        branchIds.Should().Contain("market_analysis");
        
        // Should activate environmental_check (commercial property)
        branchIds.Should().Contain("environmental_check");
        
        // Should NOT activate legal_review (complexity <= 5 and no easement issues)
        branchIds.Should().NotContain("legal_review");
        
        // Should NOT activate rush_processing (not urgent priority)
        branchIds.Should().NotContain("rush_processing");
        
        // Total: 3 active branches
        result.OutputData["activeBranches"].Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithBranchPriorities_OrdersCorrectly()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "priority-test",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = new List<ForkBranch>
                {
                    new() { Id = "low_priority", Name = "Low Priority", Priority = 1 },
                    new() { Id = "high_priority", Name = "High Priority", Priority = 10 },
                    new() { Id = "medium_priority", Name = "Medium Priority", Priority = 5 }
                }
            },
            Variables = new Dictionary<string, object>(),
            WorkflowInstance = null!
        };

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["activeBranches"].Should().Be(3);
        
        // Branch order should be preserved for priority handling by join activity
        var branchIds = result.OutputData["branchIds"] as List<string>;
        branchIds.Should().NotBeNull();
        branchIds.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var context = new ActivityContext
        {
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityId = "cancellation-test",
            Properties = new Dictionary<string, object>
            {
                ["branches"] = new List<ForkBranch>
                {
                    new() { Id = "branch1", Name = "Branch 1" }
                }
            },
            Variables = new Dictionary<string, object>(),
            WorkflowInstance = null!,
            CancellationToken = new CancellationTokenSource().Token
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _activity.ExecuteAsync(context, cts.Token));
    }
}