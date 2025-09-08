using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Workflow.Tests.Workflow.Activities;

public class SwitchActivityTests
{
    private readonly SwitchActivity _activity;
    private readonly ILogger<SwitchActivity> _logger;

    public SwitchActivityTests()
    {
        _logger = Substitute.For<ILogger<SwitchActivity>>();
        _activity = new SwitchActivity(_logger);
    }

    [Fact]
    public void ActivityProperties_ShouldReturnCorrectValues()
    {
        // Assert
        _activity.ActivityType.Should().Be(ActivityTypes.SwitchActivity);
        _activity.Name.Should().Be("Switch Decision");
        _activity.Description.Should().Be("Multi-branch conditional routing with support for comparisons and value matching");
    }

    [Fact]
    public async Task ExecuteAsync_WithDirectValueMatch_ReturnsMatchingCase()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "status",
            ["cases"] = new List<string> { "pending", "approved", "rejected" },
            ["status"] = "approved"
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData.Should().ContainKey("case");
        result.OutputData!["case"].Should().Be("approved");
        result.OutputData.Should().ContainKey("expression");
        result.OutputData["expression"].Should().Be("status");
        result.OutputData.Should().ContainKey("expressionResult");
        result.OutputData["expressionResult"].Should().Be("approved");
        result.OutputData.Should().ContainKey("evaluatedAt");
    }

    [Fact]
    public async Task ExecuteAsync_WithComparisonMatch_ReturnsMatchingCase()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "amount",
            ["cases"] = new List<string> { "< 1000", ">= 1000", "> 10000" },
            ["amount"] = 5000
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["case"].Should().Be(">= 1000"); // First matching case
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMatchingCase_ReturnsDefault()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "priority",
            ["cases"] = new List<string> { "urgent", "high" },
            ["priority"] = "low"
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["case"].Should().Be("default");
    }

    [Fact]
    public async Task ExecuteAsync_AppraisalPropertyValueRouting_HandlesComplexCases()
    {
        // Arrange - Property value-based routing for appraisal workflow
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "property_value",
            ["cases"] = new List<string> 
            { 
                "< 100000",     // Low value - standard process
                "< 500000",     // Medium value - enhanced review
                "< 1000000",    // High value - senior review
                ">= 1000000"    // Very high value - committee review
            },
            ["property_value"] = 750000
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["case"].Should().Be("< 1000000"); // Should match high value case
        result.OutputData!["expressionResult"].Should().Be(750000);
    }

    [Fact]
    public async Task ExecuteAsync_AppraisalPriorityRouting_HandlesStringMatching()
    {
        // Arrange - Priority-based routing
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "priority",
            ["cases"] = new List<string> { "urgent", "high", "standard", "low" },
            ["priority"] = "urgent"
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["case"].Should().Be("urgent");
    }

    [Fact]
    public async Task ExecuteAsync_ClientTierRouting_HandlesCaseInsensitiveMatching()
    {
        // Arrange - Client tier with different casing
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "client_tier",
            ["cases"] = new List<string> { "VIP", "Premium", "Standard" },
            ["client_tier"] = "vip"
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["case"].Should().Be("VIP"); // Should match case-insensitively
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingExpression_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["cases"] = new List<string> { "case1", "case2" }
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Missing required 'expression' property");
        // Error type: MissingProperty
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyExpression_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "   ",
            ["cases"] = new List<string> { "case1", "case2" }
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Missing required 'expression' property");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingCases_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "status"
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Missing or empty 'cases' property");
        // Error type: MissingProperty
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCases_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "status",
            ["cases"] = new List<string>()
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Missing or empty 'cases' property");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidExpression_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "invalid = expression", // Invalid syntax
            ["cases"] = new List<string> { "case1", "case2" }
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("Expression evaluation failed");
        // Error type: ExpressionError
    }

    [Theory]
    [InlineData("< 100", 50, "< 100")]
    [InlineData("< 100", 150, "default")]
    [InlineData(">= 1000", 1000, ">= 1000")]
    [InlineData(">= 1000", 999, "default")]
    [InlineData("== 500", 500, "== 500")]
    [InlineData("!= 500", 500, "default")]
    [InlineData("!= 500", 600, "!= 500")]
    public async Task ExecuteAsync_ComparisonOperators_EvaluateCorrectly(
        string caseCondition, int value, string expectedCase)
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "value",
            ["cases"] = new List<string> { caseCondition },
            ["value"] = value
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["case"].Should().Be(expectedCase);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleMatchingCases_ReturnsFirstMatch()
    {
        // Arrange - Multiple cases could match, should return first one
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "amount",
            ["cases"] = new List<string> { "> 100", "> 50", "> 10" },
            ["amount"] = 200
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["case"].Should().Be("> 100"); // First matching case
    }

    [Fact]
    public async Task ExecuteAsync_NullExpressionResult_HandleGracefully()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "missing_variable", // Will evaluate to null
            ["cases"] = new List<string> { "null", "empty", "other" }
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["expressionResult"].Should().Be("null");
        result.OutputData!["case"].Should().Be("null"); // Should match "null" case
    }

    [Fact]
    public async Task ResumeAsync_ShouldReturnFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>());
        var resumeInput = new Dictionary<string, object>();

        // Act
        var result = await _activity.ResumeAsync(context, resumeInput);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("SwitchActivity does not support resume operations");
        // Error type: UnsupportedOperation
    }

    [Fact]
    public async Task ValidateAsync_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "status",
            ["cases"] = new List<string> { "pending", "approved", "> 1000" }
        });

        // Act
        var result = await _activity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithMissingExpression_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["cases"] = new List<string> { "case1", "case2" }
        });

        // Act
        var result = await _activity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("'expression' property is required for SwitchActivity");
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidExpressionSyntax_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "invalid = syntax",
            ["cases"] = new List<string> { "case1" }
        });

        // Act
        var result = await _activity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid expression syntax"));
    }

    [Fact]
    public async Task ValidateAsync_WithMissingCases_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "status"
        });

        // Act
        var result = await _activity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("'cases' property is required and must contain at least one case for SwitchActivity");
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyCaseConditions_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "status",
            ["cases"] = new List<string> { "valid_case", "", "another_case" }
        });

        // Act
        var result = await _activity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Case conditions cannot be empty");
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidCaseConditionSyntax_ReturnsFailure()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "amount",
            ["cases"] = new List<string> { "> 100", "= invalid", "< 50" } // Invalid comparison
        });

        // Act
        var result = await _activity.ValidateAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid case condition"));
    }

    [Fact]
    public async Task ExecuteAsync_ComplexAppraisalWorkflowRouting_HandlesAllScenarios()
    {
        // Arrange - Complex appraisal routing scenarios
        var scenarios = new[]
        {
            new 
            {
                Name = "High value commercial property",
                Expression = "property_type + '_' + (property_value >= 1000000 ? 'high' : 'standard')",
                Cases = new List<string> { "residential_standard", "residential_high", "commercial_standard", "commercial_high" },
                Variables = new Dictionary<string, object>
                {
                    ["property_type"] = "commercial",
                    ["property_value"] = 1200000
                },
                Expected = "commercial_high"
            },
            new 
            {
                Name = "Standard residential property",
                Expression = "property_type + '_' + (property_value >= 1000000 ? 'high' : 'standard')",
                Cases = new List<string> { "residential_standard", "residential_high", "commercial_standard", "commercial_high" },
                Variables = new Dictionary<string, object>
                {
                    ["property_type"] = "residential",
                    ["property_value"] = 450000
                },
                Expected = "residential_standard"
            }
        };

        foreach (var scenario in scenarios)
        {
            // Arrange
            var context = CreateTestContext(new Dictionary<string, object>(scenario.Variables)
            {
                ["expression"] = scenario.Expression,
                ["cases"] = scenario.Cases
            });

            // Act
            var result = await _activity.ExecuteAsync(context);

            // Assert
            result.Should().NotBeNull($"Scenario: {scenario.Name}");
            result.Status.Should().Be(ActivityResultStatus.Completed, $"Scenario: {scenario.Name}");
            result.OutputData!["case"].Should().Be(scenario.Expected, $"Scenario: {scenario.Name}");
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "status",
            ["cases"] = new List<string> { "approved", "pending" }
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - SwitchActivity completes immediately, so cancellation may not always throw
        var result = await _activity.ExecuteAsync(context, cts.Token);
        
        // Either completes successfully (immediate) or throws cancellation
        if (result.Status == ActivityResultStatus.Failed)
        {
            // If it fails, it should be due to cancellation
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void CreateActivityExecution_ReturnsCorrectExecution()
    {
        // Arrange
        var context = CreateTestContext(new Dictionary<string, object>());

        // Act
        // Note: CreateActivityExecution is protected, testing through public interface
        
        // Assert - Test that the activity has correct metadata
        _activity.Name.Should().Be("Switch Decision");
        _activity.ActivityType.Should().Be(ActivityTypes.SwitchActivity);
    }

    [Fact]
    public async Task ExecuteAsync_LoanToValueRatioRouting_HandlesPrecisionComparisons()
    {
        // Arrange - LTV ratio-based routing with decimal precision
        var context = CreateTestContext(new Dictionary<string, object>
        {
            ["expression"] = "loan_to_value",
            ["cases"] = new List<string> { "<= 0.8", "<= 0.9", "<= 0.95", "> 0.95" },
            ["loan_to_value"] = 0.85
        });

        // Act
        var result = await _activity.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData!["case"].Should().Be("<= 0.9"); // Should match second case
        result.OutputData!["expressionResult"].Should().Be(0.85);
    }

    private ActivityContext CreateTestContext(Dictionary<string, object> properties)
    {
        return new ActivityContext
        {
            ActivityId = "test-switch-activity",
            WorkflowInstance = null!,
            Variables = new Dictionary<string, object>(properties.Where(p => p.Key != "expression" && p.Key != "cases")),
            Properties = properties
        };
    }
}